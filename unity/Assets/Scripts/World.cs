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
        public float firstMaturitySeconds = 10f, fruitingSeconds = 30f;
        public int minFruit = 2, maxFruit = 4;
        public float growthRadius = 4f;

        public void Validate()
        {
            if (!float.IsFinite(capacity) || capacity <= 0 || !float.IsFinite(growthRate) || growthRate <= 0 ||
                !float.IsFinite(restSeconds) || restSeconds <= 0 || !float.IsFinite(lifetimeSeconds) || lifetimeSeconds <= 0 ||
                !float.IsFinite(recoveryFraction) || recoveryFraction <= 0 || recoveryFraction > 1 ||
                !float.IsFinite(firstMaturitySeconds) || firstMaturitySeconds <= 0 ||
                !float.IsFinite(fruitingSeconds) || fruitingSeconds <= 0 ||
                !float.IsFinite(growthRadius) || growthRadius <= 0 ||
                minFruit < 2 || maxFruit < minFruit || maxFruit > capacity || maxFruit > 64 ||
                fruitingSeconds < 2 * (maxFruit - 1))
                throw new ArgumentException("食物生長類型參數無效。");
        }

        public bool Grow(FoodState food, float amount)
        {
            food.amount = Mathf.Min(food.capacity, food.amount + amount);
            if (food.phase != FoodPhase.Recovering || food.amount < food.capacity * recoveryFraction) return false;
            food.phase = FoodPhase.Available;
            return true;
        }
    }

    public enum FoodPhase { Available, Resting, Recovering, Waiting }
    public enum FoodGrowthType { Small, Large }

    public static class ScanReasons
    {
        public const string Energy = "energy";
        public const string Escape = "escape";
        public const string Threat = "threat";
        public const string StaleDirection = "stale_direction";
    }

    [Serializable]
    public sealed class WorldConfig
    {
        public float halfExtent = 5f;
        public float speed = 2.5f;
        public float stepSeconds = 0.02f;
        public float observationRadius = 2f;
        public float explorationCellSize = 2f;
        public int areaMemoryLimit = 64;
        public float areaMemorySeconds = 30f;
        public bool disableCare, disableMemoryPrediction;
        public float familySearchSatiety = 40f;
        public float protectionRadius = 2.5f, protectionEngageHealth = 0.45f, protectionRetreatHealth = 0.25f, protectionLoss = 0.2f;
        public float careRadius = 0.7f, careRate = 5f, careReserve = 30f, careStart = 40f, careStop = 60f;
        public int foodMemoryLimit = 32;
        public float foodMemorySeconds = 30f;
        public float foodPredictionSeconds = 30f;
        public bool scanEnabled = true;
        public float scanFreshnessSeconds = 8f;
        public float scanThreatRadius = 2.5f;
        public float scanEnergyThreshold = 12f;
        public float aiWeakHealth = 0.45f, aiRetreatHealth = 0.25f, aiCombatLoss = 0.2f;
        public float aiRunStart = 60f, aiRunStop = 20f, aiUrgentSatiety = 45f;
        public float aiTargetMargin = 1f;
        public int episodeSteps = 256;
        public int individualsPerSpecies = 2;
        public string initialSpecies = "";
        public bool disableReproduction, disableAging;
        public int removeStep = 64;
        public int addStep = 96;
        public bool foraging;
        public FoodGrowthConfig smallFood = new FoodGrowthConfig { capacity = 4f, growthRate = 0.75f, restSeconds = 20f, lifetimeSeconds = 120f };
        public FoodGrowthConfig largeFood = new FoodGrowthConfig { capacity = 7f, growthRate = 1.5f, restSeconds = 30f, lifetimeSeconds = 240f, firstMaturitySeconds = 15f, fruitingSeconds = 40f, minFruit = 5, maxFruit = 7, growthRadius = 5.5f };
        public float foodInitialFraction = 0.67f;
        public int foodSourceLimit = 64;
        public int naturalFoodLimit = 16;
        public float naturalSpawnInterval = 10f;
        public int naturalSpawnBatch = 2;
        public float naturalFoodSpacing = 1f;
        public float newFoodFraction = 0.05f;
        public float forageRadius = 0.7f;
        public float forageRate = 12f;
        public float initialSatiety = 60f;
        public float initialStamina = 100f, initialMana = 100f, initialHealthFraction = 1f;
        public float healthRecoveryDelay = 5f, healthRecoveryFraction = 0.01f;
        public float staminaRecoveryDelay = 1f, staminaRecoveryRate = 10f, manaRecoveryRate = 2f;
        public float nutritionStart = 0.3f, nutritionStop = 0.6f, nutritionReserve = 30f, nutritionBudget = 0.5f;
        public float nutritionHealthFraction = 0.01f, nutritionStamina = 4f, nutritionMana = 4f;
        public float idleCost = 0.5f;
        public float runMultiplier = 1.5f, runStaminaRate = 10f;
        public float starvationDamage = 0.02f;
        public float maturityAge = 30f, newbornGrowth = 0.3f;
        public float lifespan = 240f;
        public float reproductionSatiety = 80f;
        public float reproductionHealthFraction = 0.8f, reproductionReserve = 30f, newbornSatiety = 30f;
        public float reproductionCost = 40f;
        public float reproductionCooldown = 30f;
        public int populationLimit = 120;
        public float trainingSurvivalReward;
        public float trainingBirthReward;
        public float trainingDeathPenalty;
        public float explorationRewardPerCell = 0.0001f;
        public float explorationRewardLimit = 0.02f;
        public CombatConfig combat = new CombatConfig();

        public void Validate()
        {
            if (!string.IsNullOrEmpty(initialSpecies) && (!foraging || initialSpecies != "Flame" && initialSpecies != "Frost" && initialSpecies != "Moss"))
                throw new ArgumentException("指定起始物種須為覓食世界的合法物種。");
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
            if (foraging && (foodSourceLimit < 1 || foodSourceLimit > 256 || naturalFoodLimit < 0 || naturalFoodLimit > foodSourceLimit ||
                !float.IsFinite(explorationCellSize) || explorationCellSize <= 0 ||
                halfExtent * 2 / explorationCellSize > 64 ||
                areaMemoryLimit < 1 || areaMemoryLimit > 256 || foodMemoryLimit < 1 || foodMemoryLimit > 128 ||
                !float.IsFinite(familySearchSatiety) || familySearchSatiety < 0 || familySearchSatiety > 100 ||
                !float.IsFinite(protectionRadius) || protectionRadius <= 0 ||
                !float.IsFinite(protectionEngageHealth) || !float.IsFinite(protectionRetreatHealth) || !float.IsFinite(protectionLoss) ||
                protectionRetreatHealth < 0 || protectionEngageHealth <= protectionRetreatHealth || protectionEngageHealth > 1 || protectionLoss <= 0 || protectionLoss > 1 ||
                !float.IsFinite(careRadius) || careRadius <= 0 || !float.IsFinite(careRate) || careRate <= 0 ||
                !float.IsFinite(careReserve) || careReserve < 0 || careReserve > 100 ||
                !float.IsFinite(careStart) || careStart < 0 || !float.IsFinite(careStop) || careStop <= careStart || careStop > 100 ||
                !float.IsFinite(areaMemorySeconds) || areaMemorySeconds <= 0 ||
                !float.IsFinite(foodMemorySeconds) || foodMemorySeconds <= 0 ||
                !float.IsFinite(foodPredictionSeconds) || foodPredictionSeconds <= 0 ||
                !float.IsFinite(scanFreshnessSeconds) || scanFreshnessSeconds <= 0 ||
                !float.IsFinite(scanThreatRadius) || scanThreatRadius <= 0 ||
                !float.IsFinite(scanEnergyThreshold) || scanEnergyThreshold < 0 || scanEnergyThreshold > 100 ||
                !float.IsFinite(naturalSpawnInterval) || naturalSpawnInterval <= 0 || naturalSpawnBatch <= 0 ||
                !float.IsFinite(naturalFoodSpacing) || naturalFoodSpacing <= 0 || naturalFoodSpacing > halfExtent * 2 ||
                !float.IsFinite(newFoodFraction) || newFoodFraction < 0 || newFoodFraction >= smallFood.recoveryFraction ||
                !float.IsFinite(foodInitialFraction) || foodInitialFraction < 0 || foodInitialFraction > 1 ||
                !float.IsFinite(forageRadius) || forageRadius <= 0 ||
                !float.IsFinite(forageRate) || forageRate <= 0 ||
                !float.IsFinite(initialSatiety) || initialSatiety < 0 || initialSatiety > 100 ||
                !float.IsFinite(idleCost) || idleCost < 0 ||
                !float.IsFinite(runMultiplier) || runMultiplier < 1 || !float.IsFinite(runStaminaRate) || runStaminaRate <= 0 ||
                !float.IsFinite(starvationDamage) || starvationDamage <= 0))
                throw new ArgumentException("食物、採食與生存參數無效。");
            if (foraging && (!float.IsFinite(newbornGrowth) || newbornGrowth <= 0 || newbornGrowth > 1 || !float.IsFinite(maturityAge) || maturityAge <= 0 ||
                !float.IsFinite(lifespan) || lifespan <= maturityAge ||
                !float.IsFinite(reproductionSatiety) || reproductionSatiety <= 0 || reproductionSatiety > 100 ||
                !float.IsFinite(reproductionCost) || reproductionCost <= 0 || reproductionCost > reproductionSatiety ||
                !float.IsFinite(reproductionHealthFraction) || reproductionHealthFraction <= 0 || reproductionHealthFraction > 1 ||
                !float.IsFinite(reproductionReserve) || reproductionReserve < 0 || reproductionReserve >= reproductionSatiety ||
                !float.IsFinite(newbornSatiety) || newbornSatiety < 0 || newbornSatiety > reproductionCost || newbornSatiety > 100 ||
                !float.IsFinite(reproductionCooldown) || reproductionCooldown <= 0 ||
                populationLimit < (long)individualsPerSpecies * 3))
                throw new ArgumentException("成長期、壽命、繁殖或人口上限參數無效。");
            if (!float.IsFinite(trainingSurvivalReward) || !float.IsFinite(trainingBirthReward) || !float.IsFinite(trainingDeathPenalty) ||
                !float.IsFinite(explorationRewardPerCell) || explorationRewardPerCell < 0 ||
                !float.IsFinite(explorationRewardLimit) || explorationRewardLimit < 0)
                throw new ArgumentException("訓練獎勵必須是有限數值。");
            if (aiRetreatHealth < 0 || aiWeakHealth <= aiRetreatHealth || aiWeakHealth > 1 || aiCombatLoss <= 0 || aiCombatLoss > 1 || aiRunStop < 0 || aiRunStart <= aiRunStop || aiRunStart > 100 || aiUrgentSatiety < 0 || aiUrgentSatiety > 100 || aiTargetMargin < 0)
                throw new ArgumentException("規則決策門檻無效。");
            foreach (float value in new[] { aiWeakHealth, aiRetreatHealth, aiCombatLoss, aiRunStart, aiRunStop, aiUrgentSatiety, aiTargetMargin })
                if (!float.IsFinite(value)) throw new ArgumentException("規則決策門檻須為有限值。");
            foreach (float value in new[] { initialSatiety, initialStamina, initialMana })
                if (!float.IsFinite(value) || value < 0 || value > 100) throw new ArgumentException("資源初值無效。");
            if (!float.IsFinite(initialHealthFraction) || initialHealthFraction <= 0 || initialHealthFraction > 1 || nutritionStart <= 0 || nutritionStop <= nutritionStart || nutritionStop > 1 || nutritionReserve > 100)
                throw new ArgumentException("健康或營養門檻無效。");
            foreach (float value in new[] { healthRecoveryDelay, healthRecoveryFraction, staminaRecoveryDelay, staminaRecoveryRate, manaRecoveryRate, nutritionStart, nutritionStop, nutritionReserve, nutritionBudget, nutritionHealthFraction, nutritionStamina, nutritionMana })
                if (!float.IsFinite(value) || value < 0) throw new ArgumentException("恢復配置無效。");
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
        public float satiety, stamina, mana;
        public float lastStrenuousAt = -1000f;
        public bool[] nutritionActive = new bool[3];
        public string[] recoveryStatus = new string[3];
        public bool running;
        public bool feeding;
        public float fedSourceX, fedSourceY, fedAmount;
        public bool hasFoodTarget, runPreferred;
        public float foodTargetX, foodTargetY, contestHealth, retreatUntil, retreatX, retreatY;
        public string decisionReason = "";
        public float activityStaminaSpent, skillManaSpent, reproductionSpent;
        public bool initialAdult = true;
        public int parentId;
        public int careTarget, careReceivedFrom;
        public int protectionChild, protectionThreat, protectionEventChild, protectionEventThreat;
        public float protectionDamageStart;
        public bool protectionRetreat;
        public string protectionEvent = "";
        public float careGiven, careReceived, careEstimatedSatiety;
        public List<FamilyMemory> children = new List<FamilyMemory>();
        public float observedStart, observedSeconds;
        public float combatDamageTaken, starvationDamageTaken, agingHealthLost;
        public float lastAteAt = -1, lastReproducedAt = -1, lastNutritionAt = -1;
        public float lastCombatDamageAt = -1;
        public string lastCombatDamageKind = "";
        public int lastCombatDamageSource;
        public ResourceLedger ledger = new ResourceLedger();
        [NonSerialized] public List<VitalSample> recentHistory = new List<VitalSample>();
        public float basalSpent, foodReceived, nutritionSpent;
        public float[] automaticRecovered = new float[3], nutritionRecovered = new float[3];
        public float age;
        public float growth, maxHealth;
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
        public bool smokeKnowledgeAvailable;
        public List<SmokeSighting> smokeKnowledge = new List<SmokeSighting>();
        public float attackCooldown;
        public float skillCooldown;
        public float dashRemaining;
        public bool dashActiveThisStep;
        public float rootUntil;
        public float rootRemaining;
        public bool trapSlowPending;
        public float trapSlowDuration;
        public float poisonUntil;
        public int poisonStacks;
        public float nextPoisonExposureAt;
        public float nextPoisonDamageAt;
        public int poisonOwner;
        public float chargeGrowth = 1f;
        public float chargeRemaining;
        public bool chargeActiveThisStep;
        public float aimX;
        public float aimY;
        public float lastCombatAt = -1000f;
        public string lastDamageCause = "";
        public float hitUntil;
        public float basicAttackGrowth = 1f;
        public float skillGrowth = 1f;
        public WorldSkill windupSkill;
        public float windupUntil;
        public float windupRemaining;
        public bool windupActiveThisStep;
        public float slowUntil;
        public float slowRemaining;
        public float shieldGrowth = 1f;
        public float shieldUntil;
        public float shieldRemaining;
        public float cultivateGrowth = 1f;
        public float cultivateUntil;
        public float cultivateRemaining;
        public int cultivatingFood;
        public List<MovementBiasState> movementBiases = new List<MovementBiasState>();
        public List<AreaMemory> areaMemory = new List<AreaMemory>();
        public List<FoodMemory> foodMemory = new List<FoodMemory>();
        public List<FoodMemory> visibleFoods = new List<FoodMemory>();
        public List<int> rewardedCells = new List<int>();
        public float explorationRewardTotal;
        public float explorationRewardPending;
        public int explorationTargetX = -1;
        public int explorationTargetY = -1;
        public float[] directionLastSeenAt = new float[8];
        public int scanTargetSector = -1;
        public int rawTurn;
        public int actualTurn;
        public bool scanIntervened;
        public bool scanDeferred;
        public string scanReason = "";
        public float scanDeferredSeconds;
    }

    [Serializable]
    public sealed class AreaMemory
    {
        public int cellX;
        public int cellY;
        public float lastSeenAt;
    }

    [Serializable]
    public sealed class FamilyMemory
    {
        public int child;
        public float x, y, age, satiety, lastSeenAt;
        public float predictedAge, predictedSatiety;
        public bool visible, confirmedDead, threatened, searched;
    }

    [Serializable]
    public sealed class FoodMemory
    {
        public int food;
        public float x;
        public float y;
        public float amount;
        public float capacity;
        public bool mature;
        public FoodPhase phase;
        public bool visible;
        public float lastSeenAt;
        public bool possiblyAvailable;
    }

    [Serializable]
    public sealed class MovementBiasState
    {
        public WorldSkill skill;
        public float until;
        public float angle;
        public float targetAngle;
        public float nextDirectionAt;
        public float strength = 1f;
    }

    [Serializable]
    public sealed class EntityObservation
    {
        public string species;
        public int individual;
        public float x, y;
        public bool visible;
        public float velocityX, velocityY;
        public float age;
        public bool feeding;
        public float sourceX, sourceY;
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
        public bool cycleInitialized;
        public float phaseProgress, lastAdvancedAt, lastFruitAt = -1000f, waitUntil;
        public int cycle, nextMaturity;
        public float[] maturitySchedule = Array.Empty<float>();
        public float growthMultiplier = 1f;
    }

    [Serializable]
    public sealed class WorldEvent
    {
        public string kind;
        public int fruit;
        public int individual;
        public int food;
        public float amount;
        public int parent;
        public int child;
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
    public sealed class RewardRecord
    {
        public int individual;
        public float survival;
        public float birth;
        public float death;
        public float exploration;
        public float Total => survival + birth + death + exploration;
    }

    [Serializable]
    public sealed class ResourceLedger
    {
        // 陣列順序固定為健康、體力、魔力、飽食度；本帳由實際餘額與分類收支推導。
        public float[] opening = new float[4], income = new float[4], expense = new float[4], closing = new float[4], residual = new float[4];
    }

    [Serializable]
    public sealed class VitalSample
    {
        public float time;
        public float[] resources;
        public string decision;
        public string[] recoveryStatus;
        public bool running;
        public float combatDamage, starvationDamage, foodReceived, reproductionSpent, nutritionSpent;
    }

    [Serializable]
    public sealed class DeathRecord
    {
        public int individual;
        public string species;
        public string cause;
        public float age;
        public float time;
        public bool initialAdult;
        public float observedSeconds, historyFromTime;
        public float combatDamage, starvationDamage, lastAteAt, lastReproducedAt, lastNutritionAt, lastCombatDamageAt;
        public float basalSpent, activityStaminaSpent, skillManaSpent, reproductionSpent, foodReceived, nutritionSpent, agingHealthLost, careGiven, careReceived;
        public float[] automaticRecovered, nutritionRecovered;
        public string lastCombatDamageKind, decisionReason;
        public int lastCombatDamageSource;
        public ResourceLedger ledger;
        public VitalSample[] history;
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
        public float completedObservedTotal;
    }

    public readonly struct WorldAction
    {
        public readonly Vector2 movement;
        public readonly bool forage;
        public readonly int feedChild;
        public readonly bool run;
        public readonly Vector2 aim;
        public readonly bool attack;
        public readonly WorldSkill skill;
        public readonly int turn;
        public readonly int skillSlot;
        public WorldAction(Vector2 movement, bool forage = false, Vector2 aim = default, bool attack = false, WorldSkill skill = WorldSkill.None, int turn = 0, int skillSlot = -1, bool run = false, int feedChild = 0)
        {
            this.movement = movement;
            this.forage = forage;
            this.feedChild = feedChild;
            this.run = run;
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
        public List<FruitState> fruits = new List<FruitState>();
        public FruitLedger fruitLedger = new FruitLedger();
        public int nextFruit;
        public List<WorldEvent> events = new List<WorldEvent>();
        public List<RewardRecord> rewards = new List<RewardRecord>();
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
        public float growth = 1f;
    }

    [Serializable]
    public sealed class ProjectileState
    {
        public WorldSkill skill;
        public List<int> hitIndividuals = new List<int>();
        public float damage;
        public float growth = 1f;
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
            foreach (string species in string.IsNullOrEmpty(Config.initialSpecies) ? Species : new[] { Config.initialSpecies })
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
            RefreshPerception(awardExploration: true);
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
                health = CombatRules.MaxHealth(species) * Config.initialHealthFraction,
                satiety = Config.initialSatiety, stamina = Config.initialStamina, mana = Config.initialMana,
                age = Config.foraging ? Config.maturityAge : 0,
                bornAt = Config.foraging ? State.time - Config.maturityAge : State.time,
                x = (float)(random.NextDouble() - 0.5) * Config.halfExtent,
                y = (float)(random.NextDouble() - 0.5) * Config.halfExtent,
                facing = (float)random.NextDouble() * 360f
            };
            CaptureResourceBaseline(individual, State.time, true);
            individual.loadout = CreateLoadout(species, balancedLoadout);
            individual.growth = CombatRules.Growth(Config, individual); individual.maxHealth = CombatRules.CurrentMaxHealth(Config, individual);
            State.individuals.Add(individual);
            return individual;
        }

        internal static void CaptureResourceBaseline(IndividualState individual, float time, bool initialAdult)
        {
            individual.initialAdult = initialAdult;
            individual.observedStart = time;
            individual.ledger = new ResourceLedger { opening = Resources(individual), closing = Resources(individual) };
            individual.recentHistory.Clear();
        }

        private static float[] Resources(IndividualState individual) => new[] { individual.health, individual.stamina, individual.mana, individual.satiety };

        private static void CaptureDiagnostics(IndividualState individual, float time)
        {
            individual.observedSeconds = Mathf.Max(0, time - individual.observedStart);
            var account = new ResourceLedger { opening = individual.ledger.opening, closing = Resources(individual) };
            for (int i = 0; i < 3; i++) account.income[i] = individual.automaticRecovered[i] + individual.nutritionRecovered[i];
            account.income[3] = individual.foodReceived + individual.careReceived;
            account.expense[0] = individual.combatDamageTaken + individual.starvationDamageTaken + individual.agingHealthLost;
            account.expense[1] = individual.activityStaminaSpent;
            account.expense[2] = individual.skillManaSpent;
            account.expense[3] = individual.basalSpent + individual.nutritionSpent + individual.reproductionSpent + individual.careGiven;
            for (int i = 0; i < 4; i++) account.residual[i] = account.closing[i] - (account.opening[i] + account.income[i] - account.expense[i]);
            individual.ledger = account;
            var history = individual.recentHistory;
            if (history.Count > 0 && individual.health > 0 && time - history[history.Count - 1].time < 1f) return;
            history.Add(new VitalSample { time = time, resources = Resources(individual), decision = individual.decisionReason,
                running = individual.running, recoveryStatus = (string[])individual.recoveryStatus.Clone(), combatDamage = individual.combatDamageTaken, starvationDamage = individual.starvationDamageTaken,
                foodReceived = individual.foodReceived, reproductionSpent = individual.reproductionSpent, nutritionSpent = individual.nutritionSpent });
            if (history.Count > 32) history.RemoveAt(0);
        }

        public void RemoveIndividual(int individual)
        {
            if (State.individuals.RemoveAll(value => value.individual == individual) != 1)
                throw new ArgumentException("個體不存在。");
        }

        // 固定結算：活動傷害、基礎消耗、採食、飢餓、自動恢復、營養恢復。
        private void RecoverVitals(IndividualState individual, float time)
        {
            if (individual.health <= 0) return;
            float maximum = CombatRules.CurrentMaxHealth(Config, individual);
            float[] caps = { maximum, 100f, 100f };
            float[] values = { individual.health, individual.stamina, individual.mana };
            bool[] allowed = { individual.satiety > 0 && time - individual.lastCombatAt >= Config.healthRecoveryDelay,
                time - individual.lastStrenuousAt >= Config.staminaRecoveryDelay, true };
            float[] rates = { maximum * Config.healthRecoveryFraction, Config.staminaRecoveryRate, Config.manaRecoveryRate };
            float[] efficiency = { maximum * Config.nutritionHealthFraction, Config.nutritionStamina, Config.nutritionMana };
            for (int i = 0; i < 3; i++)
            {
                if (values[i] < caps[i] * Config.nutritionStart) individual.nutritionActive[i] = true;
                float gain = allowed[i] ? Mathf.Min(caps[i] - values[i], rates[i] * Config.stepSeconds) : 0;
                values[i] += gain; individual.automaticRecovered[i] += gain;
                if (values[i] >= caps[i] * Config.nutritionStop) individual.nutritionActive[i] = false;
            }
            float total = 0;
            float[] weights = new float[3];
            for (int i = 0; i < 3; i++)
            {
                individual.recoveryStatus[i] = !individual.nutritionActive[i] ? "inactive" : !allowed[i] || individual.satiety <= Config.nutritionReserve ? "paused" : "active";
                if (individual.nutritionActive[i] && allowed[i] && efficiency[i] > 0)
                    total += weights[i] = Mathf.Max(0, Config.nutritionStop - values[i] / caps[i]);
            }
            float budget = Mathf.Min(Config.nutritionBudget * Config.stepSeconds, Mathf.Max(0, individual.satiety - Config.nutritionReserve));
            for (int i = 0; i < 3; i++)
            {
                if (weights[i] <= 0 || total <= 0) continue;
                float gain = Mathf.Min(caps[i] * Config.nutritionStop - values[i], budget * weights[i] / total * efficiency[i]);
                values[i] += gain; float spent = gain / efficiency[i];
                individual.satiety -= spent; individual.nutritionSpent += spent;
                if (spent > 0) individual.lastNutritionAt = time; individual.nutritionRecovered[i] += gain;
                if (values[i] >= caps[i] * Config.nutritionStop) { individual.nutritionActive[i] = false; individual.recoveryStatus[i] = "inactive"; }
            }
            individual.health = Mathf.Clamp(values[0], 0, maximum);
            individual.stamina = Mathf.Clamp(values[1], 0, 100); individual.mana = Mathf.Clamp(values[2], 0, 100);
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
            var priorCare = new Dictionary<int, int>();
            var canStartCare = new HashSet<int>();
            foreach (var individual in State.individuals)
            {
                priorCare[individual.individual] = individual.careTarget;
                var child = State.individuals.Find(value => value.individual == actions[individual.individual].feedChild);
                if (child != null && CanSee(individual, child)) canStartCare.Add(individual.individual);
                individual.careTarget = individual.careReceivedFrom = 0;
                individual.feeding = false;
                individual.fedAmount = 0;
                individual.fedSourceX = individual.fedSourceY = 0;
            }
            State.events.Clear();
            foreach (var individual in State.individuals)
            {
                if (string.IsNullOrEmpty(individual.protectionEvent)) continue;
                State.events.Add(new WorldEvent { kind = individual.protectionEvent, individual = individual.individual,
                    target = individual.protectionEventThreat, child = individual.protectionEventChild });
                individual.protectionEvent = "";
            }
            State.rewards.Clear();
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
                individual.facing = Mathf.Repeat(individual.facing + ResolveTurn(individual, actions[individual.individual]) * 180f * Config.stepSeconds, 360f);
                if (!Config.foraging) individual.boundary = position != bounded;
                distances[individual.individual] = Vector2.Distance(start, bounded);
            }
            if (Config.foraging)
            {
                CombatRules.Resolve(Config, State, actions);
                ControlEffects.Resolve(Config, State, effectRandom);
            }
            if (Config.foraging)
            {
                // 先共同結算老化與基本消耗，再結算餵養，避免順序影響收支與採食互斥。
                foreach (var individual in State.individuals)
                {
                    if (!Config.disableAging) individual.age = time - individual.bornAt;
                    float basal = Mathf.Min(individual.satiety, Config.idleCost * Config.stepSeconds);
                    individual.satiety -= basal; individual.basalSpent += basal;
                }
                foreach (var parent in State.individuals)
                {
                    var action = actions[parent.individual];
                    var child = State.individuals.Find(value => value.individual == action.feedChild);
                    bool continued = priorCare[parent.individual] == action.feedChild && action.feedChild != 0;
                    if (child == null || child.parentId != parent.individual || parent.health <= 0 || child.health <= 0 ||
                        (!Config.disableAging && (parent.age >= Config.lifespan || child.age >= Config.lifespan)) ||
                        child.age >= Config.maturityAge || action.attack || action.skill != WorldSkill.None || action.skillSlot >= 0 ||
                        parent.windupActiveThisStep || parent.chargeActiveThisStep || parent.dashActiveThisStep ||
                        (!continued && !canStartCare.Contains(parent.individual)) ||
                        Vector2.Distance(CombatRules.Position(parent), CombatRules.Position(child)) > Config.careRadius) continue;
                    float amount = Mathf.Min(Config.careRate * Config.stepSeconds, Mathf.Max(0, parent.satiety - Config.careReserve), 100f - child.satiety);
                    if (amount <= 0) continue;
                    parent.satiety -= amount; child.satiety += amount;
                    parent.careGiven += amount; child.careReceived += amount;
                    var knownChild = parent.children.Find(value => value.child == child.individual);
                    if (knownChild != null && (!continued || knownChild.visible)) parent.careEstimatedSatiety = knownChild.satiety;
                    parent.careEstimatedSatiety = Mathf.Min(100f, parent.careEstimatedSatiety + amount);
                    parent.careTarget = child.individual; child.careReceivedFrom = parent.individual;
                    if (!continued) State.events.Add(new WorldEvent { kind = "care_started", individual = parent.individual, target = child.individual });
                    State.events.Add(new WorldEvent { kind = "care_feed", individual = parent.individual, target = child.individual, amount = amount });
                }
                foreach (var parent in State.individuals)
                    if (priorCare[parent.individual] != 0 && priorCare[parent.individual] != parent.careTarget)
                        State.events.Add(new WorldEvent { kind = "care_stopped", individual = parent.individual, target = priorCare[parent.individual] });
            }
            foreach (var individual in State.individuals)
            {
                if (!Config.foraging) continue;
                individual.reproductionCooldown = Mathf.Max(0, individual.nextReproductionTime - time);
                float distance = distances[individual.individual];
                if (individual.health > 0 && individual.satiety <= 0)
                {
                    float damage = Mathf.Min(individual.health, CombatRules.CurrentMaxHealth(Config, individual) * Config.starvationDamage * Config.stepSeconds);
                    individual.health -= damage; individual.starvationDamageTaken += damage;
                    State.events.Add(new WorldEvent { kind = "starvation_damage", individual = individual.individual, amount = damage });
                    individual.lastDamageCause = "starved";
                }
                CombatRules.Recover(Config, State, individual, time);
                RecoverVitals(individual, time);
                string cause = individual.health <= 0 ? individual.lastDamageCause : !Config.disableAging && individual.age >= Config.lifespan ? "old_age" : "";
                if (cause != "")
                {
                    if (cause == "old_age") individual.agingHealthLost += individual.health;
                    individual.health = 0;
                    CaptureDiagnostics(individual, time);
                    State.events.Add(new WorldEvent { kind = cause, individual = individual.individual,
                        species = individual.species, cause = cause, age = individual.age });
                    State.deaths.Add(new DeathRecord { individual = individual.individual, species = individual.species,
                        cause = cause, age = individual.age, time = time, initialAdult = individual.initialAdult,
                        observedSeconds = individual.observedSeconds, historyFromTime = individual.recentHistory.Count == 0 ? time : individual.recentHistory[0].time,
                        combatDamage = individual.combatDamageTaken, starvationDamage = individual.starvationDamageTaken,
                        lastAteAt = individual.lastAteAt, lastReproducedAt = individual.lastReproducedAt, lastNutritionAt = individual.lastNutritionAt,
                        lastCombatDamageKind = individual.lastCombatDamageKind, lastCombatDamageSource = individual.lastCombatDamageSource, lastCombatDamageAt = individual.lastCombatDamageAt,
                        decisionReason = individual.decisionReason, ledger = individual.ledger, history = individual.recentHistory.ToArray(),
                        basalSpent = individual.basalSpent, activityStaminaSpent = individual.activityStaminaSpent, skillManaSpent = individual.skillManaSpent,
                        reproductionSpent = individual.reproductionSpent, foodReceived = individual.foodReceived, nutritionSpent = individual.nutritionSpent,
                        careGiven = individual.careGiven, careReceived = individual.careReceived,
                        automaticRecovered = individual.automaticRecovered, nutritionRecovered = individual.nutritionRecovered, agingHealthLost = individual.agingHealthLost });
                    var statistics = Statistics(individual.species);
                    if (cause == "starved") statistics.starved++;
                    else if (cause == "combat") statistics.combat++;
                    else statistics.oldAge++;
                    statistics.completedAgeTotal += individual.age;
                    statistics.completedObservedTotal += individual.observedSeconds;
                }
            }
            if (Config.foraging)
            {
                // 僅在死亡位置可見時，親代才知道子女死亡。
                foreach (var child in State.individuals)
                {
                    if (child.health > 0 || child.parentId == 0) continue;
                    var parent = State.individuals.Find(value => value.individual == child.parentId && value.health > 0);
                    var known = parent?.children.Find(value => value.child == child.individual);
                    if (known != null && CanSeePoint(parent, CombatRules.Position(child)))
                    { known.confirmedDead = true; known.visible = false; known.x = child.x; known.y = child.y; known.age = child.age; known.satiety = child.satiety; known.lastSeenAt = time; }
                }
                State.individuals.RemoveAll(value => value.health <= 0);
                // 本步新生個體不再行動或繁殖；先結算死亡，再分配可用人口名額。
                var parents = State.individuals.ToArray();
                foreach (var parent in parents)
                {
                    if (Config.disableReproduction || parent.age < Config.maturityAge || parent.satiety < Config.reproductionSatiety ||
                        parent.health < CombatRules.MaxHealth(parent.species) * Config.reproductionHealthFraction ||
                        parent.satiety - Config.reproductionCost <= Config.reproductionReserve ||
                        parent.reproductionCooldown > 0) continue;
                    var statistics = Statistics(parent.species);
                    if (State.individuals.Count >= Config.populationLimit)
                    {
                        statistics.populationLimitHits++;
                        State.events.Add(new WorldEvent { kind = "population_limit", individual = parent.individual, species = parent.species });
                        continue;
                    }
                    var child = AddIndividual(parent.species);
                    child.parentId = parent.individual;
                    child.x = parent.x;
                    child.y = parent.y;
                    child.age = 0;
                    child.health = CombatRules.CurrentMaxHealth(Config, child); child.stamina = child.mana = 100f;
                    child.satiety = Config.newbornSatiety;
                    child.bornAt = time;
                    child.growth = CombatRules.Growth(Config, child); child.maxHealth = CombatRules.CurrentMaxHealth(Config, child);
                    CaptureResourceBaseline(child, time, false);
                    parent.children.Add(new FamilyMemory { child = child.individual, x = child.x, y = child.y,
                        age = child.age, satiety = child.satiety, lastSeenAt = time, visible = true });
                    parent.satiety -= Config.reproductionCost; parent.reproductionSpent += Config.reproductionCost; parent.lastReproducedAt = time;
                    parent.nextReproductionTime = time + Config.reproductionCooldown;
                    parent.reproductionCooldown = Config.reproductionCooldown;
                    statistics.births++;
                    State.events.Add(new WorldEvent { kind = "born", individual = child.individual,
                        parent = parent.individual, species = child.species, amount = Config.reproductionCost });
                }
                UpdateIncompleteLives();
            }
            foreach (var individual in State.individuals) { individual.growth = CombatRules.Growth(Config, individual); individual.maxHealth = CombatRules.CurrentMaxHealth(Config, individual); CaptureDiagnostics(individual, time); }
            State.step++;
            State.time = State.step * Config.stepSeconds;
            RefreshPerception(awardExploration: true);
            foreach (var individual in State.individuals)
                State.rewards.Add(new RewardRecord { individual = individual.individual,
                    exploration = individual.explorationRewardPending });
            State.endReason = State.individuals.Count == 0 ? "all_dead" : State.step >= Config.episodeSteps ? "time_limit" : "";
            return State;
        }

        private SpeciesStatistics Statistics(string species) => State.statistics.Find(value => value.species == species);

        private int ResolveTurn(IndividualState individual, WorldAction action)
        {
            individual.rawTurn = action.turn;
            individual.actualTurn = action.turn;
            individual.scanIntervened = false;
            individual.scanDeferred = false;
            individual.scanReason = "";
            if (!Config.foraging || !Config.scanEnabled) return action.turn;
            if (action.turn != 0)
            {
                individual.scanTargetSector = -1;
                individual.scanDeferredSeconds = 0;
                return action.turn;
            }
            int oldest = 0;
            for (int sector = 1; sector < individual.directionLastSeenAt.Length; sector++)
                if (individual.directionLastSeenAt[sector] < individual.directionLastSeenAt[oldest]) oldest = sector;
            if (State.time - individual.directionLastSeenAt[oldest] < Config.scanFreshnessSeconds)
            {
                individual.scanTargetSector = -1;
                individual.scanDeferredSeconds = 0;
                return 0;
            }
            var threat = individual.perception.Find(value => value.species != individual.species &&
                Vector2.Distance(CombatRules.Position(individual), new Vector2(value.x, value.y)) <= Config.scanThreatRadius);
            string reason = individual.satiety <= Config.scanEnergyThreshold ? ScanReasons.Energy
                : individual.dashRemaining > 0 ? ScanReasons.Escape
                : threat != null ? ScanReasons.Threat : "";
            if (reason != "")
            {
                individual.scanDeferred = true;
                individual.scanReason = reason;
                individual.scanDeferredSeconds += Config.stepSeconds;
                return 0;
            }
            individual.scanDeferredSeconds = 0;
            if (action.movement.sqrMagnitude > 0.04f) return 0;
            if (individual.scanTargetSector < 0 ||
                State.time - individual.directionLastSeenAt[individual.scanTargetSector] < Config.scanFreshnessSeconds)
                individual.scanTargetSector = oldest;
            float delta = Mathf.DeltaAngle(individual.facing, individual.scanTargetSector * 45f);
            if (Mathf.Abs(delta) < 1f) return 0;
            individual.actualTurn = delta > 0 ? 1 : -1;
            individual.scanIntervened = true;
            individual.scanReason = ScanReasons.StaleDirection;
            return individual.actualTurn;
        }

        private FoodGrowthConfig Growth(FoodState food) => food.growthType == FoodGrowthType.Large ? Config.largeFood : Config.smallFood;

        private void AdvanceFood(float time)
        {
            FruitRules.Advance(Config, State, random, time);
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
            FruitRules.Initialize(Config, food, time);
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
                individual.facing / 360f, Mathf.Clamp01(individual.satiety / 100f),
                (int)individual.loadout[0] / 12f, (int)individual.loadout[1] / 12f,
                individual.health / CombatRules.CurrentMaxHealth(Config, individual),
                individual.skillCooldowns[0] / 10f, individual.skillCooldowns[1] / 10f,
                individual.stamina / 100f, individual.mana / 100f,
                individual.nutritionActive[0] ? 1f : 0f, individual.nutritionActive[1] ? 1f : 0f, individual.nutritionActive[2] ? 1f : 0f,
                individual.recoveryStatus[0] == "paused" ? 1f : 0f, individual.recoveryStatus[1] == "paused" ? 1f : 0f, individual.recoveryStatus[2] == "paused" ? 1f : 0f
            };
        }

        public bool CanSee(IndividualState observer, IndividualState target)
        {
            return target != observer && target.health > 0 && CanSeePoint(observer, CombatRules.Position(target));
        }

        public bool CanSeePoint(IndividualState observer, Vector2 point) => InView(Config, observer, point);

        public static bool InView(WorldConfig config, IndividualState observer, Vector2 point)
        {
            Vector2 delta = point - CombatRules.Position(observer);
            Vector2 facing = new Vector2(Mathf.Cos(observer.facing * Mathf.Deg2Rad), Mathf.Sin(observer.facing * Mathf.Deg2Rad));
            return delta.sqrMagnitude <= config.observationRadius * config.observationRadius
                && (delta.sqrMagnitude == 0 || Vector2.Angle(facing, delta) <= 60f);
        }

        public bool CanSeeCircle(IndividualState observer, Vector2 center, float radius)
        {
            Vector2 delta = center - CombatRules.Position(observer);
            float distance = delta.magnitude;
            float reach = Config.observationRadius;
            if (distance > reach + radius + 0.00001f) return false;
            float angle = Mathf.Abs(Mathf.DeltaAngle(observer.facing, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg));
            if (angle <= 60f) return distance <= reach + radius + 0.00001f;
            // 圓心在扇形外時，最近點位於兩條徑向線段之一（包含原點與圓弧端點）。
            for (int sign = -1; sign <= 1; sign += 2)
            {
                float edge = (observer.facing + sign * 60f) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(edge), Mathf.Sin(edge));
                Vector2 nearest = direction * Mathf.Clamp(Vector2.Dot(delta, direction), 0, reach);
                if ((delta - nearest).sqrMagnitude <= radius * radius + 0.00001f) return true;
            }
            return false;
        }

        private void RefreshSmokeKnowledge(IndividualState observer)
        {
            observer.smokeKnowledgeAvailable = true;
            foreach (var known in observer.smokeKnowledge) known.visible = false;
            foreach (var area in State.areas)
            {
                if (area.skill != WorldSkill.SporeSmoke || area.until <= State.time ||
                    !CanSeeCircle(observer, new Vector2(area.x, area.y), area.radius)) continue;
                var known = observer.smokeKnowledge.Find(value => value.area == area.area);
                if (known == null) observer.smokeKnowledge.Add(known = new SmokeSighting { area = area.area });
                known.x = area.x; known.y = area.y; known.radius = area.radius;
                known.harmful = area.species != observer.species;
                known.visible = true; known.lastSeenAt = State.time;
            }
            observer.smokeKnowledge.RemoveAll(value => !value.visible && (State.time - value.lastSeenAt >= 5f ||
                CanSeeCircle(observer, new Vector2(value.x, value.y), value.radius)));
            // 共同觀察與規則使用同一份有界情報；依可見、有害、距離、時間排序。
            observer.smokeKnowledge.Sort((a, b) =>
            {
                int order = b.visible.CompareTo(a.visible);
                if (order == 0) order = b.harmful.CompareTo(a.harmful);
                if (order == 0) order = Vector2.SqrMagnitude(new Vector2(a.x - observer.x, a.y - observer.y))
                    .CompareTo(Vector2.SqrMagnitude(new Vector2(b.x - observer.x, b.y - observer.y)));
                if (order == 0) order = b.lastSeenAt.CompareTo(a.lastSeenAt);
                return order == 0 ? a.area.CompareTo(b.area) : order;
            });
            if (observer.smokeKnowledge.Count > 64) observer.smokeKnowledge.RemoveRange(64, observer.smokeKnowledge.Count - 64);
        }

        public void RefreshPerception(bool clearMemory = false, bool awardExploration = false)
        {
            if (clearMemory)
            {
                enemyMemory.Clear();
                foreach (var individual in State.individuals)
                {
                    individual.areaMemory.Clear();
                    individual.foodMemory.Clear();
                    individual.smokeKnowledge.Clear();
                    individual.visibleFoods.Clear();
                    individual.rewardedCells.Clear();
                    individual.explorationRewardTotal = individual.explorationRewardPending = 0;
                    Array.Clear(individual.directionLastSeenAt, 0, individual.directionLastSeenAt.Length);
                    individual.explorationTargetX = individual.explorationTargetY = -1;
                    individual.scanTargetSector = -1;
                }
            }
            var alive = new HashSet<int>();
            foreach (var observer in State.individuals)
            {
                alive.Add(observer.individual);
                RefreshSmokeKnowledge(observer);
                if (!enemyMemory.TryGetValue(observer.individual, out var memory))
                    enemyMemory.Add(observer.individual, memory = new Dictionary<int, EnemySighting>());
                observer.perception.Clear();
                foreach (var child in observer.children) child.visible = false;
                var visible = new HashSet<int>();
                foreach (var target in State.individuals)
                {
                    if (!CanSee(observer, target)) continue;
                    visible.Add(target.individual);
                    if (target.parentId == observer.individual)
                    {
                        var child = observer.children.Find(value => value.child == target.individual);
                        if (child == null) observer.children.Add(child = new FamilyMemory { child = target.individual });
                        child.x = target.x; child.y = target.y; child.age = target.age; child.satiety = target.satiety;
                        child.lastSeenAt = State.time; child.visible = true; child.confirmedDead = false; child.searched = false;
                        child.threatened = State.individuals.Exists(enemy => enemy.health > 0 && enemy.species != observer.species &&
                            CanSee(observer, enemy) && Vector2.Distance(CombatRules.Position(target), CombatRules.Position(enemy)) <= Config.protectionRadius);
                    }
                    observer.perception.Add(new EntityObservation { individual = target.individual, species = target.species, x = target.x, y = target.y,
                        visible = true, velocityX = target.velocityX, velocityY = target.velocityY,
                        feeding = target.feeding && CanSeePoint(observer, new Vector2(target.fedSourceX, target.fedSourceY)),
                        sourceX = target.feeding && CanSeePoint(observer, new Vector2(target.fedSourceX, target.fedSourceY)) ? target.fedSourceX : 0,
                        sourceY = target.feeding && CanSeePoint(observer, new Vector2(target.fedSourceX, target.fedSourceY)) ? target.fedSourceY : 0 });
                    if (target.species != observer.species)
                        memory[target.individual] = new EnemySighting { species = target.species, position = CombatRules.Position(target), lastSeenAt = State.time };
                }
                foreach (var child in observer.children)
                {
                    float elapsed = Mathf.Max(0, State.time - child.lastSeenAt);
                    child.predictedAge = child.age + (Config.disableAging || Config.disableMemoryPrediction ? 0 : elapsed);
                    child.predictedSatiety = Config.disableMemoryPrediction ? child.satiety : Mathf.Max(0, child.satiety - elapsed * Config.idleCost);
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
                if (Config.foraging)
                {
                    RefreshFoodMemory(observer);
                    RefreshAreaMemory(observer, awardExploration);
                    for (int sector = 0; sector < observer.directionLastSeenAt.Length; sector++)
                        if (Mathf.Abs(Mathf.DeltaAngle(observer.facing, sector * 45f)) <= 60f)
                            observer.directionLastSeenAt[sector] = State.time;
                }
            }
            var removed = new List<int>();
            foreach (int id in enemyMemory.Keys) if (!alive.Contains(id)) removed.Add(id);
            foreach (int id in removed) enemyMemory.Remove(id);
        }

        public Vector2 ExplorationCellCenter(int x, int y) => new Vector2(
            Mathf.Min(Config.halfExtent, -Config.halfExtent + (x + 0.5f) * Config.explorationCellSize),
            Mathf.Min(Config.halfExtent, -Config.halfExtent + (y + 0.5f) * Config.explorationCellSize));

        private void RefreshAreaMemory(IndividualState observer, bool awardExploration)
        {
            int columns = Mathf.CeilToInt(Config.halfExtent * 2 / Config.explorationCellSize);
            int minX = Mathf.Max(0, Mathf.FloorToInt((observer.x - Config.observationRadius + Config.halfExtent) / Config.explorationCellSize));
            int maxX = Mathf.Min(columns - 1, Mathf.FloorToInt((observer.x + Config.observationRadius + Config.halfExtent) / Config.explorationCellSize));
            int minY = Mathf.Max(0, Mathf.FloorToInt((observer.y - Config.observationRadius + Config.halfExtent) / Config.explorationCellSize));
            int maxY = Mathf.Min(columns - 1, Mathf.FloorToInt((observer.y + Config.observationRadius + Config.halfExtent) / Config.explorationCellSize));
            for (int x = minX; x <= maxX; x++)
                for (int y = minY; y <= maxY; y++)
                {
                    if (!CanSeePoint(observer, ExplorationCellCenter(x, y))) continue;
                    if (awardExploration)
                    {
                        int cell = x * columns + y;
                        if (!observer.rewardedCells.Contains(cell))
                        {
                            observer.rewardedCells.Add(cell);
                            float amount = Mathf.Min(Config.explorationRewardPerCell,
                                Config.explorationRewardLimit - observer.explorationRewardTotal);
                            if (amount > 0)
                            {
                                observer.explorationRewardTotal += amount;
                                observer.explorationRewardPending += amount;
                            }
                        }
                    }
                    var known = observer.areaMemory.Find(value => value.cellX == x && value.cellY == y);
                    if (known == null) observer.areaMemory.Add(new AreaMemory { cellX = x, cellY = y, lastSeenAt = State.time });
                    else known.lastSeenAt = State.time;
                }
            if (observer.areaMemory.Count <= Config.areaMemoryLimit) return;
            observer.areaMemory.Sort((a, b) => AreaPriority(observer, a).CompareTo(AreaPriority(observer, b)));
            observer.areaMemory.RemoveRange(0, observer.areaMemory.Count - Config.areaMemoryLimit);
        }

        private float AreaPriority(IndividualState observer, AreaMemory area)
        {
            Vector2 center = ExplorationCellCenter(area.cellX, area.cellY);
            bool usefulFood = observer.foodMemory.Exists(value => value.mature && value.phase == FoodPhase.Available &&
                Vector2.Distance(center, new Vector2(value.x, value.y)) <= Config.explorationCellSize);
            bool threat = observer.perception.Exists(value => value.species != observer.species &&
                Vector2.Distance(center, new Vector2(value.x, value.y)) <= Config.explorationCellSize);
            return area.lastSeenAt + (usefulFood ? Config.areaMemorySeconds : 0) + (threat ? Config.areaMemorySeconds : 0);
        }

        private void RefreshFoodMemory(IndividualState observer)
        {
            observer.visibleFoods.Clear();
            foreach (var known in observer.foodMemory)
            {
                known.visible = false;
                known.possiblyAvailable = !Config.disableMemoryPrediction && (!known.mature || known.phase != FoodPhase.Available) &&
                    State.time - known.lastSeenAt >= Config.foodPredictionSeconds;
            }
            foreach (var food in State.foods)
            {
                if (!CanSeePoint(observer, new Vector2(food.x, food.y))) continue;
                var known = observer.foodMemory.Find(value => value.food == food.food);
                if (known == null) observer.foodMemory.Add(known = new FoodMemory { food = food.food });
                known.x = food.x;
                known.y = food.y;
                known.amount = food.amount;
                known.capacity = food.capacity;
                known.mature = food.mature;
                known.phase = food.phase;
                known.visible = true;
                known.possiblyAvailable = false;
                known.lastSeenAt = State.time;
                observer.visibleFoods.Add(known);
            }
            observer.foodMemory.RemoveAll(value => !value.visible && CanSeePoint(observer, new Vector2(value.x, value.y)));
            if (observer.foodMemory.Count <= Config.foodMemoryLimit) return;
            observer.foodMemory.Sort((a, b) => FoodPriority(a).CompareTo(FoodPriority(b)));
            observer.foodMemory.RemoveRange(0, observer.foodMemory.Count - Config.foodMemoryLimit);
        }

        private float FoodPriority(FoodMemory food) => food.lastSeenAt +
            (food.visible ? Config.foodMemorySeconds : 0) +
            (food.mature && food.phase == FoodPhase.Available && food.amount > 0 ? Config.foodMemorySeconds : 0);

        public IEnumerable<float[]> ObserveEntities(IndividualState individual)
        {
            foreach (var smoke in individual.smokeKnowledge)
                yield return new[] { -5f, Mathf.Clamp((smoke.x - individual.x) / (Config.halfExtent * 2), -1f, 1f),
                    Mathf.Clamp((smoke.y - individual.y) / (Config.halfExtent * 2), -1f, 1f),
                    smoke.radius / (Config.halfExtent * 2), smoke.harmful ? 1f : 0f, smoke.visible ? 1f : 0f,
                    Mathf.Clamp01((State.time - smoke.lastSeenAt) / 5f), 0f, 0f, 0f };
            foreach (var entity in individual.perception)
                yield return new[] { (SpeciesIndex(entity.species) + 1) / 3f,
                    (entity.x - individual.x) / Config.observationRadius, (entity.y - individual.y) / Config.observationRadius,
                    entity.velocityX / 4f, entity.velocityY / 4f, entity.visible ? 1f : 0f, entity.age / 5f,
                    entity.visible && entity.feeding ? 1f : 0f,
                    entity.visible && entity.feeding ? (entity.sourceX - individual.x) / (Config.halfExtent * 2) : 0f,
                    entity.visible && entity.feeding ? (entity.sourceY - individual.y) / (Config.halfExtent * 2) : 0f };
            foreach (var food in State.foods)
            {
                var delta = new Vector2(food.x - individual.x, food.y - individual.y);
                if (!CanSeePoint(individual, new Vector2(food.x, food.y))) continue;
                yield return new[] { 0f, delta.x / (Config.halfExtent * 2), delta.y / (Config.halfExtent * 2),
                    1f, 0f, food.mature && food.phase == FoodPhase.Available ? 1f : 0f, food.amount / food.capacity, 0f, 0f, 0f };
            }
            foreach (var food in individual.foodMemory)
            {
                if (food.visible) continue;
                yield return new[] { 0f, Mathf.Clamp((food.x - individual.x) / (Config.halfExtent * 2), -1f, 1f),
                    Mathf.Clamp((food.y - individual.y) / (Config.halfExtent * 2), -1f, 1f), 0f,
                    Mathf.Clamp01((State.time - food.lastSeenAt) / Config.foodMemorySeconds),
                    food.mature && food.phase == FoodPhase.Available ? 1f : 0f,
                    food.capacity > 0 ? food.amount / food.capacity : 0f, food.possiblyAvailable ? 1f : 0f, 0f, 0f };
            }
            foreach (var child in individual.children)
                yield return new[] { -2f, Mathf.Clamp((child.x - individual.x) / (Config.halfExtent * 2), -1f, 1f),
                    Mathf.Clamp((child.y - individual.y) / (Config.halfExtent * 2), -1f, 1f), child.visible ? 1f : 0f,
                    Mathf.Clamp01((State.time - child.lastSeenAt) / Config.maturityAge), child.predictedAge / Config.maturityAge,
                    child.predictedSatiety / 100f, child.confirmedDead ? 1f : 0f, child.threatened ? 1f : 0f, child.child };
            if (individual.careTarget != 0 || individual.careReceivedFrom != 0)
                yield return new[] { -4f, 0f, 0f, individual.careTarget != 0 ? 1f : 0f,
                    individual.careReceivedFrom != 0 ? 1f : 0f, 0f, 0f, 0f, 0f,
                    (float)(individual.careTarget != 0 ? individual.careTarget : individual.careReceivedFrom) };
            if (individual.parentId != 0)
            {
                var parent = State.individuals.Find(value => value.individual == individual.parentId && CanSee(individual, value));
                if (parent != null)
                    yield return new[] { -3f, (parent.x - individual.x) / (Config.halfExtent * 2), (parent.y - individual.y) / (Config.halfExtent * 2),
                        1f, 0f, 0f, 0f, 0f, 0f, (float)parent.individual };
            }
            foreach (var area in individual.areaMemory)
            {
                Vector2 center = ExplorationCellCenter(area.cellX, area.cellY);
                yield return new[] { -1f, (center.x - individual.x) / (Config.halfExtent * 2),
                    (center.y - individual.y) / (Config.halfExtent * 2), 0f,
                    Mathf.Clamp01((State.time - area.lastSeenAt) / Config.areaMemorySeconds), 0f, 0f, 0f, 0f, 0f };
            }
        }
    }
}
