"""核對逐顆果實回放的公開封裝與版本契約。"""
import copy
import json
from pathlib import Path
import tempfile
import recording


def run():
    with tempfile.TemporaryDirectory() as folder:
        root = Path(folder)
        state = {"episode": 1, "step": 0, "time": 0, "fruitInformationAvailable": True,
                 "fruits": [{"fruit": 1, "holder": 1, "source": 0, "expiresAt": 40}],
                 "fruitLedger": {"initial": 1}, "individuals": [{"individual": 1, "foodOperation": 2}]}
        run = {"kind": "run", "rules": "survival-v15", "actionVersion": 2, "seed": 101,
               "run": "fruit-contract", "control": "scripted", "config": {"foraging": True}}
        final = copy.deepcopy(state); final.update(step=1, time=.02)
        source = root / "source.jsonl"
        source.write_bytes(b"".join(recording.encode(row) for row in [run,
            {"kind": "reset", "state": state}, {"kind": "step", "state": final},
            {"kind": "episode_end", "state": final}]))
        result = recording.pack(source, root / "replay.jsonl", 1)
        header = recording.validate(root / "replay.jsonl")
        assert header["rules"] == "survival-v15" and header["schemaVersion"] == 2 and header["actionVersion"] == 2
        frames = [json.loads(line)["state"] for line in (root / "replay.jsonl").read_text(encoding="utf-8").splitlines()[1:-1]]
        assert frames == [state, final], "回放必須原樣保存果實與當時操作"
        assert result["frames"] == 2
        damaged = (root / "replay.jsonl").read_bytes().replace(b'"holder":1', b'"holder":2')
        (root / "damaged.jsonl").write_bytes(damaged)
        try:
            recording.validate(root / "damaged.jsonl")
        except ValueError:
            pass
        else:
            raise AssertionError("修改快照必須使校驗失敗")
        old_schema = (root / "replay.jsonl").read_bytes().replace(b'"schemaVersion":2', b'"schemaVersion":1')
        (root / "wrong-schema.jsonl").write_bytes(old_schema)
        try:
            recording.validate(root / "wrong-schema.jsonl")
        except ValueError:
            pass
        else:
            raise AssertionError("新規則不能套用舊格式版本")
    print("果實回放封裝契約通過")

def pack_showcase(folder, run_log, destination, seed=101):
    header = json.loads(run_log.open(encoding="utf-8-sig").readline())
    if header.get("rules") != recording.CURRENT_RULES or header.get("actionVersion") != 2:
        raise ValueError("診斷快照須使用新規則實際執行標頭，不能沿用 v14。")
    states = [json.loads((folder / f"fixture-{index}.json").read_text(encoding="utf-8-sig")) for index in range(3)]
    header.update(config=json.loads((folder / "fixture-config.json").read_text(encoding="utf-8-sig")),
        control="diagnostic", policyMode="diagnostic", scenario=None, diagnostic=True,
        seed=seed, worldSteps=states[-1]["step"], summaryLog=False, metricsLog=False)
    source = destination.with_suffix(".source.jsonl")
    records = [header] + [{"kind": "reset" if index == 0 else "step", "state": state} for index, state in enumerate(states)]
    records.append({"kind": "episode_end", "state": states[-1]})
    destination.parent.mkdir(parents=True, exist_ok=True)
    if destination.exists():
        raise FileExistsError("診斷回放已存在，不覆寫。")
    with source.open("xb") as output:
        output.write(b"".join(recording.encode(record) for record in records))
    return recording.pack(source, destination, states[0]["episode"])

if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--showcase-folder", type=Path)
    parser.add_argument("--run-log", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--seed", type=int, default=101)
    args = parser.parse_args()
    if args.showcase_folder:
        if not args.run_log or not args.output:
            parser.error("診斷打包須提供 run-log 與 output。")
        print(json.dumps(pack_showcase(args.showcase_folder, args.run_log, args.output, args.seed), indent=2))
    else:
        run()
