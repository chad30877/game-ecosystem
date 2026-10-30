"""核對入口可觀察的完整性判定，不啟動訓練。"""
import json
from argparse import Namespace
from pathlib import Path
import tempfile
import unittest

from recording import require_complete, simulation_directory

ROOT = Path(__file__).resolve().parents[1]


class CompletenessTests(unittest.TestCase):
    def test_failed_recording_cannot_be_reported_as_success(self):
        with tempfile.TemporaryDirectory(dir=ROOT / "artifacts/recording-validation") as folder:
            path = Path(folder)
            (path / "run.json").write_text(json.dumps({"status": "incomplete"}), encoding="utf-8")
            (path / "summary.json").write_text(json.dumps({"status": "complete"}), encoding="utf-8")
            with self.assertRaises(ValueError):
                require_complete(path)

    def test_missing_segments_cannot_be_reported_as_success(self):
        with tempfile.TemporaryDirectory(dir=ROOT / "artifacts/recording-validation") as folder:
            path = Path(folder)
            (path / "run.json").write_text(json.dumps({"status": "complete", "recordingComplete": True, "segments": 2}), encoding="utf-8")
            (path / "summary.json").write_text(json.dumps({"status": "complete", "episodes": [1, 2]}), encoding="utf-8")
            with self.assertRaises(ValueError):
                require_complete(path, full=True)

    def test_explicit_custom_output_stays_outside_management(self):
        with tempfile.TemporaryDirectory(dir=ROOT / "artifacts/recording-validation") as folder:
            path = Path(folder)
            self.assertEqual(simulation_directory(path, False), path)

    def test_detailed_rules_evaluation_reads_current_records(self):
        from evaluate import rollout
        with tempfile.TemporaryDirectory(dir=ROOT / "artifacts/recording-validation") as folder:
            path = Path(folder)
            config = json.loads((ROOT / "training/foraging.json").read_text(encoding="utf-8"))
            config.update(individualsPerSpecies=1, episodeSteps=3, disableReproduction=True)
            config_path = path / "config.json"
            config_path.write_text(json.dumps(config), encoding="utf-8")
            args = Namespace(player=ROOT / "unity/Builds/World.exe", world_config=config, config=config_path, worker_id=23)
            result = rollout(args, path / "evaluation", 101, {species: "rules" for species in ("Flame", "Frost", "Moss")}, {}, {}, {}, True)
            self.assertEqual(result["steps"], 3)
            self.assertEqual(result["reason"], "time_limit")


if __name__ == "__main__":
    unittest.main()
