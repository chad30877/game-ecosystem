using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Ecosystem
{
    public sealed class WorldReplay : IDisposable
    {
        [Serializable]
        public sealed class Record
        {
            public string kind;
            public int schemaVersion;
            public int actionVersion;
            public string rules;
            public string build, stopReason;
            public WorldConfig config;
            public int seed;
            public int episode;
            public WorldState state;
            public int frames;
            public string sha256;
        }

        private readonly FileStream source;
        private ArtifactStore.RunLease lease;
        private readonly List<long> offsets = new List<long>();
        public Record Header { get; }
        public int Count => offsets.Count;
        public long Bytes => source.Length;
        public int Index { get; private set; }

        public WorldReplay(string path, string expectedBuild = null)
        {
            expectedBuild ??= ArtifactStore.Identity.build;
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            string runPath = Path.Combine(directory, "run.json");
            if (File.Exists(runPath) && !path.EndsWith(".partial", StringComparison.Ordinal))
            {
                var info = JsonUtility.FromJson<ArtifactStore.RunInfo>(File.ReadAllText(runPath));
                lease = new ArtifactStore.RunLease(directory, info, false);
                if (info.status != "complete" || !info.recordingComplete) { lease.Dispose(); throw new InvalidDataException("整次錄製尚未成功完成。"); }
            }
            try { source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read); }
            catch { lease?.Dispose(); throw; }
            try
            {
                using var reader = new StreamReader(source, new UTF8Encoding(false, true), false, 65536, true);
                using var hash = SHA256.Create();
                string line = reader.ReadLine() ?? throw new InvalidDataException("回放是空檔案。");
                Header = JsonUtility.FromJson<Record>(line);
                if (string.IsNullOrEmpty(Header.build) || Header.build != expectedBuild)
                    throw new InvalidDataException("回放建置來源未知或與播放器不同。");
                if (Header.kind != "replay" || !Compatible(Header.rules, Header.schemaVersion) || Header.actionVersion != WorldRules.ActionVersion || Header.config == null)
                    throw new InvalidDataException("回放版本或世界設定不相容。");
                Header.config.Validate();
                long position = 0;
                bool complete = false;
                while (line != null)
                {
                    var record = JsonUtility.FromJson<Record>(line);
                    byte[] bytes = Encoding.UTF8.GetBytes(line + "\n");
                    if (record.kind == "complete")
                    {
                        hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                        string checksum = BitConverter.ToString(hash.Hash).Replace("-", "").ToLowerInvariant();
                        if (record.frames != Count || Count < 1 || record.sha256 != checksum || reader.ReadLine() != null || position + bytes.Length != source.Length)
                            throw new InvalidDataException("回放完整性驗證失敗。");
                        complete = true;
                        break;
                    }
                    if (record.kind == "frame")
                    {
                        var state = record.state;
                        if (state == null || state.episode != Header.episode || state.step != Count || !float.IsFinite(state.time))
                            throw new InvalidDataException("回放影格缺漏或順序錯誤。");
                        if (Header.config.foraging && !state.fruitInformationAvailable)
                            throw new InvalidDataException("新規則快照缺少果實資訊。");
                        offsets.Add(position);
                    }
                    else if (position != 0) throw new InvalidDataException("回放包含未知記錄。");
                    hash.TransformBlock(bytes, 0, bytes.Length, bytes, 0);
                    position += bytes.Length;
                    line = reader.ReadLine();
                }
                if (!complete) throw new InvalidDataException("回放缺少完整結尾。");
            }
            catch { source.Dispose(); lease?.Dispose(); throw; }
        }

        public WorldState Read(int index)
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            Index = index;
            source.Seek(offsets[index], SeekOrigin.Begin);
            using var reader = new StreamReader(source, Encoding.UTF8, false, 65536, true);
            var state = JsonUtility.FromJson<Record>(reader.ReadLine()).state;
            return state;
        }

        public static bool Compatible(string rules, int schema) => rules == WorldRules.Current && schema == WorldRules.ReplaySchema;

        public void Dispose() { source.Dispose(); lease?.Dispose(); }
    }
}
