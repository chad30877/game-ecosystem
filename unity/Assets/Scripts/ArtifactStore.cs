using System;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace Ecosystem
{
    // 租約以作業系統檔案鎖判斷；程序崩潰時會釋放，不依賴 running 字樣。
    public sealed class ArtifactStore
    {
        [Serializable] public sealed class BuildIdentity { public string build, root; }
        [Serializable] public sealed class RunInfo
        {
            public string owner = "ecosystem-simulation-v1";
            public string run, build, startedAt, stoppedAt, status = "running", stopReason, error;
            public bool recordingComplete;
            public int segments;
        }
        public readonly string Root, Build;
        public ArtifactStore(string root, string build)
        {
            if (string.IsNullOrWhiteSpace(build)) throw new InvalidDataException("缺少建置識別。");
            Root = Path.GetFullPath(root); Build = build;
            SafePath(Root);
        }
        public static BuildIdentity Identity
        {
            get
            {
                var asset = Resources.Load<TextAsset>("BuildIdentity");
                if (asset == null) throw new InvalidDataException("請先透過 BuildWorld 建置，缺少建置來源。");
                return JsonUtility.FromJson<BuildIdentity>(asset.text);
            }
        }
        public static ArtifactStore Current => new ArtifactStore(Identity.root, Identity.build);
        public static RunLease AcquirePlayback(string directory)
        {
            // 受管播放與刪除共用根鎖，避免清理釋放租約後被新播放器插入。
            var store = new ArtifactStore(Path.GetDirectoryName(directory), "playback");
            using (store.Gate())
            {
                SafePath(directory);
                var info = JsonUtility.FromJson<RunInfo>(File.ReadAllText(Path.Combine(directory, "run.json")));
                var lease = new RunLease(directory, info, false);
                if (info.status != "complete" || !info.recordingComplete)
                { lease.Dispose(); throw new InvalidDataException("整次錄製尚未成功完成。"); }
                return lease;
            }
        }
        public static void SafePath(string path)
        {
            for (var item = new DirectoryInfo(Path.GetFullPath(path)); item != null; item = item.Parent)
                if (item.Exists && (item.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("拒絕連結路徑：" + item.FullName);
        }
        FileStream Gate()
        {
            SafePath(Root); Directory.CreateDirectory(Root);
            for (int attempt = 0; ; attempt++)
            {
                try { return new FileStream(Path.Combine(Root, ".gate"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
                catch (IOException) when (attempt < 100) { Thread.Sleep(10); }
            }
        }
        public void Adopt()
        {
            using (Gate()) WriteJson(Path.Combine(Root, "current.json"), new BuildIdentity { build = Build, root = Root });
            Cleanup();
        }
        public RunLease Begin(string customDirectory = null)
        {
            using (Gate())
            {
                string id = Guid.NewGuid().ToString("N");
                if (customDirectory != null && Path.GetDirectoryName(Path.GetFullPath(customDirectory)) == Root && Guid.TryParseExact(Path.GetFileName(customDirectory), "N", out _)) id = Path.GetFileName(customDirectory);
                string directory = customDirectory == null ? Path.Combine(Root, id) : Path.GetFullPath(customDirectory);
                SafePath(directory); Directory.CreateDirectory(directory);
                return new RunLease(directory, new RunInfo { run = id, build = Build, startedAt = DateTime.UtcNow.ToString("O") });
            }
        }
        public static void WriteJson(string path, object value)
        {
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(value, true), new System.Text.UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
        }
        static bool TreeSafe(string directory)
        {
            foreach (string path in Directory.GetFileSystemEntries(directory))
            {
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) return false;
                if (Directory.Exists(path)) return false;
                string name = Path.GetFileName(path);
                var known = new[] { ".lease", "run.json", "run.json.tmp", "source.json", "summary.json", "summary.json.tmp", "world.jsonl",
                    "record-source.jsonl", "world-config.json", "scenario.json", "player.log", "Player-0.log", "connection.jsonl", "bridge.json" };
                if (!known.Contains(name) && !System.Text.RegularExpressions.Regex.IsMatch(name, @"^episode-[1-9][0-9]*\.replay\.jsonl(\.partial)?$")) return false;
            }
            return true;
        }
        public void Cleanup()
        {
            using (Gate())
            {
                string active = Path.Combine(Root, "current.json");
                if (!File.Exists(active)) return;
                string current = JsonUtility.FromJson<BuildIdentity>(File.ReadAllText(active)).build;
                var candidates = new System.Collections.Generic.List<RunLease>();
                try
                {
                    foreach (string directory in Directory.GetDirectories(Root))
                    {
                        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) continue;
                        SafePath(directory);
                        string path = Path.Combine(directory, "run.json");
                        if (!File.Exists(path) || !TreeSafe(directory)) continue;
                        RunInfo info;
                        try { info = JsonUtility.FromJson<RunInfo>(File.ReadAllText(path)); }
                        catch (ArgumentException) { continue; }
                        if (info == null || info.owner != "ecosystem-simulation-v1" || info.run != Path.GetFileName(directory) || string.IsNullOrEmpty(info.build)) continue;
                        RunLease lease;
                        try { lease = new RunLease(directory, info, false); }
                        catch (IOException) { continue; }
                        candidates.Add(lease);
                        if (info.status == "running")
                        {
                            info.status = "incomplete"; info.stopReason = "unknown";
                            // 真正停止時刻未知，只記錄發現時刻，不推造正常結束。
                            info.stoppedAt = DateTime.UtcNow.ToString("O"); lease.Save();
                        }
                    }
                    var currentRuns = candidates.Where(run => run.Info.build == current).OrderByDescending(run => run.Info.stoppedAt).ToArray();
                    var recent = currentRuns.Take(5).ToHashSet();
                    var recording = currentRuns.FirstOrDefault(run => run.Info.recordingComplete);
                    foreach (var run in candidates)
                    {
                        bool keep = run.Info.build == current && (recent.Contains(run) || run == recording);
                        if (keep)
                        {
                            if (run != recording && run.Info.recordingComplete)
                            {
                                foreach (string file in Directory.GetFiles(run.DirectoryPath, "episode-*.replay.jsonl")) File.Delete(file);
                                run.Info.recordingComplete = false; run.Save();
                            }
                            continue;
                        }
                        string target = Path.GetFullPath(run.DirectoryPath);
                        if (Path.GetDirectoryName(target) != Root) throw new IOException("清理目標越界。");
                        SafePath(target);
                        run.Dispose();
                        // 刪除途中失敗時保留歸屬，下一次仍可重試。
                        foreach (string file in Directory.GetFiles(target).Where(file => Path.GetFileName(file) != "run.json" && Path.GetFileName(file) != ".lease")) File.Delete(file);
                        File.Delete(Path.Combine(target, ".lease"));
                        File.Delete(Path.Combine(target, "run.json"));
                        Directory.Delete(target);
                    }
                }
                finally { foreach (var run in candidates) run.Dispose(); }
            }
        }
        public sealed class RunLease : IDisposable
        {
            FileStream lease;
            public readonly string DirectoryPath;
            public readonly RunInfo Info;
            public bool Active => lease != null;
            public RunLease(string directory, RunInfo info, bool create = true)
            {
                DirectoryPath = directory; Info = info;
                lease = new FileStream(Path.Combine(directory, ".lease"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                if (create)
                {
                    try
                    {
                        if (new[] { "run.json", "source.json", "summary.json", "world.jsonl", "record-source.jsonl" }.Any(name => File.Exists(Path.Combine(directory, name))) || Directory.GetFiles(directory, "episode-*.replay.jsonl*").Length != 0)
                            throw new IOException("輸出目錄已有執行資料，不覆寫原始來源。");
                        Save();
                    }
                    catch { Dispose(); throw; }
                }
            }
            public void Save() => WriteJson(Path.Combine(DirectoryPath, "run.json"), Info);
            public void Dispose() { lease?.Dispose(); lease = null; }
        }
    }
}
