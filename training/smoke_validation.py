"""透過正式世界與回放入口核對煙幕情報及固定動作回歸。"""
import argparse
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def run(output: Path, baseline: bool = False, rules: bool = False, edge: bool = False) -> None:
    output.mkdir(parents=True, exist_ok=False)
    config = json.loads((ROOT / "training/foraging.json").read_text(encoding="utf-8"))
    config.update(individualsPerSpecies=2, episodeSteps=650, disableReproduction=True,
                  scanEnabled=False, halfExtent=10, naturalFoodLimit=1, naturalSpawnBatch=1)
    placements: list[dict[str, object]] = [dict(slot=i + 1, x=x, y=y, facing=0) for i, (x, y) in enumerate(
        [(-3, 0), (0, 0), (-2, 5), (-8, -8), (2, 0), (2, 1)])]
    for placement in placements[4:]:
        placement["loadout"] = ["SporeSmoke", "Cultivate"]
    if edge:
        placements[0].update(x=0, y=0)
        placements[4].update(x=2, y=4)
        placements[5].update(x=2, y=5)
    scenario = dict(placements=placements, foods=[dict(food=1, relocate=True, x=0, y=0)], commands=[
        dict(slot=5, fromStep=1, toStep=1, skillSlot=0),
        dict(slot=6, fromStep=1, toStep=1, skillSlot=0),
        dict(slot=1, fromStep=60, toStep=109, turn=1),
        dict(slot=1, fromStep=460, toStep=509, turn=1)])
    (output / "config.json").write_text(json.dumps(config), encoding="utf-8")
    (output / "scenario.json").write_text(json.dumps(scenario), encoding="utf-8")
    subprocess.run(["pwsh", "-NoProfile", "-File", str(ROOT / "scripts/world.ps1"),
                    "-Mode", "forage", "-Controller", "scripted", "-WorldConfig", str(output / "config.json"),
                    "-Scenario", str(output / "scenario.json"), "-Steps", "650", "-Seed", "7",
                    "-RecordEpisodes", "1", "-OutputRoot", str(output), "-Python", sys.executable]
                   + (["-RulesSpecies", "Flame,Frost"] if rules else []), check=True)
    replay = next(output.glob("forage-*/episode-1.replay.jsonl"))
    frames = [row["state"] for row in map(json.loads, replay.read_text(encoding="utf-8").splitlines()) if row["kind"] == "frame"]
    if not baseline:
        first = frames[1]["individuals"][0]
        assert len(first["smokeKnowledge"]) == 2 and all(row["visible"] and row["harmful"] for row in first["smokeKnowledge"])
        if rules:
            reasons = {row["decisionReason"] for frame in frames for row in frame["individuals"]}
            assert {"smoke_avoid", "smoke_exit"} <= reasons
        else:
            assert any(row["smokeKnowledge"] and not row["smokeKnowledge"][0]["visible"]
                       for frame in frames for row in frame["individuals"] if row["individual"] == first["individual"])
        assert not frames[-1]["individuals"][0]["smokeKnowledge"]
        subprocess.run(["pwsh", "-NoProfile", "-File", str(ROOT / "scripts/world.ps1"), "-Mode", "replay",
                        "-ReplayFrom", str(replay), "-SeekFrames", "0,1,150,650,1,0", "-OutputRoot", str(output)], check=True)
        returned = next(output.glob("replay-*/world.jsonl"))
        snapshots = [row["state"] for row in map(json.loads, returned.read_text(encoding="utf-8").splitlines()) if row.get("kind") == "snapshot"]
        assert snapshots == [frames[i] for i in (0, 1, 150, 650, 1, 0)]
    print(json.dumps(dict(replay=str(replay), frames=len(frames), baseline=baseline)))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("output", type=Path)
    parser.add_argument("--baseline", action="store_true")
    parser.add_argument("--rules", action="store_true")
    parser.add_argument("--edge", action="store_true")
    args = parser.parse_args()
    run(args.output.resolve(), args.baseline, args.rules, args.edge)
