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
            public string rules;
            public WorldConfig config;
            public int seed;
            public int episode;
            public WorldState state;
            public int frames;
            public string sha256;
        }

        private readonly FileStream source;
        private readonly List<long> offsets = new List<long>();
        public Record Header { get; }
        public int Count => offsets.Count;
        public long Bytes => source.Length;
        public int Index { get; private set; }

        public WorldReplay(string path)
        {
            source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            try
            {
                using var reader = new StreamReader(source, new UTF8Encoding(false, true), false, 65536, true);
                using var hash = SHA256.Create();
                string line = reader.ReadLine() ?? throw new InvalidDataException("回放是空檔案。");
                Header = JsonUtility.FromJson<Record>(line);
                if (Header.kind != "replay" || Header.schemaVersion != 1 || (Header.rules != "survival-v9" && Header.rules != "survival-v10" && Header.rules != "survival-v11" && Header.rules != "survival-v12" && Header.rules != "survival-v13" && Header.rules != "survival-v14") || Header.config == null || !Header.config.foraging)
                    throw new InvalidDataException("回放版本或世界設定不相容。");
                // 舊規則的幼體能力與成體相同，回放檢視不得套用新的成長上限。
                if (Header.rules == "survival-v9") Header.config.newbornGrowth = 1f;
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
                        if (record.frames != Count || Count < 2 || record.sha256 != checksum || reader.ReadLine() != null || position + bytes.Length != source.Length)
                            throw new InvalidDataException("回放完整性驗證失敗。");
                        complete = true;
                        break;
                    }
                    if (record.kind == "frame")
                    {
                        var state = record.state;
                        if (state == null || state.episode != Header.episode || state.step != Count || !float.IsFinite(state.time))
                            throw new InvalidDataException("回放影格缺漏或順序錯誤。");
                        offsets.Add(position);
                    }
                    else if (position != 0) throw new InvalidDataException("回放包含未知記錄。");
                    hash.TransformBlock(bytes, 0, bytes.Length, bytes, 0);
                    position += bytes.Length;
                    line = reader.ReadLine();
                }
                if (!complete) throw new InvalidDataException("回放缺少完整結尾。");
            }
            catch { source.Dispose(); throw; }
        }

        public WorldState Read(int index)
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            Index = index;
            source.Seek(offsets[index], SeekOrigin.Begin);
            using var reader = new StreamReader(source, Encoding.UTF8, false, 65536, true);
            var state = JsonUtility.FromJson<Record>(reader.ReadLine()).state;
            foreach (var individual in state.individuals)
            {
                individual.smokeKnowledge ??= new List<SmokeSighting>();
                if (Header.rules != "survival-v14")
                {
                    individual.smokeKnowledgeAvailable = false;
                    individual.smokeKnowledge.Clear();
                }
            }
            return state;
        }

        public void Dispose() => source.Dispose();
    }
}
