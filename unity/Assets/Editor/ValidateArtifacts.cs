using System;
using System.IO;
using System.Linq;
using Ecosystem;
using UnityEngine;

namespace Ecosystem.Editor
{
    public static class ValidateArtifacts
    {
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        public static void Identity()
        {
            string root = Path.GetFullPath("../artifacts/recording-validation/identity-" + Guid.NewGuid().ToString("N"));
            var store = new ArtifactStore(root, "build-a");
            store.Adopt();
            using (var first = store.Begin())
            using (var second = store.Begin())
            {
                Require(first.Info.run != second.Info.run, "每次啟動須有獨立識別");
                Require(first.Info.build == "build-a" && first.Active, "執行來源及跨程序租約");
                var next = new ArtifactStore(root, "build-b"); next.Adopt();
                Require(Directory.Exists(first.DirectoryPath), "採用新建置不得刪除活躍執行");
            }
            new ArtifactStore(root, "build-b").Cleanup();
            Require(Directory.GetDirectories(root).Length == 0, "舊建置結束後可淘汰");
            Debug.Log("產物建置與執行識別驗收通過");
        }
        public static void Recording()
        {
            string root = Path.GetFullPath("../artifacts/recording-validation/session-" + Guid.NewGuid().ToString("N"));
            var store = new ArtifactStore(root, "recording-test"); store.Adopt();
            var lease = store.Begin(); string directory = lease.DirectoryPath;
            var config = new WorldConfig { foraging = true, halfExtent = 20, naturalFoodLimit = 0, disableReproduction = true, disableAging = true };
            var world = new World(config, 101); world.Reset();
            string header = "{\"kind\":\"run\",\"build\":\"recording-test\",\"rules\":\"survival-v15\",\"actionVersion\":2,\"config\":" + JsonUtility.ToJson(config) + ",\"seed\":101}";
            using (var journal = new RunJournal(lease, header, Path.Combine(directory, "world.jsonl"), true))
            {
                journal.Observe("reset", world.State);
                for (int i = 0; i < 60; i++) { world.Step(world.State.individuals.ToDictionary(a => a.individual, a => new WorldAction(Vector2.zero))); journal.Observe("step", world.State); }
                world.State.endReason = "manual_reset"; journal.Observe("episode_end", world.State);
                world.Reset(); journal.Observe("reset", world.State);
                journal.Finish(world.State, "user_stop");
                Require(!journal.Failed && journal.Result.episodes.Count == 2, "跨輪提前停止須完整封裝");
            }
            Require(Directory.GetFiles(directory, "episode-*.replay.jsonl").Length == 2 && !File.Exists(Path.Combine(directory, "record-source.jsonl")), "成功錄製保留全部分段並移除冗餘來源");
            using (var replay = new WorldReplay(Path.Combine(directory, "episode-1.replay.jsonl"), "recording-test"))
            {
                string first = JsonUtility.ToJson(replay.Read(0)); replay.Read(replay.Count - 1);
                Require(JsonUtility.ToJson(replay.Read(0)) == first && replay.Count == 61, "前後跳轉不重跑世界");
            }
            string series = File.ReadAllText(Path.Combine(directory, "world.jsonl"));
            Require(!series.Contains("\"individuals\"") && File.ReadAllLines(Path.Combine(directory, "world.jsonl")).Length == 6, "輕量資料只有來源、輪次起訖及一秒樣本");
            bool rejected = false;
            try { using var wrong = new WorldReplay(Path.Combine(directory, "episode-1.replay.jsonl"), "different-build"); } catch (InvalidDataException) { rejected = true; }
            Require(rejected, "不同建置須拒絕");
            var lightweight = new World(config, 101); lightweight.Reset();
            for (int i = 0; i < 60; i++) lightweight.Step(lightweight.State.individuals.ToDictionary(a => a.individual, a => new WorldAction(Vector2.zero)));
            using (var replay = new WorldReplay(Path.Combine(directory, "episode-1.replay.jsonl"), "recording-test"))
                Require(JsonUtility.ToJson(replay.Read(60)) == JsonUtility.ToJson(lightweight.State), "記錄不得改變世界結果");
            string damaged = Path.Combine(root, "damaged.replay.jsonl");
            File.WriteAllText(damaged, File.ReadAllText(Path.Combine(directory, "episode-1.replay.jsonl")).Replace("\"step\":1,", "\"step\":2,"));
            rejected = false; try { using var invalid = new WorldReplay(damaged, "recording-test"); } catch (InvalidDataException) { rejected = true; }
            Require(rejected, "損壞或缺漏影格須拒絕");
            Debug.Log("輕量記錄與全次回放驗收通過");
        }
        sealed class FailingWriter : StringWriter
        {
            public bool Broken;
            public override void WriteLine(string value) { if (Broken) throw new IOException("驗收注入寫入失敗"); base.WriteLine(value); }
        }
        public static void Failures()
        {
            var store = new ArtifactStore(Path.GetFullPath("../artifacts/recording-validation/failure-" + Guid.NewGuid().ToString("N")), "failure-test"); store.Adopt();
            var world = new World(new WorldConfig { foraging = true, halfExtent = 20 }, 101); world.Reset();
            var lease = store.Begin(); var writer = new FailingWriter();
            using var journal = new RunJournal(lease, "{}", Path.Combine(lease.DirectoryPath, "world.jsonl"), false, seriesWriter: writer);
            journal.Observe("reset", world.State); writer.Broken = true;
            for (int i = 0; i < 100; i++) { world.Step(world.State.individuals.ToDictionary(a => a.individual, a => new WorldAction(Vector2.zero))); journal.Observe("step", world.State); }
            Require(journal.Failed && world.State.step == 100, "記錄失敗後世界仍須推進");
            journal.Finish(world.State, "step_limit");
            Require(lease.Info.status == "incomplete" && !lease.Info.recordingComplete, "失敗不能產生成功標記");
            bool rejected = false;
            var other = store.Begin();
            try { using var invalid = new RunJournal(other, "{}", other.DirectoryPath, false); } catch (IOException) { rejected = true; } catch (UnauthorizedAccessException) { rejected = true; }
            Require(rejected, "初始化無法寫入須拒絕啟動");
            Debug.Log("記錄失敗隔離驗收通過");
        }
        public static void Retention()
        {
            var store = new ArtifactStore(Path.GetFullPath("../artifacts/recording-validation/retention-" + Guid.NewGuid().ToString("N")), "recording-test"); store.Adopt();
            var world = new World(new WorldConfig { foraging = true, halfExtent = 20 }, 101); world.Reset();
            string first = null;
            for (int i = 0; i < 8; i++)
            {
                var run = store.Begin();
                using (var journal = new RunJournal(run, "{\"kind\":\"run\",\"build\":\"recording-test\",\"rules\":\"survival-v15\",\"actionVersion\":2,\"config\":" + JsonUtility.ToJson(world.Config) + "}", Path.Combine(run.DirectoryPath, "world.jsonl"), i == 0))
                { journal.Observe("reset", world.State); journal.Finish(world.State, "user_stop"); Require(!journal.Failed, "保留測試來源有效"); }
                if (i == 0) first = run.DirectoryPath;
            }
            store.Cleanup();
            Require(Directory.GetDirectories(store.Root).Length == 6 && File.Exists(Path.Combine(first, "source.json")), "五次輕量及五次外的成功錄製須保留必要資訊");
            var failedRun = store.Begin();
            using (var journal = new RunJournal(failedRun, "{\"kind\":\"run\",\"build\":\"recording-test\",\"rules\":\"survival-v15\",\"actionVersion\":2,\"config\":" + JsonUtility.ToJson(world.Config) + "}", Path.Combine(failedRun.DirectoryPath, "world.jsonl"), true))
            {
                journal.Observe("reset", world.State); world.State.step = 2; journal.Observe("step", world.State); journal.Finish(world.State, "user_stop");
                Require(journal.Failed && !failedRun.Info.recordingComplete && File.Exists(Path.Combine(failedRun.DirectoryPath, "record-source.jsonl")), "缺漏影格須保留失敗來源");
            }
            store.Cleanup();
            Require(Directory.Exists(first) && Directory.Exists(failedRun.DirectoryPath), "失敗場次納入五次且不得替換成功錄製");
            using (var playback = new WorldReplay(Path.Combine(first, "episode-1.replay.jsonl"), "recording-test"))
            {
                new ArtifactStore(store.Root, "next-build").Adopt();
                Require(Directory.Exists(first), "播放保護須跨建置");
            }
            string unknown = Path.Combine(store.Root, "unknown"); Directory.CreateDirectory(unknown); File.WriteAllText(Path.Combine(unknown, "data"), "保留");
            store.Cleanup();
            Require(Directory.GetDirectories(store.Root).Length == 1 && File.Exists(Path.Combine(unknown, "data")), "只淘汰有可靠歸屬的舊資料");
            Debug.Log("五次／一次與播放保護驗收通過");
        }
        public static void All() { Identity(); Recording(); Failures(); Retention(); ValidateFruitReplay.RunContracts(); ValidateFruitReplay.RunScenarioGeometry(); ValidateFruitReplay.RunInputBoundaries(); ValidateProductionAI.RunAll(); }
    }
}
