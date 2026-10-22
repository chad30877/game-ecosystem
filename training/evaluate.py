"""固定十組種子，比較初始策略、規則與已訓練策略；不執行更新。"""

import argparse
from collections import defaultdict
from concurrent.futures import ProcessPoolExecutor, as_completed
from datetime import datetime
import json
from pathlib import Path
import statistics
import time
import uuid

import torch
import yaml
from mlagents.trainers.policy.torch_policy import TorchPolicy
from mlagents.trainers.settings import NetworkSettings
from mlagents.trainers.torch_entities.networks import SimpleActor
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.exception import UnityCommunicationException, UnityCommunicatorStoppedException, UnityEnvironmentException
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel

from recording import pack
from metrics import accumulate
from resources import peak_memory
from session import ROOT, SPECIES, compatibility, file_record, load_source, read_json, write_json


SEEDS = tuple(range(101, 111))


def policy(spec, settings, weights):
    instance = TorchPolicy(0, spec, NetworkSettings(**settings), SimpleActor,
                           {"conditional_sigma": False, "tanh_squash": False})
    if weights is not None:
        instance.actor.load_state_dict(weights, strict=True)
    instance.actor.eval()
    return instance


def complete(path):
    if not path.exists():
        return False
    with path.open("rb") as source:
        source.seek(max(0, path.stat().st_size - 128))
        return b'"kind":"episode_complete"' in source.read()


def metrics(log: Path):
    individuals = {}
    individual_metrics = {}
    configurations = {}
    population = []
    final = None
    resources = None
    def group(item):
        key = item["species"] + ":" + ",".join(map(str, sorted(item["loadout"])))
        return configurations.setdefault(key, {"species": item["species"], "loadout": sorted(item["loadout"]),
            "individuals": 0, "completedAges": [], "observedSeconds": [], "incompleteAges": [],
            "births": 0, "foodEnergy": 0.0, "damageDealt": 0.0, "damageTaken": 0.0})
    with log.open(encoding="utf-8-sig") as source:
        for line in source:
            row = json.loads(line)
            kind = row["kind"]
            state = row.get("state")
            if kind == "agent_joined":
                item = row["individual"]
                bucket = group(item)
                bucket["individuals"] += 1
                individuals[item["individual"]] = (bucket, state["time"], item["age"])
                individual_metrics[item["individual"]] = bucket
            if kind in ("sample", "step"):
                sample = row if kind == "sample" else state
                if sample["step"] % 50 == 0 or sample.get("endReason"):
                    population.append({"time": sample["time"], "counts": {s["species"]: s["incompleteLives"] for s in sample["statistics"]}})
                accumulate(sample["events"], individual_metrics)
            if kind == "reset":
                population.append({"time": state["time"], "counts": {s["species"]: s["incompleteLives"] for s in state["statistics"]}})
            if kind == "episode_end":
                final = state
                if not population or population[-1]["time"] != state["time"]:
                    population.append({"time": state["time"], "counts": {s["species"]: s["incompleteLives"] for s in state["statistics"]}})
            if kind == "resources": resources = row
    if final is None or final["endReason"] not in ("all_dead", "time_limit"):
        raise ValueError("評估場次未完整結束。")
    dead = {item["individual"]: item for item in final["deaths"]}
    alive = {item["individual"]: item for item in final["individuals"]}
    for identifier, (bucket, started, age) in individuals.items():
        if identifier in dead:
            item = dead[identifier]
            bucket["completedAges"].append(item["age"])
            bucket["observedSeconds"].append(max(0, item["time"] - started))
        elif identifier in alive:
            bucket["incompleteAges"].append(alive[identifier]["age"])
            bucket["observedSeconds"].append(max(0, final["time"] - started))
        else:
            raise ValueError("個體缺少死亡或未完成生命週期結果。")
    species = {}
    for stat in final["statistics"]:
        buckets = [b for b in configurations.values() if b["species"] == stat["species"]]
        ages = [age for b in buckets for age in b["completedAges"]]
        species[stat["species"]] = {**stat, "meanCompletedAge": statistics.mean(ages) if ages else None,
            "completedLives": len(ages), "extinct": stat["incompleteLives"] == 0,
            "incompleteAges": [age for b in buckets for age in b["incompleteAges"]],
            "meanObservedSeconds": statistics.mean([v for b in buckets for v in b["observedSeconds"]])}
    return {"steps": final["step"], "seconds": final["time"], "reason": final["endReason"],
            "species": species, "configurations": configurations, "population": population, "resources": resources}


