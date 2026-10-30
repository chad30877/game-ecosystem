"""以隔離資料核對手動清理腳本；所有呼叫均不執行刪除。"""
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import uuid

ROOT = Path(__file__).resolve().parents[1]


def validate():
    output = ROOT / "artifacts/recording-validation" / ("legacy-cleanup-" + uuid.uuid4().hex)
    results = []
    for case in ("preview", "multi-preview", "changed", "tampered-plan", "junction", "protected", "cache", "model", "audit", "duplicate", "outside", "missing", "missing-apply-whatif", "apply-without-hash", "generator", "generator-protected-output"):
        workspace = output / case
        scripts = workspace / "scripts"
        scripts.mkdir(parents=True)
        (workspace / "training").mkdir()
        (workspace / "training/recording.py").write_text("# 隔離專案標記\n", encoding="utf-8")
        (workspace / "AGENTS.md").write_text("隔離測試。\n", encoding="utf-8")
        shutil.copyfile(ROOT / "scripts/cleanup-legacy-artifacts.ps1", scripts / "cleanup-legacy-artifacts.ps1")
        entry = {"path": "artifacts/old-run/world.jsonl", "bytes": 8,
                 "sha256": hashlib.sha256(b"old-data").hexdigest(), "reason": "結果已整合至正式文件"}
        target = workspace / entry["path"]
        target.parent.mkdir(parents=True)
        target.write_bytes(b"old-data")
        entry["modifiedUnixMs"] = target.stat().st_mtime_ns // 1_000_000
        plan = {"schema": 1, "purpose": "隔離測試", "count": 1, "bytes": 8, "files": [entry]}
        if case == "protected": entry["path"] = ".simulation/run/world.jsonl"
        if case == "cache": entry["path"] = "artifacts/cache/data.jsonl"
        if case == "model": entry["path"] = "artifacts/models/policy.pt"
        if case == "audit": entry["path"] = "artifacts/cleanup/batch/plan.json"
        if case == "outside": entry["path"] = "artifacts/../../preserve.txt"
        if case == "missing": entry["path"] = "artifacts/old-run/absent.jsonl"
        if case == "duplicate": plan.update(count=2, bytes=16, files=[entry, {**entry, "path": "artifacts/old-run/../old-run/world.jsonl"}])
        if case == "multi-preview": plan.update(count=2, bytes=16, files=[entry, {**entry, "path": "artifacts/old-run/absent.jsonl"}])
        plan_path = scripts / "test-plan.json"
        plan_path.write_text(json.dumps(plan), encoding="utf-8")
        plan_hash = hashlib.sha256(plan_path.read_bytes()).hexdigest()
        sentinel = workspace / "preserve.txt"
        sentinel.write_bytes(b"preserve")
        if case == "changed":
            target.write_bytes(b"new-data")
        if case == "tampered-plan":
            with plan_path.open("ab") as stream:
                stream.write(b"\n")
        if case == "junction":
            outside = workspace / "outside"
            outside.mkdir()
            artifacts = workspace / "artifacts"
            artifacts.mkdir(exist_ok=True)
            link = artifacts / "alias"
            quoted_link = "'" + str(link).replace("'", "''") + "'"
            quoted_target = "'" + str(outside).replace("'", "''") + "'"
            subprocess.run(["powershell.exe", "-NoProfile", "-NonInteractive", "-Command",
                            f"New-Item -ItemType Junction -Path {quoted_link} -Target {quoted_target} | Out-Null"], check=True, capture_output=True)
            entry["path"] = "artifacts/alias/world.jsonl"
            plan_path.write_text(json.dumps(plan), encoding="utf-8")
        if case.startswith("generator"):
            shutil.copyfile(ROOT / "training/prepare_cleanup_plan.py", workspace / "training/prepare_cleanup_plan.py")
            candidates = scripts / "candidates.json"
            candidates.write_text(json.dumps({"purpose": "生成測試", "files": [{"path": entry["path"], "reason": entry["reason"]}]}), encoding="utf-8")
            plan_path = workspace / "artifacts/cleanup/generated/plan.json"
            if case == "generator-protected-output": plan_path = workspace / ".simulation/plan.json"
            generated = subprocess.run([str(ROOT / ".venv/Scripts/python.exe"), str(workspace / "training/prepare_cleanup_plan.py"), str(candidates), "--output", str(plan_path)], capture_output=True)
            if case == "generator-protected-output":
                assert generated.returncode != 0 and not (workspace / ".simulation").exists()
                assert sentinel.read_bytes() == b"preserve" and target.read_bytes() == b"old-data"
                results.append({"case": case, "exitCode": generated.returncode, "sentinelPreserved": True})
                continue
            assert generated.returncode == 0
            assert json.loads(plan_path.read_text(encoding="utf-8"))["files"][0]["sha256"] == entry["sha256"]
        command = ["powershell.exe", "-NoProfile", "-NonInteractive", "-File", str(scripts / "cleanup-legacy-artifacts.ps1"), "-Plan", str(plan_path)]
        if case == "missing":
            command += ["-Verify", "-PlanSha256", plan_hash]
        if case == "missing-apply-whatif":
            command += ["-Apply", "-WhatIf"]
        if case == "apply-without-hash": command += ["-Apply"]
        if case == "tampered-plan": command += ["-PlanSha256", plan_hash]
        process = subprocess.run(command, capture_output=True)
        success = case in ("preview", "multi-preview", "missing", "missing-apply-whatif", "generator")
        assert (process.returncode == 0) == success, (case, process.stderr.decode(errors="replace"))
        assert sentinel.read_bytes() == b"preserve"
        assert target.read_bytes() == (b"new-data" if case == "changed" else b"old-data")
        reports = list((workspace / "artifacts/cleanup").rglob("manual-*.jsonl"))
        if success:
            summary = json.loads(reports[0].read_text(encoding="utf-8").splitlines()[-1])
            assert summary["deleted"] == 0 and summary["total"] == len(plan["files"])
            assert summary["complete"] == (case == "missing")
            assert summary["mode"] == ("verify" if case == "missing" else "preview")
        results.append({"case": case, "exitCode": process.returncode, "sentinelPreserved": True})
    (output / "results.json").write_text(json.dumps(results, indent=2), encoding="utf-8")
    print(json.dumps({"output": str(output), "results": results}))


if __name__ == "__main__":
    validate()
