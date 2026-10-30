using System;
using UnityEngine;

namespace Ecosystem
{
    // 來源共享完整生長圓；地面果實與生物不占用生長空間。
    public static class SourcePlacementRules
    {
        public static float Radius(WorldConfig config, FoodGrowthType type) =>
            (type == FoodGrowthType.Large ? config.largeFood : config.smallFood).growthRadius;

        public static bool CanPlace(WorldConfig config, WorldState state, Vector2 point, FoodGrowthType type)
        {
            float radius = Radius(config, type);
            if (!float.IsFinite(point.x) || !float.IsFinite(point.y) ||
                Mathf.Abs(point.x) + radius > config.halfExtent || Mathf.Abs(point.y) + radius > config.halfExtent ||
                state.foods.Count >= Mathf.Min(64, config.foodSourceLimit)) return false;
            foreach (var food in state.foods)
                if (Vector2.Distance(point, new Vector2(food.x, food.y)) < radius + Radius(config, food.growthType)) return false;
            return true;
        }

        public static FoodState Create(WorldConfig config, WorldState state, Vector2 point, FoodGrowthType type, bool natural, float time)
        {
            if (!CanPlace(config, state, point, type)) return null;
            var food = new FoodState { food = ++state.nextFood, natural = natural, patch = type == FoodGrowthType.Large,
                growthType = type, x = point.x, y = point.y };
            FruitRules.Initialize(config, food, time);
            state.foods.Add(food);
            return food;
        }

        public static void Initialize(WorldConfig config, WorldState state, System.Random random)
        {
            int target = Mathf.Min(config.naturalFoodLimit, config.foodSourceLimit, 64);
            int orientation = random.Next(8);
            Vector2 Transform(Vector2 point)
            {
                if ((orientation & 4) != 0) point.x = -point.x;
                for (int i = 0; i < (orientation & 3); i++) point = new Vector2(-point.y, point.x);
                return point;
            }
            if (config.halfExtent >= 20 && Radius(config, FoodGrowthType.Small) <= 4 && Radius(config, FoodGrowthType.Large) <= 5.5f)
            {
                foreach (float x in new[] { -5.5f, 5.5f }) foreach (float y in new[] { -5.5f, 5.5f })
                    if (state.foods.Count < target) Create(config, state, Transform(new Vector2(x, y)), FoodGrowthType.Large, true, 0);
                foreach (float y in new[] { -16f, 16f }) foreach (float x in new[] { -16f, -8f, 0f, 8f, 16f })
                    if (state.foods.Count < target) Create(config, state, Transform(new Vector2(x, y)), FoodGrowthType.Small, true, 0);
                foreach (float x in new[] { -16f, 16f })
                    if (state.foods.Count < target) Create(config, state, Transform(new Vector2(x, 0)), FoodGrowthType.Small, true, 0);
            }
            else
            {
                while (state.foods.Count < target && TryAddNatural(config, state, random, 0)) { }
            }
            foreach (var food in state.foods)
            {
                float lifetime = FruitRules.Growth(config, food).lifetimeSeconds;
                float age = Mathf.Min(lifetime - 0.0001f, (float)(random.NextDouble() * lifetime));
                FruitRules.Initialize(config, food, -age);
            }
            FruitRules.Advance(config, state, random, 0);
            if (state.fruits.Count == 0 && state.foods.Count > 0)
            {
                var food = state.foods[random.Next(state.foods.Count)];
                var growth = FruitRules.Growth(config, food);
                // 只有全場無果才調整一處年齡，仍依自然成熟重建，保留已經過的保存時間。
                float age = growth.firstMaturitySeconds + Mathf.Min(1f, growth.fruitingSeconds / 4f);
                if (age < growth.lifetimeSeconds)
                {
                    food.cycle = 0; food.lastFruitAt = -1000f;
                    FruitRules.Initialize(config, food, -age);
                    FruitRules.Advance(config, state, random, 0);
                }
            }
            state.fruitLedger = new FruitLedger { initial = state.fruits.Count };
            state.events.Clear();
            if (state.foods.Count < target)
                state.events.Add(new WorldEvent { kind = "food_initial_shortfall", amount = target - state.foods.Count });
            state.nextNaturalSpawnAt = config.naturalSpawnInterval;
        }

        public static bool TryAddNatural(WorldConfig config, WorldState state, System.Random random, float time)
        {
            for (int attempt = 0; attempt < 256; attempt++)
            {
                float extent = config.halfExtent - Radius(config, FoodGrowthType.Small);
                if (extent < 0) return false;
                var point = new Vector2((float)(random.NextDouble() * 2 - 1) * extent, (float)(random.NextDouble() * 2 - 1) * extent);
                if (TryCreate(config, state, random, point, true, 0.3f, time, out _)) return true;
            }
            return false;
        }

        public static bool TryCreate(WorldConfig config, WorldState state, System.Random random, Vector2 point,
            bool natural, float largeChance, float time, out FoodState food)
        {
            food = null;
            bool small = CanPlace(config, state, point, FoodGrowthType.Small);
            bool large = CanPlace(config, state, point, FoodGrowthType.Large);
            if (!small && !large) return false;
            var type = large && (!small || random.NextDouble() < largeChance) ? FoodGrowthType.Large : FoodGrowthType.Small;
            food = Create(config, state, point, type, natural, time);
            state.events.Add(new WorldEvent { kind = "food_spawned", food = food.food, x = point.x, y = point.y });
            return true;
        }
    }
}
