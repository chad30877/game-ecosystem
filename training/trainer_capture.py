"""在 PPO 更新前保存本次訓練真正使用的三物種初始權重。"""

import os
from pathlib import Path
import runpy

import torch
from mlagents.trainers.ppo.trainer import PPOTrainer


capture_dir = os.environ.get("WORLD_INITIAL_CAPTURE_DIR")
if capture_dir:
    original_create_policy = PPOTrainer.create_policy

    def create_policy_and_capture(self, parsed_behavior_id, behavior_spec):
        policy = original_create_policy(self, parsed_behavior_id, behavior_spec)
        destination = Path(capture_dir) / f"{self.brain_name}.pt"
        if destination.exists():
            raise ValueError(f"初始權重已存在：{destination}")
        torch.save({"Policy": policy.actor.state_dict(), "trainerSeed": self.seed}, destination)
        return policy

    PPOTrainer.create_policy = create_policy_and_capture

if __name__ == "__main__":
    torch.set_num_threads(1)
    print(f"WORLD_TRAINER_PID={os.getpid()}", flush=True)
    runpy.run_module("mlagents.trainers.learn", run_name="__main__")
