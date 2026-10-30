"""由 LLM 已分類的候選清單建立唯讀盤點；不掃描、不搬動或刪除檔案。"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PROTECTED = {"cache", "migration", "downloads"}
MODELS = {".pt", ".onnx", ".ckpt", ".safetensors"}


def no_links(path):
    for item in (path, *path.parents):
        try:
            if item.is_symlink() or getattr(item.lstat(), "st_file_attributes", 0) & 0x400:
                raise ValueError(f"拒絕連結或 junction：{item}")
        except FileNotFoundError:
            pass


def project_path(path):
    path = Path(path).absolute()
    no_links(path)
    path = path.resolve()
    if not path.is_relative_to(ROOT):
        raise ValueError(f"路徑需位於專案內：{path}")
    return path


def prepare(candidates, output):
    candidates = project_path(candidates)
    output = project_path(output)
    plan_root = ROOT / "artifacts/cleanup"
    if output == plan_root or not output.is_relative_to(plan_root) or output.suffix.lower() != ".json":
        raise ValueError("新清單只寫入 artifacts/cleanup/ 下的 JSON，不寫入受管資料或程式目錄。")
    data = json.loads(candidates.read_text(encoding="utf-8-sig"))
    purpose = data["purpose"].strip()
    if not purpose or not data["files"]:
        raise ValueError("須提供盤點目的與至少一個候選檔案。")
    entries = []
    seen = set()
    for item in data["files"]:
        relative = Path(item["path"])
        if relative.is_absolute() or ":" in item["path"]:
            raise ValueError("候選檔案使用專案相對路徑。")
        path = project_path(ROOT / relative)
        if not path.is_relative_to(ROOT / "artifacts"):
            raise ValueError(f"只接受非受管 artifacts 檔案：{relative}")
        group = path.relative_to(ROOT / "artifacts").parts[0].lower()
        if group in PROTECTED or group == "cleanup" or group.startswith("cleanup-") or path.suffix.lower() in MODELS:
            raise ValueError(f"模型、工具與清理稽核不納入此流程：{relative}")
        reason = item["reason"].strip()
        key = str(path).casefold()
        if not reason or key in seen or not path.is_file():
            raise ValueError(f"候選缺少理由、重複或不是檔案：{relative}")
        seen.add(key)
        before = path.stat()
        digest = hashlib.sha256()
        with path.open("rb") as source:
            for chunk in iter(lambda: source.read(4 * 1024 * 1024), b""):
                digest.update(chunk)
        after = path.stat()
        if (before.st_size, before.st_mtime_ns) != (after.st_size, after.st_mtime_ns):
            raise ValueError(f"盤點時檔案變動，請停止使用後重新盤點：{relative}")
        entries.append({"path": path.relative_to(ROOT).as_posix(), "bytes": after.st_size,
                        "modifiedUnixMs": after.st_mtime_ns // 1_000_000,
                        "sha256": digest.hexdigest(), "reason": reason})
    plan = {"schema": 1, "purpose": purpose, "createdAt": datetime.now(timezone.utc).isoformat(),
            "count": len(entries), "bytes": sum(entry["bytes"] for entry in entries), "files": entries}
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open("x", encoding="utf-8", newline="\n") as destination:
        destination.write(json.dumps(plan, ensure_ascii=False, indent=2) + "\n")
    return {"plan": str(output), "planSha256": hashlib.sha256(output.read_bytes()).hexdigest(),
            "count": plan["count"], "bytes": plan["bytes"]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("candidates", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    try:
        print(json.dumps(prepare(args.candidates, args.output), ensure_ascii=False))
    except (ValueError, OSError, KeyError, TypeError, AttributeError) as error:
        parser.exit(1, f"不能建立清理清單：{error}\n")


if __name__ == "__main__":
    main()
