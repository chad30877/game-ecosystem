"""透過實際 Player／腳本入口核對受管資料與同建置回放。"""
import json
from pathlib import Path
import subprocess
import uuid

ROOT = Path(__file__).resolve().parents[1]
PLAYER = ROOT / "unity/Builds/World.exe"
OUT = ROOT / "artifacts/recording-validation" / ("integration-" + uuid.uuid4().hex)


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def run():
    OUT.mkdir(parents=True, exist_ok=True)
    config = read(ROOT / "training/foraging.json")
    config.update(individualsPerSpecies=1, episodeSteps=3, disableReproduction=True, disableAging=True)
    config_path = OUT / "config.json"
    config_path.write_text(json.dumps(config), encoding="utf-8")
    reports = []
    baseline = None
    for index in range(8):
        folder = OUT / str(index)
        folder.mkdir(exist_ok=False)
        command = [str(PLAYER), "-batchmode", "-nographics", "--control", "rules", "--world-config", str(config_path),
                   "--world-steps", "5", "--world-log", str(folder / "world.jsonl"), "-logFile", str(folder / "player.log")]
        if index in (0, 1):
            command += ["--record", "true"]
        subprocess.run(command, check=True)
        info = read(folder / "run.json")
        summary = read(folder / "summary.json")
        rows = list(map(json.loads, (folder / "world.jsonl").read_text(encoding="utf-8-sig").splitlines()))
        assert info["status"] == summary["status"] == "complete"
        assert all("state" not in row and "individual" not in row for row in rows)
        assert [episode["end"]["step"] for episode in summary["episodes"]] == [3, 2]
        assert summary["stopReason"] == "step_limit"
        if baseline is None: baseline = summary
        assert summary == baseline, "輕量與完整錄製不得改變世界結果"
        if index in (0, 1):
            assert info["segments"] == 2 and info["recordingComplete"]
            assert not (folder / "record-source.jsonl").exists()
            for replay in folder.glob("episode-*.replay.jsonl"):
                subprocess.run([str(PLAYER), "-batchmode", "-nographics", "--replay", str(replay), "--replay-seek", "0,last,0",
                                "--world-log", str(OUT / ("seek-" + str(index) + "-" + replay.stem) / "world.jsonl"), "-logFile", str(folder / "seek-player.log")], check=True)
                frames = [row["state"] for row in map(json.loads, replay.read_text(encoding="utf-8").splitlines()) if row["kind"] == "frame"]
                seek = OUT / ("seek-" + str(index) + "-" + replay.stem) / "world.jsonl"
                actual = [row["state"] for row in map(json.loads, seek.read_text(encoding="utf-8").splitlines()) if row["kind"] == "snapshot"]
                assert actual == [frames[0], frames[-1], frames[0]]
        reports.append({"run": info["run"], "build": info["build"], "episodes": 2, "steps": 5, "full": index < 2})
    (OUT / "results.json").write_text(json.dumps(reports, indent=2), encoding="utf-8")
    managed = []
    managed_root = ROOT / ".simulation"
    for index in range(8):
        before = set(managed_root.iterdir())
        command = [str(PLAYER), "-batchmode", "-nographics", "--control", "rules", "--world-config", str(config_path), "--world-steps", "5",
                   "-logFile", str(OUT / ("managed-" + str(index) + ".log"))]
        if index < 2: command += ["--record", "true"]
        subprocess.run(command, check=True)
        created = [path for path in set(managed_root.iterdir()) - before if path.is_dir()]
        assert len(created) == 1
        managed.append(read(created[0] / "run.json"))
    remaining = [read(path / "run.json") for path in managed_root.iterdir() if path.is_dir() and (path / "run.json").exists()]
    kept = {info["run"] for info in remaining}
    assert managed[0]["run"] not in kept and managed[1]["run"] in kept
    assert all(info["run"] in kept for info in managed[-5:])
    assert len(remaining) == 6 and sum(info["recordingComplete"] for info in remaining) == 1
    (OUT / "managed-results.json").write_text(json.dumps({"executions": managed, "remaining": remaining}, indent=2), encoding="utf-8")
    print(json.dumps(reports))


if __name__ == "__main__":
    run()
