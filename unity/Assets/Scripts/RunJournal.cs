using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Ecosystem
{
    // 記錄只觀察世界，不修改狀態或重新執行感知。
    public sealed class RunJournal : IDisposable
    {
        [Serializable] public sealed class Population
        {
            public string species;
            public int population, births, deaths, lowSatiety;
            public float satietyMin, satietyMax, satietyMean;
        }
        [Serializable] public sealed class Sample
        {
            public string kind = "sample", reason;
            public int episode, step, population, sources, sourceFruits, carriedFruits, groundFruits;
            public float time;
            public Population[] species;
            public FruitLedger fruitLedger;
        }
        [Serializable] public sealed class EpisodeSummary
        {
            public int episode, minPopulation, maxPopulation, minFruits, maxFruits;
            public float minSatiety, maxSatiety;
            public string reason;
            public Sample start, end;
        }
        [Serializable] public sealed class Summary
        {
            public string source = "source.json", series = "world.jsonl", status = "incomplete", stopReason, error;
            public List<EpisodeSummary> episodes = new List<EpisodeSummary>();
        }
        [Serializable] sealed class Frame { public string kind; public WorldState state; }
        readonly ArtifactStore.RunLease lease;
        readonly string sourceHeader, directory, recordingPath;
        readonly TextWriter series;
        TextWriter recording;
        readonly bool full;
        readonly string build;
        public readonly Summary Result = new Summary();
        public bool Failed { get; private set; }
        public bool Finished { get; private set; }
        public string Error { get; private set; }
        EpisodeSummary current;
        int sampledSecond = -1;

        public RunJournal(ArtifactStore.RunLease lease, string header, string logPath, bool full, string fullPath = null, TextWriter seriesWriter = null)
        {
            this.lease = lease; directory = lease.DirectoryPath; build = lease.Info.build;
            sourceHeader = header; this.full = full;
            recordingPath = fullPath ?? Path.Combine(directory, "record-source.jsonl");
            try
            {
                ArtifactStore.WriteJson(Path.Combine(directory, "summary.json"), Result);
                File.WriteAllText(Path.Combine(directory, "source.json"), header, new UTF8Encoding(false));
                series = seriesWriter ?? Open(logPath);
                series.WriteLine(header); series.Flush();
                if (full) { recording = Open(recordingPath); recording.WriteLine(header); recording.Flush(); }
            }
            catch { series?.Dispose(); recording?.Dispose(); lease.Dispose(); throw; }
        }
        static StreamWriter Open(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            return new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
        }
        public static Sample Capture(WorldState state, string reason = null)
        {
            return new Sample { episode = state.episode, step = state.step, time = state.time, reason = reason,
                population = state.individuals.Count, sources = state.foods.Count,
                sourceFruits = state.fruits.Count(f => f.source != 0), carriedFruits = state.fruits.Count(f => f.holder != 0),
                groundFruits = state.fruits.Count(f => f.source == 0 && f.holder == 0),
                fruitLedger = JsonUtility.FromJson<FruitLedger>(JsonUtility.ToJson(state.fruitLedger)),
                species = World.Species.Select(species => {
                    var members = state.individuals.Where(a => a.species == species).ToArray();
                    var statistics = state.statistics.FirstOrDefault(s => s.species == species);
                    return new Population { species = species, population = members.Length, births = statistics?.births ?? 0,
                        deaths = state.deaths.Count(d => d.species == species), lowSatiety = members.Count(a => a.satiety <= 45),
                        satietyMin = members.Length == 0 ? 0 : members.Min(a => a.satiety),
                        satietyMax = members.Length == 0 ? 0 : members.Max(a => a.satiety),
                        satietyMean = members.Length == 0 ? 0 : members.Average(a => a.satiety) };
                }).ToArray() };
        }
        void WriteSample(Sample sample, string kind)
        {
            sample.kind = kind; series.WriteLine(JsonUtility.ToJson(sample)); series.Flush();
        }
        public void Observe(string kind, WorldState state)
        {
            if (Failed || Finished) return;
            try
            {
                if (kind == "reset")
                {
                    if (current != null && current.reason == null) End(state, "unknown");
                    var sample = Capture(state);
                    current = new EpisodeSummary { episode = state.episode, start = sample,
                        minPopulation = sample.population, maxPopulation = sample.population,
                        minFruits = state.fruits.Count, maxFruits = state.fruits.Count,
                        minSatiety = sample.species.Min(s => s.satietyMin), maxSatiety = sample.species.Max(s => s.satietyMax) };
                    Result.episodes.Add(current); sampledSecond = 0;
                    WriteSample(sample, "episode_start");
                }
                if (current != null && (kind == "step" || kind == "reset"))
                {
                    var sample = Capture(state); current.end = sample;
                    current.minPopulation = Math.Min(current.minPopulation, sample.population); current.maxPopulation = Math.Max(current.maxPopulation, sample.population);
                    current.minFruits = Math.Min(current.minFruits, state.fruits.Count); current.maxFruits = Math.Max(current.maxFruits, state.fruits.Count);
                    current.minSatiety = Math.Min(current.minSatiety, sample.species.Min(s => s.satietyMin));
                    current.maxSatiety = Math.Max(current.maxSatiety, sample.species.Max(s => s.satietyMax));
                    int second = (int)Math.Floor(state.time + .001f);
                    if (kind == "step" && second > sampledSecond) { WriteSample(sample, "sample"); sampledSecond = second; }
                }
                if (kind == "episode_end") End(state, state.endReason);
                if (full && (kind == "reset" || kind == "step" || kind == "episode_end"))
                {
                    recording.WriteLine(JsonUtility.ToJson(new Frame { kind = kind, state = state })); recording.Flush();
                }
            }
            catch (Exception error) { Fail(error); }
        }
        void End(WorldState state, string reason)
        {
            if (current == null || current.reason != null) return;
            current.reason = reason; current.end = Capture(state, reason); WriteSample(current.end, "episode_end");
        }
        public void WriteDiagnostic(string line)
        {
            if (Failed || Finished) return;
            try { series.WriteLine(line); series.Flush(); } catch (Exception error) { Fail(error); }
        }
        void Fail(Exception error)
        {
            Failed = true; Error = error.Message; Result.error = Error; Result.status = "incomplete";
            lease.Info.error = Error; lease.Info.status = "incomplete"; lease.Info.recordingComplete = false;
            Debug.LogError("資料記錄失敗，世界繼續：" + Error);
            try { lease.Save(); ArtifactStore.WriteJson(Path.Combine(directory, "summary.json"), Result); } catch (Exception reportError) { Debug.LogError(reportError.Message); }
        }
        public void Finish(WorldState state, string reason)
        {
            if (Finished) return;
            try
            {
                if (!Failed)
                {
                    End(state, reason);
                    if (full) recording.WriteLine(JsonUtility.ToJson(new Frame { kind = "observation_end", state = state }));
                }
                series.Dispose(); recording?.Dispose(); recording = null;
                if (!Failed && full) Pack();
                Result.status = Failed ? "incomplete" : "complete"; Result.stopReason = reason;
                ArtifactStore.WriteJson(Path.Combine(directory, "summary.json"), Result);
                lease.Info.status = Result.status; lease.Info.stopReason = reason;
                lease.Info.stoppedAt = DateTime.UtcNow.ToString("O");
                lease.Info.recordingComplete = !Failed && full;
                lease.Info.segments = full ? Result.episodes.Count : 0;
                lease.Save();
                if (!Failed && full) File.Delete(recordingPath);
            }
            catch (Exception error) { Fail(error); }
            finally
            {
                if (Failed)
                {
                    lease.Info.stoppedAt = DateTime.UtcNow.ToString("O"); lease.Info.stopReason = reason;
                    try { lease.Save(); } catch (Exception error) { Debug.LogError(error.Message); }
                }
                Finished = true; lease.Dispose();
            }
        }
        void Pack()
        {
            if (Result.episodes.Count == 0) throw new InvalidDataException("錄製沒有有效輪次。");
            var expected = Result.episodes.Select(e => e.episode).ToArray();
            int segment = -1, count = 0;
            StreamWriter writer = null;
            SHA256 hash = null;
            string temporary = null, destination = null;
            void Write(string text)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text + "\n"); hash.TransformBlock(bytes, 0, bytes.Length, bytes, 0); writer.WriteLine(text);
            }
            void CloseSegment()
            {
                if (writer == null) return;
                if (count == 0) throw new InvalidDataException("錄製缺少初始狀態。");
                hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                writer.WriteLine("{\"kind\":\"complete\",\"frames\":" + count + ",\"sha256\":\"" + BitConverter.ToString(hash.Hash).Replace("-", "").ToLowerInvariant() + "\"}");
                writer.Dispose(); writer = null; hash.Dispose(); hash = null;
                using (var replay = new WorldReplay(temporary, build)) { if (replay.Count != count) throw new InvalidDataException("分段驗證失敗。"); }
                File.Move(temporary, destination);
            }
            try
            {
                foreach (string line in File.ReadLines(recordingPath).Skip(1))
                {
                    var frame = JsonUtility.FromJson<Frame>(line);
                    if (frame.kind == "reset")
                    {
                        CloseSegment(); segment++;
                        if (segment >= expected.Length || frame.state.episode != expected[segment]) throw new InvalidDataException("輪次不連續。");
                        destination = Path.Combine(directory, "episode-" + expected[segment] + ".replay.jsonl"); temporary = destination + ".partial";
                        writer = new StreamWriter(new FileStream(temporary, FileMode.CreateNew), new UTF8Encoding(false)) { NewLine = "\n" };
                        hash = SHA256.Create(); count = 0;
                        string header = sourceHeader.Substring(0, sourceHeader.Length - 1).Replace("\"kind\":\"run\"", "\"kind\":\"replay\"");
                        Write(header + ",\"schemaVersion\":" + WorldRules.ReplaySchema + ",\"episode\":" + expected[segment] + ",\"stopReason\":\"" + Result.episodes[segment].reason + "\"}");
                    }
                    if (frame.kind != "reset" && frame.kind != "step") continue;
                    if (writer == null || frame.state.episode != expected[segment] || frame.state.step != count) throw new InvalidDataException("錄製影格缺漏。");
                    Write(JsonUtility.ToJson(new Frame { kind = "frame", state = frame.state })); count++;
                }
                CloseSegment();
                if (segment + 1 != expected.Length) throw new InvalidDataException("錄製缺少必要分段。");
            }
            finally { writer?.Dispose(); hash?.Dispose(); }
        }
        public void Dispose() { series?.Dispose(); recording?.Dispose(); lease.Dispose(); }
    }
}
