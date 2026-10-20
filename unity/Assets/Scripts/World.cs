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
        public bool foraging;
        public float foodCapacity = 30f;
        public float foodInitial = 20f;
        public float foodRegeneration = 0.4f;
        public float forageRadius = 0.7f;
        public float forageRate = 12f;
        public float initialEnergy = 60f;
        public float idleCost = 0.5f;
        public float movementCost = 1.2f;
        public float starvationDamage = 6f;

        public void Validate()
        {
            if (!float.IsFinite(halfExtent) || halfExtent <= 0 ||
                !float.IsFinite(speed) || speed <= 0 ||
                !float.IsFinite(stepSeconds) || stepSeconds <= 0 ||
                !float.IsFinite(observationRadius) || observationRadius <= 0 || episodeSteps <= 0 ||
                individualsPerSpecies < 1 || (!foraging && (individualsPerSpecies < 2 || removeStep <= 0 || addStep <= removeStep || addStep >= episodeSteps)))
                throw new ArgumentException("世界配置須為有限正數、每物種至少兩隻，且 0 < removeStep < addStep < episodeSteps。");
            if (foraging && (!float.IsFinite(foodCapacity) || foodCapacity <= 0 ||
                !float.IsFinite(foodInitial) || foodInitial < 0 || foodInitial > foodCapacity ||
                !float.IsFinite(foodRegeneration) || foodRegeneration < 0 ||
                !float.IsFinite(forageRadius) || forageRadius <= 0 ||
                !float.IsFinite(forageRate) || forageRate <= 0 ||
                !float.IsFinite(initialEnergy) || initialEnergy < 0 || initialEnergy > 100 ||
                !float.IsFinite(idleCost) || idleCost < 0 ||
                !float.IsFinite(movementCost) || movementCost < 0 ||
                !float.IsFinite(starvationDamage) || starvationDamage <= 0))
                throw new ArgumentException("食物、採食與生存參數無效。");
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
        public float health;
        public float energy;
    }

    [Serializable]
    public sealed class FoodState
    {
        public int food;
        public bool patch;
        public float x;
        public float y;
        public float amount;
        public float capacity;
    }

    [Serializable]
    public sealed class WorldEvent
    {
        public string kind;
        public int individual;
        public int food;
        public float amount;
    }

    public readonly struct WorldAction
    {
        public readonly Vector2 movement;
        public readonly bool forage;
        public WorldAction(Vector2 movement, bool forage = false)
        {
            this.movement = movement;
            this.forage = forage;
        }
    }

    [Serializable]
    public sealed class WorldState
    {
        public List<FoodState> foods = new List<FoodState>();
        public List<WorldEvent> events = new List<WorldEvent>();
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
            if (Config.foraging)
            {
                for (int i = 0; i < 16; i++)
                {
                    bool patch = i < 4;
                    float scale = patch ? 1f : 0.25f;
                    State.foods.Add(new FoodState
                    {
                        food = i + 1, patch = patch,
                        x = patch ? (i % 2 == 0 ? -1 : 1) * Config.halfExtent * 0.45f
                            : (float)(random.NextDouble() * 2 - 1) * Config.halfExtent * 0.9f,
                        y = patch ? (i < 2 ? -1 : 1) * Config.halfExtent * 0.45f
                            : (float)(random.NextDouble() * 2 - 1) * Config.halfExtent * 0.9f,
                        capacity = Config.foodCapacity * scale, amount = Config.foodInitial * scale
                    });
                }
            }
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
                health = species == "Flame" ? 120f : species == "Frost" ? 70f : 90f,
                energy = Config.initialEnergy,
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
            var commands = new Dictionary<int, WorldAction>();
            foreach (var pair in actions)
                commands.Add(pair.Key, new WorldAction(pair.Value));
            return Step(commands);
        }

        public WorldState Step(IReadOnlyDictionary<int, WorldAction> actions)
        {
            if (State == null || State.endReason != "")
                throw new InvalidOperationException("請先重建世界。");
            foreach (var individual in State.individuals)
                if (!actions.TryGetValue(individual.individual, out var action) ||
                    !float.IsFinite(action.movement.x) || !float.IsFinite(action.movement.y))
                    throw new ArgumentException("每隻個體都必須取得有限數值的動作。");
            State.events.Clear();
            foreach (var food in State.foods)
                food.amount = Mathf.Min(food.capacity, food.amount + Config.foodRegeneration * Config.stepSeconds * (food.patch ? 1f : 0.25f));
            foreach (var individual in State.individuals)
            {
                float speed = Config.foraging ? (individual.species == "Frost" ? 3.2f : individual.species == "Moss" ? 2.7f : 2.5f) : Config.speed;
                var movement = Vector2.ClampMagnitude(actions[individual.individual].movement, 1f) * (speed * Config.stepSeconds);
                var position = new Vector2(individual.x, individual.y) + movement;
                var bounded = new Vector2(Mathf.Clamp(position.x, -Config.halfExtent, Config.halfExtent),
                    Mathf.Clamp(position.y, -Config.halfExtent, Config.halfExtent));
                individual.x = bounded.x;
                individual.y = bounded.y;
                individual.boundary = position != bounded;
                if (!Config.foraging)
                    continue;
                float distance = Vector2.Distance(new Vector2(individual.x, individual.y), position - movement);
                individual.energy = Mathf.Max(0, individual.energy - Config.idleCost * Config.stepSeconds - Config.movementCost * distance);
                if (actions[individual.individual].forage)
                    foreach (var food in State.foods)
                        if (Vector2.Distance(bounded, new Vector2(food.x, food.y)) <= Config.forageRadius)
                        {
                            float eaten = Mathf.Min(food.amount, Config.forageRate * Config.stepSeconds, 100f - individual.energy);
                            if (eaten <= 0) continue;
                            food.amount -= eaten;
                            individual.energy += eaten;
                            State.events.Add(new WorldEvent { kind = "ate", individual = individual.individual, food = food.food, amount = eaten });
                            break;
                        }
                if (individual.energy <= 0)
                    individual.health = Mathf.Max(0, individual.health - Config.starvationDamage * Config.stepSeconds);
                if (individual.health <= 0)
                    State.events.Add(new WorldEvent { kind = "starved", individual = individual.individual });
            }
            if (Config.foraging) State.individuals.RemoveAll(value => value.health <= 0);
            State.step++;
            State.time = State.step * Config.stepSeconds;
            State.endReason = State.individuals.Count == 0 ? "all_dead" : State.step >= Config.episodeSteps ? "time_limit" : "";
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