def rollout(args, output, seed, modes, weights, network, initial, capture):
    output.mkdir()
    log = output / "world.jsonl"
    rules = ",".join(s for s in SPECIES if modes[s] == "rules")
    channel = EngineConfigurationChannel()
    channel.set_configuration_parameters(time_scale=100, width=960, height=720)
    additional = ["--control", "python", "--policy-mode", "fixed", "--world-seed", str(seed),
                  "--world-config", str(args.config), "--world-log", str(log), "--summary-log", "true",
                  "--rules-species", rules, "--single-episode", "true"]
    if capture:
        additional += ["--record-log", str(output / "record-source.jsonl"), "--record-episodes", "1"]
    env = UnityEnvironment(file_name=str(args.player), seed=seed, no_graphics=True, worker_id=args.worker_id,
                           side_channels=[channel], log_folder=str(output), additional_args=additional)
    steps = 0
    unity_peak = 0
    try:
        env.reset()
        specs = {name.split("?")[0]: (name, spec) for name, spec in env.behavior_specs.items()}
        if set(specs) != set(SPECIES): raise ValueError("三物種 behavior 不完整。")
        if not initial:
            for index, species in enumerate(SPECIES):
                torch.manual_seed(args.initial_seed + index)
                instance = policy(specs[species][1], network[species], None)
                initial[species] = instance.get_weights()
                torch.save({"Policy": initial[species], "seed": args.initial_seed + index}, args.output / f"initial-{species}.pt")
        policies = {s: policy(specs[s][1], network[s], weights[s] if modes[s] == "trained" else initial[s]) for s in SPECIES}
        random_states = {}
        for index, s in enumerate(SPECIES):
            torch.manual_seed(seed * 10 + index)
            random_states[s] = torch.get_rng_state()
        started = time.perf_counter()
        while steps <= args.world_config["episodeSteps"]:
            for species in SPECIES:
                behavior = specs[species][0]
                decisions, _ = env.get_steps(behavior)
                if len(decisions) == 0: continue
                torch.set_rng_state(random_states[species])
                action = policies[species].get_action(decisions).action
                random_states[species] = torch.get_rng_state()
                env.set_actions(behavior, action)
            try:
                env.step()
            except (UnityCommunicationException, UnityCommunicatorStoppedException, UnityEnvironmentException):
                if not complete(log): raise
                break
            steps += 1
            if steps % 50 == 0 and env._process is not None:
                unity_peak = max(unity_peak, peak_memory(env._process.pid) or 0)
            if complete(log): break
        elapsed = time.perf_counter() - started
        if not complete(log): raise ValueError("評估超過配置輪次長度而未結束。")
    finally:
        env.close()
    result = metrics(log)
    if result["resources"] is not None:
        result["resources"]["peakWorkingSetBytes"] = unity_peak or None
    result.update({"seed": seed, "controllers": modes, "wallSeconds": elapsed,
                   "worldStepsPerSecond": result["steps"] / elapsed})
    write_json(output / "result.json", result)
    return result


def evaluate_condition(args, name, modes, weights, network, initial, seeds):
    torch.set_num_threads(1)
    runs = []
    for seed in seeds:
        print(f"評估 {name} / {seed}", flush=True)
        folder = args.output / f"{name}-{seed}"
        capture = seed == SEEDS[0] and name in ("initial", "rules", "trained")
        result = rollout(args, folder, seed, modes, weights, network, initial, capture)
        if capture:
            result["replay"] = pack(folder / "record-source.jsonl", folder / "episode-1.replay.jsonl", 1,
                {"controllers": modes, "trained": args.checkpoints,
                 "initial": {s: file_record(args.output / f"initial-{s}.pt") for s in SPECIES}, "compatibility": args.signature})
        runs.append(result)
        write_json(args.output / f"progress-{name}.json", {"completed": len(runs), "seeds": seeds[:len(runs)]})
    return runs


def describe(values):
    values = [v for v in values if v is not None]
    return {"n": len(values), "mean": statistics.mean(values) if values else None,
            "sd": statistics.stdev(values) if len(values) > 1 else None,
            "min": min(values) if values else None, "max": max(values) if values else None}


