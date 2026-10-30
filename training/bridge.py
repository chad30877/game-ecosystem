"""透過世界入口驗證三物種、局部觀察、動態個體及移動往返。"""

import argparse
import json
from pathlib import Path
import time

import numpy as np
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel

SPECIES = {"Flame", "Frost", "Moss"}


def smoke_contract(args, config):
    """固定場景透過實際 ML-Agents 往返，比對共同煙幕觀察。"""
    args.output.mkdir(parents=True, exist_ok=False)
    channel = EngineConfigurationChannel()
    channel.set_configuration_parameters(time_scale=20, width=1280, height=720)
    log = args.output / "world.jsonl"
    env = UnityEnvironment(file_name=str(args.env.resolve()), seed=args.seed, no_graphics=True,
        side_channels=[channel], log_folder=str(args.output.resolve()), additional_args=[
            "--diagnostic-log", "true", "--world-log", str(log.resolve()), "--world-config", str(args.config.resolve()),
            "--world-scenario", str(args.smoke_scenario.resolve()), "--control", "python"])
    compared = 0
    try:
        env.reset()
        for step in range(args.steps + 1):
            records = [json.loads(line) for line in log.read_text(encoding="utf-8").splitlines()]
            states = [record["state"] for record in records if record["kind"] == "step"]
            state = states[-1] if states else None
            for behavior in env.behavior_specs:
                species = behavior.split("?")[0]
                decisions, terminals = env.get_steps(behavior)
                assert not len(terminals), "煙幕介面場景不可提前死亡"
                entities = next(obs for obs in decisions.obs if obs.ndim == 3)
                actual = [tuple(map(float, row)) for batch in entities for row in batch if row[0] == -5]
                if state is not None:
                    expected = []
                    for individual in state["individuals"]:
                        if individual["species"] != species:
                            continue
                        for smoke in individual["smokeKnowledge"]:
                            scale = config["halfExtent"] * 2
                            expected.append((-5, np.clip((smoke["x"] - individual["x"]) / scale, -1, 1),
                                np.clip((smoke["y"] - individual["y"]) / scale, -1, 1), smoke["radius"] / scale,
                                float(smoke["harmful"]), float(smoke["visible"]),
                                np.clip((state["time"] - smoke["lastSeenAt"]) / 5, 0, 1), 0, 0, 0))
                    assert len(actual) == len(expected) and np.allclose(sorted(actual), sorted(expected), atol=1e-5)
                    compared += len(actual)
                continuous = np.zeros((len(decisions), 4), dtype=np.float32)
                discrete = np.tile(np.array([1, 1 if species == "Moss" and step == 0 else 0, 0, 0, 0, 0, 0], dtype=np.int32), (len(decisions), 1))
                env.set_actions(behavior, ActionTuple(continuous=continuous, discrete=discrete))
            if step < args.steps:
                env.step()
    finally:
        env.close()
    assert compared > 0, "未收到煙幕觀察"
    report = {"steps": args.steps, "compared_smoke_rows": compared, "seed": args.seed}
    (args.output / "bridge.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--env", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--config", type=Path, required=True)
    parser.add_argument("--steps", type=int, default=512)
    parser.add_argument("--seed", type=int, default=1)
    parser.add_argument("--visible", action="store_true")
    parser.add_argument("--smoke-scenario", type=Path)
    parser.add_argument("--diagnostic-log", action="store_true")
    args = parser.parse_args()
    config = json.loads(args.config.read_text(encoding="utf-8-sig"))
    if args.smoke_scenario:
        return smoke_contract(args, config)
    if args.steps <= config["addStep"]:
        parser.error("steps 必須大於 addStep，才能驗證動態個體")
    args.output.mkdir(parents=True, exist_ok=False)
    channel = EngineConfigurationChannel()
    channel.set_configuration_parameters(time_scale=1 if args.visible else 20, width=1280, height=720)
    env = UnityEnvironment(
        file_name=str(args.env.resolve()), seed=args.seed, no_graphics=not args.visible,
        side_channels=[channel], log_folder=str(args.output.resolve()),
        additional_args=["--world-log", str((args.output / "world.jsonl").resolve()),
                         "--world-config", str(args.config.resolve()),
                         "--world-steps", str(args.steps),
                         "--world-seed", str(args.seed), "--control", "python"] + (["--diagnostic-log", "true"] if args.diagnostic_log else []),
    )
    seen: dict[int, str] = {}
    retired: set[int] = set()
    populations: dict[str, set[int]] = {species: set() for species in SPECIES}
    observations = 0
    origins = {}
    moved = {species: False for species in SPECIES}
    try:
        env.reset()
        if {name.split("?")[0] for name in env.behavior_specs} != SPECIES or len(env.behavior_specs) != 3:
            raise RuntimeError("三物種未各自連線至獨立 behavior")
        started = time.perf_counter()
        with (args.output / "connection.jsonl").open("w", encoding="utf-8") as connection:
            # 包含最後一次觀察，以核對末步的 terminal／新生個體。
            for step in range(args.steps + 1):
                local_step = step % config["episodeSteps"]
                expected = config["individualsPerSpecies"] - int(config["removeStep"] <= local_step < config["addStep"])
                for behavior in env.behavior_specs:
                    species = behavior.split("?")[0]
                    if list(env.behavior_specs[behavior].action_spec.discrete_branches) != [3, 3, 2, 2, 2, 7, 8]:
                        raise RuntimeError("動作版本 2 的七分支契約不符")
                    decisions, terminals = env.get_steps(behavior)
                    decision_ids = set(map(int, decisions.agent_id))
                    terminal_ids = set(map(int, terminals.agent_id))
                    if len(decisions) != expected or decision_ids & retired or decision_ids & terminal_ids:
                        raise RuntimeError(f"個體數量或識別不符：{behavior} step={step}")
                    for agent_id in decision_ids | terminal_ids:
                        if agent_id in seen and seen[agent_id] != species:
                            raise RuntimeError("跨物種混用個體識別")
                        seen[agent_id] = species
                    previous = populations[species]
                    if previous - decision_ids != terminal_ids:
                        raise RuntimeError(f"移除個體未送出 terminal：{behavior} step={step}")
                    retired.update(terminal_ids)
                    populations[species] = decision_ids
                    observations_by_rank = {obs.ndim: obs for obs in decisions.obs}
                    observation = observations_by_rank[2]
                    entities = observations_by_rank[3]
                    if observation.shape != (expected, 19) or entities.shape[2] != 10 or not all(np.isfinite(obs).all() for obs in decisions.obs):
                        raise RuntimeError("局部觀察格式不符")
                    for index, agent_id in enumerate(decisions.agent_id):
                        position = observation[index, :4].copy()
                        origins.setdefault(int(agent_id), position)
                        if np.max(np.abs(position - origins[int(agent_id)])) > .001: moved[species] = True
                    observations += len(decisions)
                    connection.write(json.dumps({"step": step, "behavior": behavior,
                                                 "decisions": sorted(decision_ids),
                                                 "terminals": sorted(terminal_ids)}) + "\n")
                    if step < args.steps:
                        # 同一 behavior 的個體可接受不同動作，便於核對共享不等於同步。
                        direction = np.array([1.0, 0.0] if step < args.steps // 2 else [0.0, 1.0], dtype=np.float32)
                        signs = np.array([1 if int(agent_id) % 2 else -1 for agent_id in decisions.agent_id], dtype=np.float32)
                        env.set_actions(behavior, ActionTuple(
                            continuous=np.concatenate((signs[:, None] * direction, signs[:, None] * direction), axis=1),
                            discrete=np.tile(np.array([2, 0, 0, 0, 0, 0, 0], dtype=np.int32), (expected, 1))))
                if step < args.steps:
                    env.step()
        elapsed = time.perf_counter() - started
    finally:
        env.close()
    records = [json.loads(line) for line in (args.output / "world.jsonl").read_text().splitlines()]
    summary = json.loads((args.output / "summary.json").read_text(encoding="utf-8-sig"))
    world_steps = sum(episode["end"]["step"] for episode in summary["episodes"])
    if summary["status"] != "complete" or world_steps != args.steps or observations == 0:
        raise RuntimeError("世界步數或資料完整性不符")
    if not all(moved.values()) or not retired:
        raise RuntimeError("未驗證三物種移動與 terminal")
    report = {"seed": args.seed, "visible": args.visible, "requested_steps": args.steps,
              "world_steps": world_steps, "observations": observations,
              "seconds": elapsed, "steps_per_second": world_steps / elapsed,
              "moved": moved, "behaviors": sorted(env.behavior_specs),
              "unique_agents": len(seen), "terminal_agents": len(retired),
              "device": records[0]["device"]}
    (args.output / "bridge.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
