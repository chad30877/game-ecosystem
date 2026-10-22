using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ecosystem
{
    [Serializable]
    public sealed class FoodGrowthConfig
    {
        public float capacity;
        public float growthRate;
        public float restSeconds;
        public float lifetimeSeconds;
        public float recoveryFraction = 0.25f;

        public void Validate()
        {
            if (!float.IsFinite(capacity) || capacity <= 0 || !float.IsFinite(growthRate) || growthRate <= 0 ||
                !float.IsFinite(restSeconds) || restSeconds <= 0 || !float.IsFinite(lifetimeSeconds) || lifetimeSeconds <= 0 ||
                !float.IsFinite(recoveryFraction) || recoveryFraction <= 0 || recoveryFraction > 1)
                throw new ArgumentException("食物生長類型參數無效。");
        }
    }

    public enum FoodPhase { Available, Resting, Recovering }
    public enum FoodGrowthType { Small, Large }

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
        public FoodGrowthConfig smallFood = new FoodGrowthConfig { capacity = 15f, growthRate = 0.75f, restSeconds = 10f, lifetimeSeconds = 120f };
        public FoodGrowthConfig largeFood = new FoodGrowthConfig { capacity = 30f, growthRate = 1.5f, restSeconds = 25f, lifetimeSeconds = 240f };
        public float foodInitialFraction = 0.67f;
        public int foodSourceLimit = 64;
        public int naturalFoodLimit = 16;
        public float naturalSpawnInterval = 10f;
        public int naturalSpawnBatch = 2;
        public float naturalFoodSpacing = 1f;
        public float newFoodFraction = 0.05f;
        public float forageRadius = 0.7f;
        public float forageRate = 12f;
        public float initialEnergy = 60f;
        public float idleCost = 0.5f;
        public float movementCost = 1.2f;
        public float starvationDamage = 6f;
        public float maturityAge = 30f;
        public float lifespan = 240f;
        public float reproductionEnergy = 80f;
        public float reproductionCost = 40f;
        public float reproductionCooldown = 30f;
        public int populationLimit = 120;
        public float trainingSurvivalReward;
        public float trainingBirthReward;
        public float trainingDeathPenalty;
        public CombatConfig combat = new CombatConfig();

        public void Validate()
        {
            if (foraging)
            {
                (smallFood ?? throw new ArgumentException("缺少小型生長類型。")).Validate();
                (largeFood ?? throw new ArgumentException("缺少大型生長類型。")).Validate();
            }
            if (!float.IsFinite(halfExtent) || halfExtent <= 0 ||
                !float.IsFinite(speed) || speed <= 0 ||
                !float.IsFinite(stepSeconds) || stepSeconds <= 0 ||
                !float.IsFinite(observationRadius) || observationRadius <= 0 || episodeSteps <= 0 ||
                individualsPerSpecies < 1 || (!foraging && (individualsPerSpecies < 2 || removeStep <= 0 || addStep <= removeStep || addStep >= episodeSteps)))
                throw new ArgumentException("世界配置須為有限正數、每物種至少兩隻，且 0 < removeStep < addStep < episodeSteps。");
            if (foraging && (foodSourceLimit < 1 || naturalFoodLimit < 0 || naturalFoodLimit > foodSourceLimit ||
                !float.IsFinite(naturalSpawnInterval) || naturalSpawnInterval <= 0 || naturalSpawnBatch <= 0 ||
                !float.IsFinite(naturalFoodSpacing) || naturalFoodSpacing <= 0 || naturalFoodSpacing > halfExtent * 2 ||
                !float.IsFinite(newFoodFraction) || newFoodFraction < 0 || newFoodFraction >= smallFood.recoveryFraction ||
                !float.IsFinite(foodInitialFraction) || foodInitialFraction < 0 || foodInitialFraction > 1 ||
                !float.IsFinite(forageRadius) || forageRadius <= 0 ||
                !float.IsFinite(forageRate) || forageRate <= 0 ||
                !float.IsFinite(initialEnergy) || initialEnergy < 0 || initialEnergy > 100 ||
                !float.IsFinite(idleCost) || idleCost < 0 ||
                !float.IsFinite(movementCost) || movementCost < 0 ||
                !float.IsFinite(starvationDamage) || starvationDamage <= 0))
                throw new ArgumentException("食物、採食與生存參數無效。");
            if (foraging && (!float.IsFinite(maturityAge) || maturityAge <= 0 ||
                !float.IsFinite(lifespan) || lifespan <= maturityAge ||
                !float.IsFinite(reproductionEnergy) || reproductionEnergy <= 0 || reproductionEnergy > 100 ||
                !float.IsFinite(reproductionCost) || reproductionCost <= 0 || reproductionCost > reproductionEnergy ||
                !float.IsFinite(reproductionCooldown) || reproductionCooldown <= 0 ||
                populationLimit < (long)individualsPerSpecies * 3))
                throw new ArgumentException("成長期、壽命、繁殖或人口上限參數無效。");
            if (!float.IsFinite(trainingSurvivalReward) || !float.IsFinite(trainingBirthReward) || !float.IsFinite(trainingDeathPenalty))
                throw new ArgumentException("訓練獎勵必須是有限數值。");
            if (foraging) (combat ?? throw new ArgumentException("缺少戰鬥配置。")).Validate();
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
        public float age;
        public float bornAt;
        public float reproductionCooldown;
        public float nextReproductionTime;
        public float attackReadyAt;
        public float skillReadyAt;
        public WorldSkill[] loadout = Array.Empty<WorldSkill>();
        public float facing;
        public float velocityX;
        public float velocityY;
        public float[] skillReadyTimes = new float[2];
        public float[] skillCooldowns = new float[2];
        public bool skillUsedThisStep;
        public WorldSkill lastSkill;
        public float skillFlashUntil;
        public List<EntityObservation> perception = new List<EntityObservation>();
        public float attackCooldown;
        public float skillCooldown;
        public float dashRemaining;
        public bool dashActiveThisStep;
        public float rootUntil;
        public float rootRemaining;
        public bool trapSlowPending;
        public float poisonUntil;
        public int poisonStacks;
        public float nextPoisonExposureAt;
        public float nextPoisonDamageAt;
        public int poisonOwner;
        public float chargeRemaining;
        public bool chargeActiveThisStep;
        public float aimX;
        public float aimY;
        public float lastCombatAt = -1000f;
        public string lastDamageCause = "";
        public float hitUntil;
        public WorldSkill windupSkill;
        public float windupUntil;
        public float windupRemaining;
        public bool windupActiveThisStep;
        public float slowUntil;
        public float slowRemaining;
        public float shieldUntil;
        public float shieldRemaining;
        public float cultivateUntil;
        public float cultivateRemaining;
        public int cultivatingFood;
        public List<MovementBiasState> movementBiases = new List<MovementBiasState>();
    }

    [Serializable]
    public sealed class MovementBiasState
    {
        public WorldSkill skill;
        public float until;
        public float angle;
        public float targetAngle;
        public float nextDirectionAt;
    }

    [Serializable]
    public sealed class EntityObservation
    {
        public string species;
        public float x, y;
        public bool visible;
        public float velocityX, velocityY;
        public float age;
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
        public FoodGrowthType growthType;
        public FoodPhase phase;
        public float restUntil;
        public float bornAt;
        public bool natural;
        public bool mature = true;
        public float matureAt;
    }

    [Serializable]
    public sealed class WorldEvent
    {
        public string kind;
        public int individual;
        public int food;
        public float amount;
        public int parent;
        public string species;
        public string cause;
        public float age;
        public int target;
        public float rawAmount;
        public WorldSkill skill;
        public float x;
        public float y;
    }

    [Serializable]
    public sealed class DeathRecord
    {
        public int individual;
        public string species;
        public string cause;
        public float age;
        public float time;
    }

    [Serializable]
    public sealed class SpeciesStatistics
    {
        public string species;
        public int births;
        public int starved;
        public int oldAge;
        public int combat;
        public int populationLimitHits;
        public int incompleteLives;
        public float completedAgeTotal;
    }

    public readonly struct WorldAction
    {
        public readonly Vector2 movement;
        public readonly bool forage;
        public readonly Vector2 aim;
        public readonly bool attack;
        public readonly WorldSkill skill;
        public readonly int turn;
        public readonly int skillSlot;
        public WorldAction(Vector2 movement, bool forage = false, Vector2 aim = default, bool attack = false, WorldSkill skill = WorldSkill.None, int turn = 0, int skillSlot = -1)
        {
            this.movement = movement;
            this.forage = forage;
            this.aim = aim;
            this.attack = attack;
            this.skill = skill;
            this.turn = turn;
            this.skillSlot = skillSlot;
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
        public List<DeathRecord> deaths = new List<DeathRecord>();
        public List<SpeciesStatistics> statistics = new List<SpeciesStatistics>();
        public List<ProjectileState> projectiles = new List<ProjectileState>();
        public int nextProjectile;
        public int nextFood;
        public float nextNaturalSpawnAt;
        public int nextArea;
        public List<AreaState> areas = new List<AreaState>();
    }

    [Serializable]
    public sealed class AreaState
    {
        public int area;
        public int owner;
        public string species;
        public WorldSkill skill;
        public float x, y, radius, until;
    }

    [Serializable]
    public sealed class ProjectileState
    {
        public WorldSkill skill;
        public List<int> hitIndividuals = new List<int>();
        public float damage;
        public int projectile;
        public int owner;
        public string species;
        public float x;
        public float y;
        public float directionX;
        public float directionY;
        public float remaining;
    }

    public sealed class World
    {
        public static readonly IReadOnlyList<string> Species = Array.AsReadOnly(new[] { "Flame", "Frost", "Moss" });
        public WorldConfig Config { get; }
        public WorldState State { get; private set; }
        private readonly System.Random random;
        private readonly System.Random effectRandom;
        private int episode;
        private int nextIndividual;
        private readonly int[] loadoutCycle = new int[3];
        private readonly int[,] loadoutOrder = new int[3, 6];
        private readonly Dictionary<int, Dictionary<int, EnemySighting>> enemyMemory = new Dictionary<int, Dictionary<int, EnemySighting>>();

        private sealed class EnemySighting
        {
            public string species;
            public Vector2 position;
            public float lastSeenAt;
        }

        public World(WorldConfig config, int seed)
        {
            config.Validate();
            Config = config;
            random = new System.Random(seed);
            effectRandom = new System.Random(seed ^ 0x43f214);
        }

        internal void ReadSnapshot(WorldState snapshot) => State = snapshot;

        public WorldState Reset()
        {
            State = new WorldState { episode = ++episode };
            foreach (string species in Species)
                State.statistics.Add(new SpeciesStatistics { species = species });
            Array.Clear(loadoutCycle, 0, loadoutCycle.Length);
            enemyMemory.Clear();
            for (int species = 0; species < 3; species++)
                ShuffleLoadouts(species);
            foreach (string species in Species)
                for (int i = 0; i < Config.individualsPerSpecies; i++)
                    AddIndividual(species, balancedLoadout: true);
            if (Config.foraging)
            {
                for (int i = 0; i < Mathf.Min(4, Config.naturalFoodLimit); i++)
                {
                    var position = new Vector2((i % 2 == 0 ? -1 : 1) * Config.halfExtent * 0.45f,
                        (i < 2 ? -1 : 1) * Config.halfExtent * 0.45f);
                    if (!State.foods.Exists(value => Vector2.Distance(position, new Vector2(value.x, value.y)) < Config.naturalFoodSpacing))
                        AddNaturalFood(position, FoodGrowthType.Large, true, 0);
                }
                for (int i = 4; i < Config.naturalFoodLimit && State.foods.Count < Config.foodSourceLimit; i++)
                    TryAddNaturalFood(initial: true, time: 0);
                State.nextNaturalSpawnAt = Config.naturalSpawnInterval;
            }
            UpdateIncompleteLives();
            RefreshPerception();
            return State;
        }

        public IndividualState AddIndividual(string species, bool balancedLoadout = false)
        {
            bool known = false;
            foreach (string value in Species)
                known |= value == species;
            if (!known)
                throw new ArgumentException("未知物種。");
            var individual = new IndividualState
            {
                individual = ++nextIndividual, species = species,
                health = CombatRules.MaxHealth(species),
                energy = Config.initialEnergy,
                age = Config.foraging ? Config.maturityAge : 0,
                bornAt = Config.foraging ? State.time - Config.maturityAge : State.time,
                x = (float)(random.NextDouble() - 0.5) * Config.halfExtent,
                y = (float)(random.NextDouble() - 0.5) * Config.halfExtent,
                facing = (float)random.NextDouble() * 360f
            };
            individual.loadout = CreateLoadout(species, balancedLoadout);
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
                    !float.IsFinite(action.movement.x) || !float.IsFinite(action.movement.y) ||
                    !float.IsFinite(action.aim.x) || !float.IsFinite(action.aim.y) || (action.turn < -1 || action.turn > 1) || action.skillSlot < -1 || action.skillSlot > 1 || !Enum.IsDefined(typeof(WorldSkill), action.skill))
                    throw new ArgumentException("每隻個體都必須取得有限數值的動作。");
            State.events.Clear();
            float time = (State.step + 1) * Config.stepSeconds;
            AdvanceFood(time);
            if (Config.foraging)
            {
                CombatRules.BeginStep(Config, State, actions);
                ControlEffects.PrepareMovement(Config, State, effectRandom);
            }
            var distances = new Dictionary<int, float>();
            foreach (var individual in State.individuals)
            {
                var start = CombatRules.Position(individual);
                var position = start + Vector2.ClampMagnitude(actions[individual.individual].movement, 1f) * (Config.speed * Config.stepSeconds);
                var bounded = Config.foraging ? (individual.health > 0 ? CombatRules.Move(Config, State, individual, actions[individual.individual]) : start)
                    : new Vector2(Mathf.Clamp(position.x, -Config.halfExtent, Config.halfExtent), Mathf.Clamp(position.y, -Config.halfExtent, Config.halfExtent));
                individual.velocityX = (bounded.x - start.x) / Config.stepSeconds;
                individual.velocityY = (bounded.y - start.y) / Config.stepSeconds;
                individual.x = bounded.x;
                individual.y = bounded.y;
                individual.facing = Mathf.Repeat(individual.facing + actions[individual.individual].turn * 180f * Config.stepSeconds, 360f);
                if (!Config.foraging) individual.boundary = position != bounded;
                distances[individual.individual] = Vector2.Distance(start, bounded);
            }
            if (Config.foraging)
            {
                CombatRules.Resolve(Config, State, actions);
                ControlEffects.Resolve(Config, State, effectRandom);
            }
            foreach (var individual in State.individuals)
            {
                if (!Config.foraging) continue;
                individual.age = time - individual.bornAt;
                individual.reproductionCooldown = Mathf.Max(0, individual.nextReproductionTime - time);
                float distance = distances[individual.individual];
                individual.energy = Mathf.Max(0, individual.energy - Config.idleCost * Config.stepSeconds - Config.movementCost * distance);
                if (individual.health > 0 && actions[individual.individual].forage)
                    foreach (var food in State.foods)
                        if (food.mature && food.phase == FoodPhase.Available && Vector2.Distance(CombatRules.Position(individual), new Vector2(food.x, food.y)) <= Config.forageRadius)
                        {
                            float eaten = Mathf.Min(food.amount, Config.forageRate * Config.stepSeconds, 100f - individual.energy);
                            if (eaten <= 0) continue;
                            food.amount -= eaten;
                            individual.energy += eaten;
                            State.events.Add(new WorldEvent { kind = "ate", individual = individual.individual, food = food.food, amount = eaten });
                            if (food.amount <= 0.00001f)
                            {
                                food.amount = 0;
                                food.phase = FoodPhase.Resting;
                                food.restUntil = time + Growth(food).restSeconds;
                                State.events.Add(new WorldEvent { kind = "food_depleted", food = food.food, x = food.x, y = food.y });
                            }
                            break;
                        }
                if (individual.health > 0 && individual.energy <= 0)
                {
                    individual.health = Mathf.Max(0, individual.health - Config.starvationDamage * Config.stepSeconds);
                    individual.lastDamageCause = "starved";
                }
                CombatRules.Recover(Config, State, individual, time);
                string cause = individual.health <= 0 ? individual.lastDamageCause : individual.age >= Config.lifespan ? "old_age" : "";
                if (cause != "")
                {
                    individual.health = 0;
                    State.events.Add(new WorldEvent { kind = cause, individual = individual.individual,
                        species = individual.species, cause = cause, age = individual.age });
                    State.deaths.Add(new DeathRecord { individual = individual.individual, species = individual.species,
                        cause = cause, age = individual.age, time = time });
                    var statistics = Statistics(individual.species);
                    if (cause == "starved") statistics.starved++;
                    else if (cause == "combat") statistics.combat++;
                    else statistics.oldAge++;
                    statistics.completedAgeTotal += individual.age;
                }
            }
            if (Config.foraging)
            {
                State.individuals.RemoveAll(value => value.health <= 0);
                // 本步新生個體不再行動或繁殖；先結算死亡，再分配可用人口名額。
                var parents = State.individuals.ToArray();
                foreach (var parent in parents)
                {
                    if (parent.age < Config.maturityAge || parent.energy < Config.reproductionEnergy ||
                        parent.reproductionCooldown > 0) continue;
                    var statistics = Statistics(parent.species);
                    if (State.individuals.Count >= Config.populationLimit)
                    {
                        statistics.populationLimitHits++;
                        State.events.Add(new WorldEvent { kind = "population_limit", individual = parent.individual, species = parent.species });
                        continue;
                    }
                    var child = AddIndividual(parent.species);
                    child.x = parent.x;
                    child.y = parent.y;
                    child.age = 0;
                    child.bornAt = time;
                    parent.energy -= Config.reproductionCost;
                    parent.nextReproductionTime = time + Config.reproductionCooldown;
                    parent.reproductionCooldown = Config.reproductionCooldown;
                    statistics.births++;
                    State.events.Add(new WorldEvent { kind = "born", individual = child.individual,
                        parent = parent.individual, species = child.species, amount = Config.reproductionCost });
                }
                UpdateIncompleteLives();
            }
            State.step++;
            State.time = State.step * Config.stepSeconds;
            RefreshPerception();
            State.endReason = State.individuals.Count == 0 ? "all_dead" : State.step >= Config.episodeSteps ? "time_limit" : "";
            return State;
        }

        private SpeciesStatistics Statistics(string species) => State.statistics.Find(value => value.species == species);

        private FoodGrowthConfig Growth(FoodState food) => food.growthType == FoodGrowthType.Large ? Config.largeFood : Config.smallFood;

        private void AdvanceFood(float time)
        {
            for (int i = State.foods.Count - 1; i >= 0; i--)
            {
                var expired = State.foods[i];
                if (time < expired.bornAt + Growth(expired).lifetimeSeconds) continue;
                State.foods.RemoveAt(i);
                foreach (var individual in State.individuals)
                    if (individual.cultivatingFood == expired.food)
                    {
                        individual.cultivatingFood = 0;
                        individual.cultivateUntil = 0;
                        individual.cultivateRemaining = 0;
                    }
                State.events.Add(new WorldEvent { kind = "food_expired", food = expired.food, x = expired.x, y = expired.y });
            }
            foreach (var food in State.foods)
            {
                if (!food.mature && time >= food.matureAt)
                {
                    food.mature = true;
                    State.events.Add(new WorldEvent { kind = "food_matured", food = food.food, x = food.x, y = food.y });
                }
                if (!food.mature || food.phase == FoodPhase.Resting && time < food.restUntil) continue;
                if (food.phase == FoodPhase.Resting)
                {
                    food.phase = FoodPhase.Recovering;
                    State.events.Add(new WorldEvent { kind = "food_recovering", food = food.food, x = food.x, y = food.y });
                }
                food.amount = Mathf.Min(food.capacity, food.amount + Growth(food).growthRate * Config.stepSeconds);
                if (food.phase == FoodPhase.Recovering && food.amount >= food.capacity * Growth(food).recoveryFraction)
                {
                    food.phase = FoodPhase.Available;
                    State.events.Add(new WorldEvent { kind = "food_available", food = food.food, x = food.x, y = food.y });
                }
            }
            if (Config.foraging && time >= State.nextNaturalSpawnAt)
            {
                State.nextNaturalSpawnAt = time + Config.naturalSpawnInterval;
                int missing = Config.naturalFoodLimit - State.foods.FindAll(value => value.natural).Count;
                for (int i = 0; i < Mathf.Min(missing, Config.naturalSpawnBatch) && State.foods.Count < Config.foodSourceLimit; i++)
                    TryAddNaturalFood(initial: false, time: time);
            }
        }

        private bool TryAddNaturalFood(bool initial, float time)
        {
            for (int attempt = 0; attempt < 32; attempt++)
            {
                var point = new Vector2((float)(random.NextDouble() * 2 - 1) * Config.halfExtent * 0.9f,
                    (float)(random.NextDouble() * 2 - 1) * Config.halfExtent * 0.9f);
                if (State.foods.Exists(value => Vector2.Distance(point, new Vector2(value.x, value.y)) < Config.naturalFoodSpacing)) continue;
                AddNaturalFood(point, FoodGrowthType.Small, initial, time);
                return true;
            }
            return false;
        }

        private void AddNaturalFood(Vector2 position, FoodGrowthType type, bool initial, float time)
        {
            var growth = type == FoodGrowthType.Large ? Config.largeFood : Config.smallFood;
            var food = new FoodState { food = ++State.nextFood, natural = true, patch = type == FoodGrowthType.Large,
                growthType = type, x = position.x, y = position.y, capacity = growth.capacity,
                amount = growth.capacity * (initial ? Config.foodInitialFraction : Config.newFoodFraction),
                phase = initial && Config.foodInitialFraction >= growth.recoveryFraction ? FoodPhase.Available : FoodPhase.Recovering,
                bornAt = time };
            State.foods.Add(food);
            if (!initial) State.events.Add(new WorldEvent { kind = "food_spawned", food = food.food, x = food.x, y = food.y });
        }

        private void UpdateIncompleteLives()
        {
            foreach (var statistics in State.statistics)
                statistics.incompleteLives = State.individuals.FindAll(value => value.species == statistics.species).Count;
        }

        private WorldSkill[] CreateLoadout(string species, bool balanced = false)
        {
            WorldSkill[] pool = SkillPool(species);
            int speciesIndex = SpeciesIndex(species);
            int cycle = loadoutCycle[speciesIndex]++;
            if (balanced && cycle > 0 && cycle % 6 == 0) ShuffleLoadouts(speciesIndex);
            int combo = balanced ? loadoutOrder[speciesIndex, cycle % 6] : random.Next(6);
            int first = 0, second = 1, index = 0;
            for (int a = 0; a < 4; a++) for (int b = a + 1; b < 4; b++, index++) if (index == combo) { first = a; second = b; }
            return new[] { pool[first], pool[second] };
        }

        public static bool IsSpeciesSkill(string species, WorldSkill skill) => Array.IndexOf(SkillPool(species), skill) >= 0;

        private static WorldSkill[] SkillPool(string species) => species == "Flame" ? new[] { WorldSkill.Charge, WorldSkill.HotStrike, WorldSkill.Shield, WorldSkill.IntimidatingRoar }
                : species == "Frost" ? new[] { WorldSkill.FrostbindArrow, WorldSkill.PiercingIceArrow, WorldSkill.ColdMistDash, WorldSkill.FrostTrap }
                : new[] { WorldSkill.Cultivate, WorldSkill.Sow, WorldSkill.EntanglingRoots, WorldSkill.SporeSmoke };

        private void ShuffleLoadouts(int species)
        {
            for (int i = 0; i < 6; i++) loadoutOrder[species, i] = i;
            for (int i = 5; i > 0; i--)
            {
                int other = random.Next(i + 1);
                int value = loadoutOrder[species, i];
                loadoutOrder[species, i] = loadoutOrder[species, other];
                loadoutOrder[species, other] = value;
            }
        }

        private static int SpeciesIndex(string species) => species == "Flame" ? 0 : species == "Frost" ? 1 : 2;

        public float[] Observe(IndividualState individual)
        {
            return new[]
            {
                Mathf.Clamp01((Config.halfExtent - individual.x) / Config.observationRadius),
                Mathf.Clamp01((Config.halfExtent + individual.x) / Config.observationRadius),
                Mathf.Clamp01((Config.halfExtent - individual.y) / Config.observationRadius),
                Mathf.Clamp01((Config.halfExtent + individual.y) / Config.observationRadius),
                individual.facing / 360f, Mathf.Clamp01(individual.energy / 100f),
                (int)individual.loadout[0] / 12f, (int)individual.loadout[1] / 12f,
                individual.health / CombatRules.MaxHealth(individual.species),
                individual.skillCooldowns[0] / 10f, individual.skillCooldowns[1] / 10f
            };
        }

        public bool CanSee(IndividualState observer, IndividualState target)
        {
            return target != observer && target.health > 0 && CanSeePoint(observer, CombatRules.Position(target));
        }

        public bool CanSeePoint(IndividualState observer, Vector2 point)
        {
            Vector2 delta = point - CombatRules.Position(observer);
            Vector2 facing = new Vector2(Mathf.Cos(observer.facing * Mathf.Deg2Rad), Mathf.Sin(observer.facing * Mathf.Deg2Rad));
            return delta.sqrMagnitude <= Config.observationRadius * Config.observationRadius
                && (delta.sqrMagnitude == 0 || Vector2.Angle(facing, delta) <= 60f);
        }

        public void RefreshPerception(bool clearMemory = false)
        {
            if (clearMemory) enemyMemory.Clear();
            var alive = new HashSet<int>();
            foreach (var observer in State.individuals)
            {
                alive.Add(observer.individual);
                if (!enemyMemory.TryGetValue(observer.individual, out var memory))
                    enemyMemory.Add(observer.individual, memory = new Dictionary<int, EnemySighting>());
                observer.perception.Clear();
                var visible = new HashSet<int>();
                foreach (var target in State.individuals)
                {
                    if (!CanSee(observer, target)) continue;
                    visible.Add(target.individual);
                    observer.perception.Add(new EntityObservation { species = target.species, x = target.x, y = target.y,
                        visible = true, velocityX = target.velocityX, velocityY = target.velocityY });
                    if (target.species != observer.species)
                        memory[target.individual] = new EnemySighting { species = target.species, position = CombatRules.Position(target), lastSeenAt = State.time };
                }
                var expired = new List<int>();
                foreach (var pair in memory)
                {
                    var last = pair.Value;
                    float age = State.time - last.lastSeenAt;
                    if (age >= 5f) expired.Add(pair.Key);
                    else if (!visible.Contains(pair.Key))
                        observer.perception.Add(new EntityObservation { species = last.species, x = last.position.x, y = last.position.y, age = age });
                }
                foreach (int id in expired) memory.Remove(id);
                // 以空間排序，避免觀察列順序成為個體的穩定識別。
                observer.perception.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            }
            var removed = new List<int>();
            foreach (int id in enemyMemory.Keys) if (!alive.Contains(id)) removed.Add(id);
            foreach (int id in removed) enemyMemory.Remove(id);
        }

        public IEnumerable<float[]> ObserveEntities(IndividualState individual)
        {
            foreach (var entity in individual.perception)
                yield return new[] { (SpeciesIndex(entity.species) + 1) / 3f,
                    (entity.x - individual.x) / Config.observationRadius, (entity.y - individual.y) / Config.observationRadius,
                    entity.velocityX / 4f, entity.velocityY / 4f, entity.visible ? 1f : 0f, entity.age / 5f };
            foreach (var food in State.foods)
            {
                var delta = new Vector2(food.x - individual.x, food.y - individual.y);
                if (delta.sqrMagnitude > Config.observationRadius * Config.observationRadius) continue;
                yield return new[] { 0f, delta.x / Config.observationRadius, delta.y / Config.observationRadius,
                    0f, 0f, food.mature && food.phase == FoodPhase.Available ? 1f : 0f, food.capacity > 0 ? food.amount / food.capacity : 0f };
            }
        }
    }
}