def report(output, cases, metadata):
    aggregates = {}
    for name, runs in cases.items():
        species = {}
        for s in SPECIES:
            species[s] = {field: describe([r["species"][s][field] for r in runs]) for field in
                          ("meanCompletedAge", "meanObservedSeconds", "births", "populationLimitHits", "incompleteLives", "completedLives")}
            species[s]["extinctionFrequency"] = sum(r["species"][s]["extinct"] for r in runs) / len(runs)
        configurations = defaultdict(list)
        for run in runs:
            for key, bucket in run["configurations"].items(): configurations[key].append(bucket)
        aggregates[name] = {"species": species, "configurations": {key: {
            "individuals": sum(b["individuals"] for b in buckets),
            "completedLives": sum(len(b["completedAges"]) for b in buckets),
            "incompleteLives": sum(len(b["incompleteAges"]) for b in buckets),
            "meanObservedSeconds": describe([statistics.mean(b["observedSeconds"]) for b in buckets]),
            "meanCompletedAge": describe([statistics.mean(b["completedAges"]) if b["completedAges"] else None for b in buckets]),
            **{field: describe([b[field] for b in buckets]) for field in ("births", "foodEnergy", "damageDealt", "damageTaken")}
        } for key, buckets in configurations.items()}}
    speeds = [r["worldStepsPerSecond"] for runs in cases.values() for r in runs]
    peaks = [r["resources"]["peakWorkingSetBytes"] for runs in cases.values() for r in runs if r["resources"] and r["resources"]["peakWorkingSetBytes"]]
    disk = sum(p.stat().st_size for p in output.rglob("*") if p.is_file())
    result = {**metadata, "status": "學習效果未驗證", "reason": "僅一份已訓練實驗與獨立初始參考策略，尚不足以建立可靠改善或拆分配置策略的因果證據。",
              "nextExperiment": "保持世界與十組種子不變，保存訓練的精確零步策略，執行至少三次獨立訓練並提高至每物種十萬步；逐物種重做配對比較。",
              "policySplit": "維持同物種共用策略；尚無多次獨立訓練及獨立配置策略對照，不提出拆分依據。",
              "budget": {"worldStepsPerSecond": describe(speeds), "unityPeakWorkingSetBytes": describe(peaks),
                         "diskBytes": disk, "secondsPerMillionWorldStepsAtMeasuredRate": 1_000_000 / statistics.mean(speeds),
                         "limitation": "推論吞吐量不含 PPO 更新成本；三物種個體經驗步與世界步不同，不直接視為訓練工時。"},
              "cases": cases, "aggregates": aggregates}
    training = metadata["trainingResources"]
    training_steps = training["worldSteps"]
    training_seconds = training["wallSeconds"]
    result["budget"]["training"] = training
    result["budget"]["training"]["worldStepsPerSecond"] = training_steps / training_seconds if training_seconds else None
    result["budget"]["training"]["secondsPerMillionWorldSteps"] = training_seconds * 1_000_000 / training_steps if training_steps else None
    result["budget"]["training"]["telemetryBytesPerMillionWorldSteps"] = training["worldLogBytes"] * 1_000_000 / training_steps if training_steps else None
    result["budget"]["training"]["combinedPeakUpperBoundBytes"] = (training["trainerPeakWorkingSetBytes"] + training["unityPeakWorkingSetBytes"]) if training["trainerPeakWorkingSetBytes"] and training["unityPeakWorkingSetBytes"] else None
    write_json(output / "evaluation.json", result)
    template = (ROOT / "training/evaluation.html").read_text(encoding="utf-8")
    (output / "evaluation.html").write_text(template.replace("__RESULT__", json.dumps(result, ensure_ascii=False).replace("<", "\\u003c")), encoding="utf-8")
    lines = ["# 學習成效對照", "", "**學習效果未驗證**", "", "[族群曲線與配置比較](evaluation.html)", "", result["reason"], "", "固定種子：" + ", ".join(map(str, SEEDS)), "",
             "單物種對照固定另兩物種為初始策略；共同演化觀察比較三物種共同切換。初始參考為另存的零更新策略，並非訓練來源的精確零步權重。", "",
             "| 條件 | 物種 | 完成生命平均年齡 mean ± sd | 出生 mean ± sd | 滅絕頻率 | 未完成樣本 mean |", "|---|---|---|---|---|---|"]
    def fmt(d):
        return "無完成樣本" if d["mean"] is None else f"{d['mean']:.2f} ± {d['sd'] or 0:.2f}"
    for name, aggregate in aggregates.items():
        for s, values in aggregate["species"].items():
            lines.append(f"| {name} | {s} | {fmt(values['meanCompletedAge'])} | {fmt(values['births'])} | {values['extinctionFrequency']:.0%} | {values['incompleteLives']['mean']:.1f} |")
    lines += ["", "完整逐配置樣本、變異、族群曲線、上限觸發、未完成個體年齡、版本與參數保存在 evaluation.json。起始成年個體已有年齡，完成生命年齡與本次觀測存活秒數分開呈現；未完成樣本不當作完整壽命。", "",
              "## 預算與限制", "", f"平均推論 {statistics.mean(speeds):.1f} 世界步／秒；目前輸出 {disk:,} bytes。Unity 記憶體峰值見 JSON，未包含 Python／trainer，不能直接當作訓練記憶體預算。", "",
              f"來源訓練實測 {training_steps:,} 世界步／{training_seconds:.2f} 秒，trainer 峰值 {training['trainerPeakWorkingSetBytes']} bytes，Unity 峰值 {training['unityPeakWorkingSetBytes']} bytes，保存容量 {training['diskBytes']:,} bytes。兩程序各自峰值不代表同時峰值；工時與磁碟量依相同配置線性外推，族群增長及長訓練可能改變成本。", "",
              result["budget"]["limitation"], "", result["policySplit"], "", "下一個實驗：" + result["nextExperiment"]]
    (output / "evaluation.md").write_text("\n".join(lines) + "\n", encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--player", type=Path, required=True)
    parser.add_argument("--trained", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--initial-seed", type=int, default=1729)
    parser.add_argument("--workers", type=int, choices=(1, 2, 3), default=3)
    args = parser.parse_args()
    torch.set_num_threads(1)
    args.player = args.player.resolve(strict=True)
    template = yaml.safe_load((ROOT / "training/toolchain.yaml").read_text(encoding="utf-8"))
    signature = compatibility(args.player, template)
    source, trainer, manifest = load_source(args.trained, signature, None, None)
    args.output = args.output.resolve() / f"evaluate-{datetime.now():%Y%m%d-%H%M%S}-{uuid.uuid4().hex[:8]}"
    args.output.mkdir(parents=True)
    args.world_config = manifest["worldConfig"]
    args.config = args.output / "world-config.json"
    write_json(args.config, args.world_config)
    weights = {s: torch.load(trainer / s / "checkpoint.pt", map_location="cpu", weights_only=True)["Policy"] for s in SPECIES}
    before = {s: file_record(trainer / s / "checkpoint.pt") for s in SPECIES}
    network = {s: template["behaviors"][s]["network_settings"] for s in SPECIES}
    conditions = {"initial": {s: "initial" for s in SPECIES}, "rules": {s: "rules" for s in SPECIES}, "trained": {s: "trained" for s in SPECIES}}
    for species in SPECIES:
        for mode in ("rules", "trained"):
            conditions[f"{species}-{mode}"] = {s: mode if s == species else "initial" for s in SPECIES}
    cases = {}
    initial = {}
    args.checkpoints = manifest["checkpoints"]
    args.signature = signature
    args.worker_id = 0
    try:
        cases["initial"] = evaluate_condition(args, "initial", conditions["initial"], weights, network, initial, SEEDS[:1])
        with ProcessPoolExecutor(max_workers=args.workers) as executor:
            jobs = {}
            for index, (name, modes) in enumerate(conditions.items(), start=1):
                worker_args = argparse.Namespace(**vars(args))
                worker_args.worker_id = index
                jobs[executor.submit(evaluate_condition, worker_args, name, modes, weights, network, initial,
                    SEEDS[1:] if name == "initial" else SEEDS)] = name
            for future in as_completed(jobs):
                name = jobs[future]
                cases.setdefault(name, []).extend(future.result())
                write_json(args.output / "progress.json", {k: len(v) for k, v in cases.items()})
        cases = {name: cases[name] for name in conditions}
        if before != {s: file_record(trainer / s / "checkpoint.pt") for s in SPECIES}:
            raise ValueError("評估意外改變來源 checkpoint。")
        training_world = 0
        unity_peak = None
        with (source / "world.jsonl").open(encoding="utf-8-sig") as log:
            for line in log:
                row = json.loads(line)
                training_world = max(training_world, row.get("totalSteps", 0))
                if row["kind"] == "resources": unity_peak = row["peakWorkingSetBytes"]
        training_resources = {**manifest.get("resources", {"wallSeconds": 0, "trainerPeakWorkingSetBytes": None}),
            "worldSteps": training_world, "unityPeakWorkingSetBytes": manifest.get("resources", {}).get("unityPeakWorkingSetBytes") or unity_peak or None,
            "diskBytes": sum(p.stat().st_size for p in source.rglob("*") if p.is_file()),
            "checkpointBytes": manifest["checkpointBytes"], "worldLogBytes": (source / "world.jsonl").stat().st_size}
        report(args.output, cases, {"schemaVersion": 1, "seeds": SEEDS, "worldConfig": args.world_config,
            "compatibility": signature, "source": str(source), "trainedCheckpoints": manifest["checkpoints"],
            "initialPolicies": {s: file_record(args.output / f"initial-{s}.pt") for s in SPECIES}, "initialSeed": args.initial_seed,
            "updatesEnabled": False, "independentTrainingRuns": 1, "evaluationWorkers": args.workers, "trainingResources": training_resources})
        print(args.output / "evaluation.md", flush=True)
    except Exception as error:
        write_json(args.output / "failure.json", {"error": str(error), "completed": {k: len(v) for k, v in cases.items()}})
        raise


if __name__ == "__main__":
    main()
