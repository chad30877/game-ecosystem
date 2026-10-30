"""將指定輪次封裝為獨立、可驗證的同建置回放。"""

import argparse
import hashlib
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent

def current_build():
    return json.loads((ROOT / "unity/Builds/build.json").read_text(encoding="utf-8-sig"))["build"]
CURRENT_RULES = "survival-v15"

def compatible(rules, schema):
    return rules == CURRENT_RULES and schema == 2

def encode(value):
    return (json.dumps(value, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")


def pack(source: Path, destination: Path, episode: int, model=None, expected_build=None):
    if destination.exists() and not source.exists():
        result = validate(destination, expected_build)
        return {"path": str(destination.resolve()), "episode": result["episode"], "frames": result["frames"], "bytes": result["bytes"]}
    destination.parent.mkdir(parents=True, exist_ok=True)
    temporary = destination.with_suffix(".partial")
    count = 0
    digest = hashlib.sha256()
    complete = False
    with source.open(encoding="utf-8-sig") as log, temporary.open("xb") as output:
        run = json.loads(next(log))
        if run.get("kind") != "run" or run.get("rules") != CURRENT_RULES or not run["config"]["foraging"]:
            raise ValueError("記錄缺少相容的世界設定。")
        if not run.get("build") or run["build"] != (expected_build or current_build()):
            raise ValueError("來源建置未知或與目前播放器不同。")
        if run.get("actionVersion") != 2:
            raise ValueError("新規則須提供 actionVersion=2，舊動作格式不能直接使用。")
        header = {"kind": "replay", "schemaVersion": 2, "build": run["build"], "rules": run["rules"],
                  "actionVersion": run.get("actionVersion", 1), "diagnostic": run.get("diagnostic", False),
                  "singleEpisode": run.get("singleEpisode", False), "worldSteps": run.get("worldSteps", 0),
                  "summaryLog": run.get("summaryLog", False), "metricsLog": run.get("metricsLog", False),
                  "config": run["config"], "seed": run["seed"], "episode": episode,
                  "sourceRun": run["run"], "control": run["control"],
                  "policyMode": run.get("policyMode"), "model": model,
                  "scenario": run.get("scenario"), "rulesSpecies": run.get("rulesSpecies", []),
                  "resourceOrder": ["health", "stamina", "mana", "satiety"]}
        def write(value):
            data = encode(value)
            output.write(data)
            digest.update(data)
        write(header)
        for line in log:
            row = json.loads(line)
            state = row.get("state")
            if state is None or state["episode"] != episode:
                continue
            if row["kind"] in ("reset", "step"):
                if run["rules"] == CURRENT_RULES and not state.get("fruitInformationAvailable"):
                    raise ValueError("新規則快照缺少果實資訊。")
                if state["step"] != count:
                    raise ValueError("指定場次影格缺漏或重複。")
                write({"kind": "frame", "state": state})
                count += 1
            if row["kind"] in ("episode_end", "observation_end"):
                complete = True
        if not complete or count < 1:
            raise ValueError("指定場次尚未完整結束，不能封裝回放。")
        output.write(encode({"kind": "complete", "frames": count, "sha256": digest.hexdigest()}))
    if destination.exists():
        raise ValueError("回放已存在，不覆寫原始資料。")
    temporary.rename(destination)
    file_digest = hashlib.sha256()
    with destination.open("rb") as recorded:
        for chunk in iter(lambda: recorded.read(1024 * 1024), b""):
            file_digest.update(chunk)
    result = {"path": str(destination.resolve()), "episode": episode, "frames": count,
              "bytes": destination.stat().st_size,
              "sha256": file_digest.hexdigest()}
    destination.with_suffix(".json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    return result


def validate(path: Path, expected_build=None):
    count = 0
    digest = hashlib.sha256()
    with path.open("rb") as source:
        raw = next(source)
        header = json.loads(raw)
        if header.get("kind") != "replay" or not compatible(header.get("rules"), header.get("schemaVersion")):
            raise ValueError("不相容的回放版本。")
        if header["rules"] == CURRENT_RULES and header.get("actionVersion") != 2:
            raise ValueError("新規則動作版本不相容。")
        if not header.get("build") or header["build"] != (expected_build or current_build()):
            raise ValueError("回放建置未知或與目前播放器不同。")
        digest.update(raw)
        for raw in source:
            row = json.loads(raw)
            if row.get("kind") == "complete":
                if count < 1 or row["frames"] != count or row["sha256"] != digest.hexdigest() or source.read(1):
                    raise ValueError("回放容量、影格或完整性驗證失敗。")
                return {**header, "frames": count, "bytes": path.stat().st_size}
            state = row.get("state", {})
            if row.get("kind") != "frame" or state.get("step") != count or state.get("episode") != header["episode"]:
                raise ValueError("回放影格缺漏或順序錯誤。")
            if header["rules"] == CURRENT_RULES and not state.get("fruitInformationAvailable"):
                raise ValueError("新規則快照缺少果實資訊。")
            digest.update(raw)
            count += 1
    raise ValueError("回放缺少完整結尾。")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--episode", type=int, default=1)
    args = parser.parse_args()
    try:
        result = pack(args.source, args.output, args.episode) if args.output else validate(args.source)
        print(json.dumps(result, ensure_ascii=False, indent=2))
    except (ValueError, OSError, KeyError, StopIteration) as error:
        parser.exit(1, f"回放記錄無效：{error}\n")


if __name__ == "__main__":
    main()
