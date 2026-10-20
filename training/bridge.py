"""透過同一世界入口驗證 Python 的觀察及移動往返。"""

import argparse
import json
from pathlib import Path
import time

import numpy as np
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--env", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--config", type=Path, required=True)
    parser.add_argument("--steps", type=int, default=512)
    parser.add_argument("--seed", type=int, default=1)
    parser.add_argument("--visible", action="store_true")
    args = parser.parse_args()
    if args.steps <= 0:
        parser.error("steps 必須大於零")
    args.output.mkdir(parents=True, exist_ok=False)
    channel = EngineConfigurationChannel()
    channel.set_configuration_parameters(time_scale=1 if args.visible else 20, width=960, height=720)
    env = UnityEnvironment(
        file_name=str(args.env.resolve()), seed=args.seed, no_graphics=not args.visible,
        side_channels=[channel], log_folder=str(args.output.resolve()),
        additional_args=["--world-log", str((args.output / "world.jsonl").resolve()),
                         "--world-config", str(args.config.resolve()),
                         "--world-steps", str(args.steps),
                         "--world-seed", str(args.seed), "--control", "python"],
    )
    try:
        env.reset()
        behavior, = env.behavior_specs
        observations = 0
        started = time.perf_counter()
        for step in range(args.steps):
            decisions, _ = env.get_steps(behavior)
            if len(decisions):
                observation = decisions.obs[0]
                if observation.shape[1] != 4 or not np.isfinite(observation).all():
                    raise RuntimeError("局部觀察格式不符")
                observations += len(decisions)
                # 固定向右再向上，讓畫面及狀態紀錄可直接核對移動。
                direction = [1.0, 0.0] if step < args.steps // 2 else [0.0, 1.0]
                actions = np.tile(direction, (len(decisions), 1)).astype(np.float32)
                env.set_actions(behavior, ActionTuple(continuous=actions))
            env.step()
        elapsed = time.perf_counter() - started
    finally:
        env.close()
    records = [json.loads(line) for line in (args.output / "world.jsonl").read_text().splitlines()]
    states = [record for record in records if record["kind"] in ("step", "boundary")]
    if not states or observations == 0:
        raise RuntimeError("未取得實際世界步進")
    if len(states) != args.steps:
        raise RuntimeError(f"世界步數不符：{len(states)} != {args.steps}")
    resets = {record["state"]["episode"]: record["state"] for record in records if record["kind"] == "reset"}
    moved = any(abs(record["state"]["x"] - resets[record["state"]["episode"]]["x"]) > 0.01
                for record in states)
    if not moved:
        raise RuntimeError("Python 動作未使個體移動")
    report = {"seed": args.seed, "visible": args.visible, "requested_steps": args.steps,
              "world_steps": len(states), "observations": observations,
              "seconds": elapsed, "steps_per_second": len(states) / elapsed,
              "moved": moved, "device": records[0]["device"]}
    (args.output / "bridge.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
