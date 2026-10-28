"""將指定輪次封裝為獨立、可驗證的歷史回放。"""

import argparse
import hashlib
import json
from pathlib import Path


def encode(value):
    return (json.dumps(value, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")


def pack(source: Path, destination: Path, episode: int, model=None):
    destination.parent.mkdir(parents=True, exist_ok=True)
    temporary = destination.with_suffix(".partial")
    count = 0
    digest = hashlib.sha256()
    complete = False
    with source.open(encoding="utf-8-sig") as log, temporary.open("xb") as output:
        run = json.loads(next(log))
        if run.get("kind") != "run" or run.get("rules") not in ("survival-v9", "survival-v10") or not run["config"]["foraging"]:
            raise ValueError("記錄缺少相容的世界設定。")
        header = {"kind": "replay", "schemaVersion": 1, "rules": run["rules"],
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
                if state["step"] != count:
                    raise ValueError("指定場次影格缺漏或重複。")
                write({"kind": "frame", "state": state})
                count += 1
            if row["kind"] == "episode_end":
                complete = True
        if not complete or count < 2:
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


def validate(path: Path):
    count = 0
    digest = hashlib.sha256()
    with path.open("rb") as source:
        raw = next(source)
        header = json.loads(raw)
        if header.get("kind") != "replay" or header.get("schemaVersion") != 1 or header.get("rules") not in ("survival-v9", "survival-v10"):
            raise ValueError("不相容的回放版本。")
        digest.update(raw)
        for raw in source:
            row = json.loads(raw)
            if row.get("kind") == "complete":
                if count < 2 or row["frames"] != count or row["sha256"] != digest.hexdigest() or source.read(1):
                    raise ValueError("回放容量、影格或完整性驗證失敗。")
                return {**header, "frames": count, "bytes": path.stat().st_size}
            state = row.get("state", {})
            if row.get("kind") != "frame" or state.get("step") != count or state.get("episode") != header["episode"]:
                raise ValueError("回放影格缺漏或順序錯誤。")
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
