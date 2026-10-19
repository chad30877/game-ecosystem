using System;
using UnityEngine;

namespace Ecosystem
{
    [Serializable]
    public sealed class WorldConfig
    {
        public float halfExtent = 5f;
        public float speed = 2.5f;
        public float stepSeconds = 0.02f;
        public float observationRadius = 2f;
        public int episodeSteps = 256;

        public void Validate()
        {
            if (!float.IsFinite(halfExtent) || halfExtent <= 0 ||
                !float.IsFinite(speed) || speed <= 0 ||
                !float.IsFinite(stepSeconds) || stepSeconds <= 0 ||
                !float.IsFinite(observationRadius) || observationRadius <= 0 || episodeSteps <= 0)
                throw new ArgumentException("世界配置必須為有限正數。");
        }
    }

    [Serializable]
    public sealed class WorldState
    {
        public int episode;
        public int step;
        public float time;
        public float x;
        public float y;
        public bool boundary;
        public string endReason;
    }

    public sealed class World
    {
        public WorldConfig Config { get; }
        public WorldState State { get; private set; }
        private readonly System.Random random;
        private int episode;

        public World(WorldConfig config, int seed)
        {
            config.Validate();
            Config = config;
            random = new System.Random(seed);
        }

        public WorldState Reset()
        {
            State = new WorldState
            {
                episode = ++episode,
                x = (float)(random.NextDouble() - 0.5) * Config.halfExtent,
                y = (float)(random.NextDouble() - 0.5) * Config.halfExtent,
                endReason = ""
            };
            return State;
        }

        public WorldState Step(Vector2 action)
        {
            if (State == null || State.endReason != "")
                throw new InvalidOperationException("請先重建世界。");
            if (!float.IsFinite(action.x) || !float.IsFinite(action.y))
                throw new ArgumentException("動作必須為有限數值。");
            var movement = Vector2.ClampMagnitude(action, 1f) * (Config.speed * Config.stepSeconds);
            var position = new Vector2(State.x, State.y) + movement;
            var bounded = new Vector2(Mathf.Clamp(position.x, -Config.halfExtent, Config.halfExtent),
                Mathf.Clamp(position.y, -Config.halfExtent, Config.halfExtent));
            State = new WorldState
            {
                episode = episode,
                step = State.step + 1,
                time = (State.step + 1) * Config.stepSeconds,
                x = bounded.x,
                y = bounded.y,
                boundary = position != bounded,
                endReason = State.step + 1 >= Config.episodeSteps ? "time_limit" : ""
            };
            return State;
        }

        public float[] Observe()
        {
            float radius = Config.observationRadius;
            return new[]
            {
                Mathf.Clamp01((Config.halfExtent - State.x) / radius),
                Mathf.Clamp01((Config.halfExtent + State.x) / radius),
                Mathf.Clamp01((Config.halfExtent - State.y) / radius),
                Mathf.Clamp01((Config.halfExtent + State.y) / radius)
            };
        }
    }
}
