"""管理全新實驗、訓練保存與經驗延續。"""

import argparse
import copy
from datetime import datetime, timezone
import hashlib
from importlib.metadata import version
import io
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import uuid

import torch
import yaml

from summarize import summarize


SPECIES = ("Flame", "Frost", "Moss")
ROOT = Path(__file__).resolve().parent.parent


def read_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path: Path, value):
    temporary = path.with_suffix(".tmp")
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")
    temporary.replace(path)


def file_record(path: Path):
    with path.open("rb") as source:
        digest = hashlib.sha256()
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return {"bytes": path.stat().st_size, "sha256": digest.hexdigest()}


def checkpoint_records(trainer: Path):
    records = {}
    for species in SPECIES:
        path = trainer / species / "checkpoint.pt"
        if not path.is_file():
            raise ValueError(f"缺少 {species}/checkpoint.pt，無法恢復三物種訓練。")
        try:
            checkpoint = torch.load(path, map_location="cpu", weights_only=True)
            step = int(checkpoint["global_step"]["_GlobalSteps__global_step"][0])
            if not checkpoint["Policy"] or step < 0:
                raise ValueError("策略或步數無效")
            for key in ("Optimizer:value_optimizer", "Optimizer:critic"):
                if not checkpoint[key]:
                    raise ValueError("缺少 optimizer 狀態")
        except Exception as error:
            raise ValueError(f"{species} checkpoint 不完整或無法讀取：{error}") from error
        records[species] = {"step": step, **file_record(path)}
    return records


def compatibility(player: Path, trainer_config):
    return {
        "modelVersion": "tactical-v1",
        "rulesVersion": "skills-v1",
        "worldAssembly": file_record(player.parent / "World_Data/Managed/Assembly-CSharp.dll")["sha256"],
        "packages": {name: version(name) for name in ("mlagents", "mlagents-envs", "torch")},
        "trainer": trainer_config,
    }


def load_source(path: Path, signature, world_config, target_steps):
    # 同時接受執行目錄與原有 trainer 子目錄。
    source = path.resolve(strict=True)
    if not (source / "training.json").is_file():
        source = source.parent
    try:
        manifest = read_json(source / "training.json")
        if not isinstance(manifest, dict) or manifest.get("schemaVersion") != 1:
            raise ValueError("存檔缺少相容性資料；舊版訓練請另開全新實驗。")
        if manifest.get("status") not in ("completed", "stopped", "failed"):
            raise ValueError("此執行尚未完成保存，請等待 trainer 結束。")
        if not isinstance(manifest["seed"], int) or not isinstance(manifest["runId"], str) or not manifest["runId"]:
            raise ValueError("種子或執行識別無效。")
        if manifest["compatibility"] != signature:
            raise ValueError("模型介面、世界規則、套件或 trainer 配置不相容，請另開全新實驗。")
        if world_config is not None and manifest["worldConfig"] != world_config:
            raise ValueError("世界配置與存檔不同，請省略 WorldConfig 沿用存檔，或另開全新實驗。")
        if Path(manifest["trainerRunId"]).name != manifest["trainerRunId"]:
            raise ValueError("trainerRunId 必須是存檔內的目錄名稱。")
        trainer = source / manifest["trainerRunId"]
        records = checkpoint_records(trainer)
        if records != manifest["checkpoints"]:
            raise ValueError("checkpoint 與保存紀錄不符，資料可能缺漏或遭修改。")
        if target_steps is not None and any(target_steps <= record["step"] for record in records.values()):
            raise ValueError("TrainSteps 必須大於三物種已保存步數。")
        if not manifest["experimentId"] or read_json(source / "world-config.json") != manifest["worldConfig"]:
            raise ValueError("實驗識別或保存的世界配置不完整。")
        return source, trainer, manifest
    except (KeyError, OSError, TypeError, json.JSONDecodeError) as error:
        raise ValueError(f"續訓資料缺漏或格式無效：{error}") from error


def copy_output(output: Path, destination_root: Path):
    destination_root = destination_root.resolve()
    if destination_root == output or output in destination_root.parents:
        raise ValueError("備份／歷史目錄不能位於本次輸出內。")
    destination_root.mkdir(parents=True, exist_ok=True)
    required = sum(path.stat().st_size for path in output.rglob("*") if path.is_file())
    if shutil.disk_usage(destination_root).free < required:
        raise ValueError(f"{destination_root} 空間不足，需要 {required} bytes。")
    destination = destination_root / output.name
    shutil.copytree(output, destination)
    return {"path": str(destination), "bytes": required}


