using System;
using System.Collections.Generic;
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
        public int individualsPerSpecies = 2;
        public int removeStep = 64;
        public int addStep = 96;

        public void Validate()
        {
            if (!float.IsFinite(halfExtent) || halfExtent <= 0 ||
                !float.IsFinite(speed) || speed <= 0 ||
                !float.IsFinite(stepSeconds) || stepSeconds <= 0 ||
                !float.IsFinite(observationRadius) || observationRadius <= 0 || episodeSteps <= 0 ||
                individualsPerSpecies < 2 || removeStep <= 0 || addStep <= removeStep || addStep >= episodeSteps)
                throw new ArgumentException("世界配置須為有限正數、每物種至少兩隻，且 0 < removeStep < addStep < episodeSteps。");
        }
    }

    [Serializable]
    public sealed class IndividualState
    {
        public int individual;
        public string species;
        public float x;
        public float y;
        public bool boundary;
    }

    [Serializable]
    public sealed class WorldState
    {
        public int episode;
        public int step;
        public float time;
        public string endReason = "";
        public List<IndividualState> individuals = new List<IndividualState>();
    }

    public sealed class World
    {
        public static readonly IReadOnlyList<string> Species = Array.AsReadOnly(new[] { "Flame", "Frost", "Moss" });
        public WorldConfig Config { get; }
        public WorldState State { get; private set; }
        private readonly System.Random random;
        private int episode;
        private int nextIndividual;

        public World(WorldConfig config, int seed)
        {
            config.Validate();
            Config = config;
            random = new System.Random(seed);
        }

        public WorldState Reset()
        {
            State = new WorldState { episode = ++episode };
            foreach (string species in Species)
                for (int i = 0; i < Config.individualsPerSpecies; i++)
                    AddIndividual(species);
            return State;
        }

        public IndividualState AddIndividual(string species)
        {
            bool known = false;
            foreach (string value in Species)
                known |= value == species;
            if (!known)
                throw new ArgumentException("未知物種。");
            var individual = new IndividualState
            {
                individual = ++nextIndividual, species = species,
                x = (float)(random.NextDouble() - 0.5) * Config.halfExtent,
                y = (float)(random.NextDouble() - 0.5) * Config.halfExtent
            };
            State.individuals.Add(individual);
            return individual;
        }

        public void RemoveIndividual(int individual)
        {
            if (State.individuals.RemoveAll(value => value.individual == individual) != 1)
                throw new ArgumentException("個體不存在。");
        }

        public WorldState Step(IReadOnlyDictionary<int, Vector2> actions)
        {
            if (State == null || State.endReason != "")
                throw new InvalidOperationException("請先重建世界。");
            foreach (var individual in State.individuals)
                if (!actions.TryGetValue(individual.individual, out var action) ||
                    !float.IsFinite(action.x) || !float.IsFinite(action.y))
                    throw new ArgumentException("每隻個體都必須取得有限數值的動作。");
            foreach (var individual in State.individuals)
            {
                var movement = Vector2.ClampMagnitude(actions[individual.individual], 1f) * (Config.speed * Config.stepSeconds);
                var position = new Vector2(individual.x, individual.y) + movement;
                var bounded = new Vector2(Mathf.Clamp(position.x, -Config.halfExtent, Config.halfExtent),
                    Mathf.Clamp(position.y, -Config.halfExtent, Config.halfExtent));
                individual.x = bounded.x;
                individual.y = bounded.y;
                individual.boundary = position != bounded;
            }
            State.step++;
            State.time = State.step * Config.stepSeconds;
            State.endReason = State.step >= Config.episodeSteps ? "time_limit" : "";
            return State;
        }

        public float[] Observe(IndividualState individual)
        {
            float radius = Config.observationRadius;
            return new[]
            {
                Mathf.Clamp01((Config.halfExtent - individual.x) / radius),
                Mathf.Clamp01((Config.halfExtent + individual.x) / radius),
                Mathf.Clamp01((Config.halfExtent - individual.y) / radius),
                Mathf.Clamp01((Config.halfExtent + individual.y) / radius)
            };
        }
    }
}
