"""透過正式世界入口執行分階段觀察，保存配置、回放與逐步事件證據。"""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess

from summarize import summarize

ROOT = Path(__file__).resolve().parents[1]


def episode_report(log):
    # 世界滅絕可能提早換輪；只彙整已封裝回放的第一輪，保留原始多輪紀錄。
    first = log.with_name("episode-1.source.jsonl")
    with log.open(encoding="utf-8-sig") as source, first.open("w", encoding="utf-8") as target:
        for line in source:
            record = json.loads(line)
            if record.get("kind") == "run" or record.get("state", {}).get("episode") == 1:
                target.write(line)
    return summarize(first), inspect(first)


def inspect(log):
    events, reasons, transitions = Counter(), Counter(), Counter()
    previous, examples, births = {}, {}, []
    supply = {"initial": 0, "final": 0, "harvested": 0, "maxSources": 0,
              "existingSourceGrowth": 0, "newSourceStock": 0, "removedStock": 0, "cultivated": 0}
    last = None
    checks = Counter()
    with log.open(encoding="utf-8-sig") as source:
        for line in source:
            record = json.loads(line)
            state = record.get("state")
            if state is None or state["episode"] != 1 or record["kind"] not in ("reset", "step"):
                continue
            if last is None:
                supply["initial"] = sum(f["amount"] for f in state["foods"])
            supply["maxSources"] = max(supply["maxSources"], len(state["foods"]))
            if record["kind"] == "step":
                harvested = Counter()
                for event in state["events"]:
                    kind = event["kind"]
                    events[kind] += 1
                    examples.setdefault(kind, {"step": state["step"], "event": event})
                    if kind == "cultivated":
                        supply["cultivated"] += event["amount"]
                    if kind == "ate":
                        supply["harvested"] += event["amount"]
                        harvested[event["food"]] += event["amount"]
                    if kind == "born":
                        parent = next((i for i in state["individuals"] if i["individual"] == event["parent"]), None)
                        child = next((i for i in state["individuals"] if i["individual"] == event["individual"]), None)
                        births.append({"step": state["step"], "parent": event["parent"], "child": event["individual"],
                                       "parentSatiety": None if parent is None else parent["satiety"],
                                       "childSatiety": None if child is None else child["satiety"]})
                old_foods = {f["food"]: f for f in last["foods"]}
                new_foods = {f["food"]: f for f in state["foods"]}
                for key, food in new_foods.items():
                    if key in old_foods:
                        supply["existingSourceGrowth"] += food["amount"] - old_foods[key]["amount"] + harvested[key]
                    else:
                        supply["newSourceStock"] += food["amount"] + harvested[key]
                supply["removedStock"] += sum(f["amount"] for key, f in old_foods.items() if key not in new_foods)
                for individual in state["individuals"]:
                    for entity in individual.get("perception", []):
                        if entity.get("feeding"):
                            checks["visibleFeeding" if entity["visible"] else "hiddenFeedingViolation"] += 1
                        if not entity["visible"] and (entity.get("sourceX", 0) != 0 or entity.get("sourceY", 0) != 0):
                            checks["hiddenSourceViolation"] += 1
                    if individual.get("rootUntil", 0) > state["time"] and individual.get("feeding"):
                        checks["rootedFeeding"] += 1
                    if individual.get("foodMemory") and individual.get("hasFoodTarget"):
                        target = (individual["foodTargetX"], individual["foodTargetY"])
                        if any(not f["visible"] and (f["x"], f["y"]) == target for f in individual["foodMemory"]):
                            checks["rememberedTarget"] += 1
                    key, reason = individual["individual"], individual["decisionReason"]
                    reasons[reason] += 1
                    if key in previous and previous[key] != reason:
                        transition = previous[key] + " -> " + reason
                        transitions[transition] += 1
                        examples.setdefault(transition, {"step": state["step"], "individual": key})
                    previous[key] = reason
            last = state
    supply["final"] = sum(f["amount"] for f in last["foods"])
    return {"events": events, "decisionSteps": reasons, "transitions": transitions,
            "examples": examples, "births": births, "supply": supply, "seconds": last["time"], "checks": checks}


