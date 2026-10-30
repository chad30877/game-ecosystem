"""以隔離資料核對手動清理腳本；所有呼叫均不執行刪除。"""
import json
from pathlib import Path
import shutil
import subprocess
import uuid

ROOT = Path(__file__).resolve().parents[1]


def validate():
    output = ROOT / "artifacts/recording-validation" / ("legacy-cleanup-" + uuid.uuid4().hex)
    results = []
    for case in ("changed", "tampered-plan", "junction", "missing", "missing-apply-whatif"):
        workspace = output / case
        scripts = workspace / "scripts"
        scripts.mkdir(parents=True)
        (workspace / "training").mkdir()
        (workspace / "training/recording.py").write_text("# 隔離專案標記\n", encoding="utf-8")
        (workspace / "AGENTS.md").write_text("隔離測試。\n", encoding="utf-8")
        for name in ("cleanup-legacy-artifacts.ps1", "legacy-artifacts-20261030.json"):
            shutil.copyfile(ROOT / "scripts" / name, scripts / name)
        plan_path = scripts / "legacy-artifacts-20261030.json"
        plan = json.loads(plan_path.read_text(encoding="utf-8"))
        sentinel = workspace / "preserve.txt"
        sentinel.write_bytes(b"preserve")
        target = workspace / plan["files"][0]["path"]
        if case == "changed":
            target.parent.mkdir(parents=True)
            target.write_bytes(b"changed")
        if case == "tampered-plan":
            with plan_path.open("ab") as stream:
                stream.write(b"\n")
        if case == "junction":
            outside = workspace / "outside"
            outside.mkdir()
            artifacts = workspace / "artifacts"
            artifacts.mkdir()
            link = artifacts / Path(plan["files"][0]["path"]).parts[1]
            quoted_link = "'" + str(link).replace("'", "''") + "'"
            quoted_target = "'" + str(outside).replace("'", "''") + "'"
            subprocess.run(["powershell.exe", "-NoProfile", "-NonInteractive", "-Command",
                            f"New-Item -ItemType Junction -Path {quoted_link} -Target {quoted_target} | Out-Null"], check=True, capture_output=True)
        command = ["powershell.exe", "-NoProfile", "-NonInteractive", "-File", str(scripts / "cleanup-legacy-artifacts.ps1")]
        if case == "missing":
            command += ["-Verify"]
        if case == "missing-apply-whatif":
            command += ["-Apply", "-WhatIf"]
        process = subprocess.run(command, capture_output=True)
        assert (process.returncode == 0) == case.startswith("missing"), case
        assert sentinel.read_bytes() == b"preserve"
        if case == "changed":
            assert target.read_bytes() == b"changed"
        reports = list((workspace / "artifacts/cleanup-20261030").glob("manual-*.jsonl"))
        if case.startswith("missing"):
            summary = json.loads(reports[0].read_text(encoding="utf-8").splitlines()[-1])
            assert summary["deleted"] == 0 and summary["alreadyAbsent"] == 384
            assert summary["complete"] == (case == "missing")
            assert summary["mode"] == ("verify" if case == "missing" else "preview")
        results.append({"case": case, "exitCode": process.returncode, "sentinelPreserved": True})
    (output / "results.json").write_text(json.dumps(results, indent=2), encoding="utf-8")
    print(json.dumps({"output": str(output), "results": results}))


if __name__ == "__main__":
    validate()