def train(args):
    player = args.player.resolve(strict=True)
    trainer_template = yaml.safe_load((ROOT / "training/toolchain.yaml").read_text(encoding="utf-8"))
    signature = compatibility(player, trainer_template)
    world_config = read_json(args.config) if args.config else None
    source = trainer_source = previous = None
    if args.resume:
        source, trainer_source, previous = load_source(args.resume, signature, world_config, None if args.observe else args.steps)
        world_config = previous["worldConfig"]
    if world_config is None:
        world_config = read_json(ROOT / "training/foraging.json")
    seed = args.seed if args.seed is not None else previous["seed"] if previous else 1
    run_id = f"{'observe' if args.observe else 'train'}-{datetime.now().strftime('%Y%m%d-%H%M%S')}-{uuid.uuid4().hex[:8]}"
    output = args.output.resolve() / run_id
    destinations = [path.resolve() for path in (args.backup, args.history) if path]
    if len(set(destinations)) != len(destinations):
        raise ValueError("備份與歷史目錄必須分開。")
    if any(path == output.parent or path == output or output in path.parents for path in destinations):
        raise ValueError("備份／歷史目錄須與活躍輸出分開，且不能位於本次輸出內。")
    output.mkdir(parents=True, exist_ok=False)
    trainer_id = previous["trainerRunId"] if previous else run_id
    if trainer_source:
        shutil.copytree(trainer_source, output / trainer_id)
    write_json(output / "world-config.json", world_config)
    manifest = {
        "schemaVersion": 1, "experimentId": previous["experimentId"] if previous else uuid.uuid4().hex,
        "runId": run_id, "trainerRunId": trainer_id, "source": str(source) if source else None,
        "operation": "observe" if args.observe else "resume" if previous else "new",
        "seed": seed, "targetSteps": None if args.observe else args.steps,
        "worldStepLimit": args.world_steps if args.observe else None,
        "worldConfig": world_config, "compatibility": signature, "status": "running",
        "startedAt": datetime.now(timezone.utc).isoformat(),
        "startingSteps": {name: previous["checkpoints"][name]["step"] if previous else 0 for name in SPECIES},
    }
    write_json(output / "training.json", manifest)
    trainer_template = copy.deepcopy(trainer_template)
    for settings in trainer_template["behaviors"].values():
        settings["max_steps"] = args.steps
    (output / "trainer.yaml").write_text(yaml.safe_dump(trainer_template), encoding="utf-8")
    command = [sys.executable, "-m", "mlagents.trainers.learn", str(output / "trainer.yaml"),
               "--env", str(player), "--run-id", trainer_id, "--results-dir", str(output),
               "--seed", str(seed), "--torch-device", "cpu", "--time-scale", "1" if args.visible else "20",
               "--width", "960", "--height", "720", "--max-lifetime-restarts", "0"]
    if previous:
        command.append("--resume")
    if args.observe:
        command.append("--inference")
    if args.visible:
        # 暫停期間不送出新的決策，容許長時間檢視而不觸發預設通訊逾時。
        command += ["--timeout-wait", "86400"]
    if not args.visible:
        command.append("--no-graphics")
    command += ["--env-args", "--world-log", str(output / "world.jsonl"), "--world-seed", str(seed),
                "--world-config", str(output / "world-config.json"), "--control", "python",
                "--policy-mode", "fixed" if args.observe else "training"]
    if args.observe:
        command += ["--world-steps", str(args.world_steps)]
    print(f"{'固定模型觀察' if args.observe else '續訓' if previous else '全新實驗'}：{manifest['experimentId']}\n輸出：{output}", flush=True)
    with (output / "trainer.log").open("w", encoding="utf-8") as log:
        with subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                              encoding="utf-8", errors="replace",
                              env={**os.environ, "PYTHONUNBUFFERED": "1", "PYTHONIOENCODING": "utf-8"}) as process:
            assert process.stdout is not None
            try:
                for line in process.stdout:
                    print(line, end="", flush=True)
                    log.write(line)
                    log.flush()
            except KeyboardInterrupt:
                # Ctrl+C 同時送至 trainer；等待其 finally 寫完模型。
                process.wait()
            exit_code = process.wait()
    manifest["finishedAt"] = datetime.now(timezone.utc).isoformat()
    try:
        manifest["checkpoints"] = checkpoint_records(output / trainer_id)
        if args.observe and (previous is None or manifest["checkpoints"] != previous["checkpoints"]):
            raise ValueError("固定觀察期間 checkpoint 意外改變。")
        manifest["status"] = "completed" if args.observe or all(record["step"] >= args.steps for record in manifest["checkpoints"].values()) else "stopped"
    except ValueError as error:
        manifest["status"] = "failed"
        write_json(output / "training.json", manifest)
        raise ValueError(f"{error}；請查看 {output / 'trainer.log'}。") from error
    if exit_code:
        manifest["status"] = "failed"
    manifest["checkpointBytes"] = sum(record["bytes"] for record in manifest["checkpoints"].values())
    write_json(output / "training.json", manifest)
    if (output / "world.jsonl").is_file():
        write_json(output / "training-summary.json", summarize(output / "world.jsonl"))
    if exit_code:
        raise ValueError(f"訓練失敗（{exit_code}），請查看 {output / 'trainer.log'}。已保留可讀取的 checkpoint。")
    copies = {}
    for label, destination in (("backup", args.backup), ("history", args.history)):
        if destination:
            copies[label] = copy_output(output, destination)
    if copies:
        write_json(output / "storage.json", copies)
    print(json.dumps({"output": str(output), "status": manifest["status"],
                      "checkpoints": manifest["checkpoints"], "copies": copies}, ensure_ascii=False), flush=True)


def main():
    if isinstance(sys.stdout, io.TextIOWrapper):
        sys.stdout.reconfigure(errors="replace")
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--player", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--config", type=Path)
    parser.add_argument("--seed", type=int)
    parser.add_argument("--steps", type=int, default=2048)
    parser.add_argument("--observe", action="store_true")
    parser.add_argument("--world-steps", type=int, default=512)
    operation = parser.add_mutually_exclusive_group()
    operation.add_argument("--resume", type=Path)
    operation.add_argument("--new", action="store_true")
    parser.add_argument("--backup", type=Path)
    parser.add_argument("--history", type=Path)
    parser.add_argument("--visible", action="store_true")
    args = parser.parse_args()
    if args.steps <= 0:
        parser.error("訓練步數必須為正數。")
    if args.observe and (not args.resume or args.world_steps < 0 or (not args.visible and args.world_steps == 0)):
        parser.error("固定觀察須指定 resume；無畫面須有正數 world-steps。")
    try:
        train(args)
    except (ValueError, OSError, yaml.YAMLError) as error:
        parser.exit(1, f"{error}\n")


if __name__ == "__main__":
    main()