def run_case(output, name, config, scenario, seed, rules_species=None):
    folder = output / name
    folder.mkdir()
    for file, data in (("config.json", config), ("layout.json", scenario)):
        (folder / file).write_text(json.dumps(data, indent=2), encoding="utf-8")
    command = ["powershell", "-NoProfile", "-File", str(ROOT / "scripts/world.ps1"),
               "-Mode", "forage", "-Controller", "scripted" if rules_species else "rules",
               "-WorldConfig", str(folder / "config.json"), "-Scenario", str(folder / "layout.json"),
               "-Seed", str(seed), "-Steps", str(config["episodeSteps"]),
               "-Record", "-DiagnosticLog", "-OutputRoot", str(folder)]
    if rules_species:
        command.extend(["-RulesSpecies", rules_species])
    subprocess.run(command, check=True)
    log = next(folder.glob("forage-*/world.jsonl"))
    summary, observation = episode_report(log)
    result = {"name": name, "seed": seed, "log": str(log), "config": config,
              "summary": summary, "observation": observation}
    (folder / "result.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--probes", action="store_true", help="執行短程指定動作核對；不代表生態樣本")
    parser.add_argument("--refresh", action="store_true", help="僅重算既有紀錄的第一輪摘要")
    args = parser.parse_args()
    output = args.output.resolve()
    if args.refresh:
        results = json.loads((output / "results.json").read_text(encoding="utf-8"))
        for result in results:
            result["summary"], result["observation"] = episode_report(Path(result["log"]))
            (Path(result["log"]).parents[1] / "result.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
        (output / "results.json").write_text(json.dumps(results, indent=2), encoding="utf-8")
        return
    output.mkdir(parents=True, exist_ok=False)
    base = json.loads((ROOT / "training/scenarios/survival-isolation/Flame.json").read_text())
    layout = json.loads((ROOT / "training/scenarios/survival-isolation/layout.json").read_text())
    results = []
    if args.probes:
        for name, weak, alternative, species, loadout in (
            ("contest", False, False, "Flame", ["HotStrike", "IntimidatingRoar"]),
            ("switch", True, True, "Flame", ["Charge", "Shield"]),
            ("retreat", True, False, "Flame", ["Charge", "Shield"]),
            ("frost", False, False, "Frost", ["FrostbindArrow", "PiercingIceArrow"]),
            ("roots", False, False, "Moss", ["EntanglingRoots", "SporeSmoke"]),
            ("costly", False, False, "Frost", ["FrostbindArrow", "PiercingIceArrow"]),
            ("stale", False, False, "Flame", ["Shield", "IntimidatingRoar"]),
            ("hidden", False, False, "Flame", ["Shield", "IntimidatingRoar"]),
        ):
            config = {**base, "initialSpecies": "", "disableAging": False, "disableReproduction": False,
                      "episodeSteps": 1000, "naturalFoodLimit": 2 if alternative else 1}
            actor = {"Flame": 1, "Frost": 2, "Moss": 3}[species]
            feeder = 2 if actor == 1 else 1
            placements = [{"slot": i, "x": -7, "y": -7} for i in range(1, 4)]
            placements[actor - 1] = {"slot": actor, "x": 0, "y": 0, "facing": 0, "loadout": loadout,
                                     "setSatiety": True, "satiety": 40}
            if weak:
                placements[actor - 1].update(setVitals=True, health=20, stamina=100, mana=100)
            placements[feeder - 1] = {"slot": feeder, "x": 2, "y": 0, "facing": 180,
                                      "setSatiety": True, "satiety": 5}
            scenario = {"placements": placements,
                        "foods": [{"food": 1, "relocate": True, "x": 2, "y": 0}],
                        "commands": [{"slot": feeder, "fromStep": 1, "toStep": 400, "forage": True}]}
            if alternative:
                scenario["foods"].append({"food": 2, "relocate": True, "x": 2, "y": 2})
            if name == "costly":
                placements[actor - 1]["x"] = 1
                placements[feeder - 1]["loadout"] = ["HotStrike", "IntimidatingRoar"]
                scenario["commands"] = [
                    {"slot": feeder, "fromStep": 1, "toStep": 1, "forage": True},
                    {"slot": feeder, "fromStep": 2, "toStep": 2, "forage": True,
                     "attack": True, "skillSlot": 0, "aimX": 1},
                    {"slot": feeder, "fromStep": 3, "toStep": 400, "forage": True}]
            if name == "stale":
                scenario["foods"][0].update(setElapsedAge=True, elapsedAge=239.9)
            if name == "hidden":
                placements[actor - 1]["facing"] = 180
            results.append(run_case(output, name, config, scenario, 101, species))
            (output / "results.json").write_text(json.dumps(results, indent=2), encoding="utf-8")
        return
    for stage in ("reproduction", "limited", "combat"):
        species_list = ("Flame", "Frost", "Moss") if stage != "combat" else ("all",)
        for species in species_list:
            for seed in (101, 102):
                name = f"{stage}-{species}-{seed}"
                config = {**base, "initialSpecies": "" if species == "all" else species,
                          "disableAging": False, "disableReproduction": False,
                          "episodeSteps": 15000, "populationLimit": 24}
                if stage != "reproduction":
                    config.update(naturalFoodLimit=1, naturalSpawnInterval=60, naturalSpawnBatch=1)
                scenario = json.loads(json.dumps(layout))
                if stage == "combat":
                    scenario["placements"] = [{"slot": i + 1, "x": 1 + i * .5, "y": 0, "facing": 180} for i in range(3)]
                if stage != "reproduction":
                    scenario["foods"] = scenario["foods"][:1]
                results.append(run_case(output, name, config, scenario, seed))
                (output / "results.json").write_text(json.dumps(results, indent=2), encoding="utf-8")
                print(name, "完成", flush=True)


if __name__ == "__main__":
    main()
