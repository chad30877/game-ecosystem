using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ecosystem
{
    [Serializable]
    public sealed class FruitState
    {
        public int fruit, source, holder;
        public float x, y, maturedAt, expiresAt;
        public bool NearRot(float time) => time >= expiresAt - 10f;
    }

    [Serializable]
    public sealed class FruitLedger
    {
        public int initial, matured, eaten, rotten, sourceExpired, delivered, dropped;
        public int Residual(int inventory) => initial + matured - inventory - eaten - rotten - sourceExpired;
    }

    // 所有期限使用世界時間；成長進度可加速，成熟果實與來源壽命不可加速。
    public static class FruitRules
    {
        public const float PreservationSeconds = 40f;
        public static FoodGrowthConfig Growth(WorldConfig config, FoodState food) =>
            food.growthType == FoodGrowthType.Large ? config.largeFood : config.smallFood;

        public static void Initialize(WorldConfig config, FoodState food, float bornAt)
        {
            food.bornAt = food.lastAdvancedAt = bornAt;
            food.phase = FoodPhase.Recovering;
            food.phaseProgress = food.amount = 0;
            food.mature = false;
            food.matureAt = bornAt + Growth(config, food).firstMaturitySeconds;
            food.capacity = Growth(config, food).maxFruit;
            food.cycleInitialized = true;
            food.maturitySchedule = Array.Empty<float>();
        }

        static void BeginCycle(FoodGrowthConfig growth, FoodState food, System.Random random)
        {
            food.phase = FoodPhase.Available;
            food.mature = true;
            food.phaseProgress = 0;
            food.nextMaturity = 0;
            food.cycle++;
            int count = random.Next(growth.minFruit, growth.maxFruit + 1);
            food.maturitySchedule = new float[count];
            for (int i = 1; i < count - 1; i++)
            {
                // 反正弦分位數使前後較疏、中段較密；擾動受相鄰間距約束。
                double t = (double)i / (count - 1);
                double fraction = Math.Acos(1 - 2 * t) / Math.PI;
                food.maturitySchedule[i] = (float)(growth.fruitingSeconds * fraction + (random.NextDouble() - 0.5) * 0.5);
            }
            food.maturitySchedule[count - 1] = growth.fruitingSeconds;
        }

        public static void Advance(WorldConfig config, WorldState state, System.Random random, float time)
        {
            // 到期先於任何食物操作完成；已採果不因來源死亡而移除。
            for (int i = state.fruits.Count - 1; i >= 0; i--)
            {
                var fruit = state.fruits[i];
                if (fruit.expiresAt > time) continue;
                state.fruits.RemoveAt(i);
                state.fruitLedger.rotten++;
                state.events.Add(new WorldEvent { kind = "fruit_rotten", fruit = fruit.fruit, food = fruit.source });
            }
            foreach (var food in state.foods)
            {
                if (!food.cycleInitialized) Initialize(config, food, food.bornAt);
                var growth = Growth(config, food);
                float expiry = food.bornAt + growth.lifetimeSeconds;
                float end = Mathf.Min(time, expiry);
                float cursor = food.lastAdvancedAt;
                float rate = Mathf.Clamp(food.growthMultiplier, 1f, 2f);
                while (cursor < end)
                {
                    if (food.phase == FoodPhase.Waiting)
                    {
                        if (food.waitUntil > end) { cursor = end; break; }
                        cursor = Mathf.Max(cursor, food.waitUntil);
                        food.phase = FoodPhase.Resting;
                        food.phaseProgress = 0;
                        continue;
                    }
                    if (food.phase == FoodPhase.Available)
                    {
                        float due = food.maturitySchedule[food.nextMaturity];
                        float at = Mathf.Max(cursor + Mathf.Max(0, due - food.phaseProgress) / rate, food.lastFruitAt + 1f);
                        if (at > end || at >= expiry)
                        {
                            food.phaseProgress += (end - cursor) * rate;
                            cursor = end; break;
                        }
                        food.phaseProgress += (at - cursor) * rate;
                        cursor = at;
                        food.lastFruitAt = at;
                        var fruit = new FruitState { fruit = ++state.nextFruit, source = food.food,
                            x = food.x, y = food.y, maturedAt = at, expiresAt = at + PreservationSeconds };
                        state.fruitLedger.matured++;
                        state.events.Add(new WorldEvent { kind = "fruit_matured", fruit = fruit.fruit, food = food.food });
                        // 大步長仍保留實際成熟時間，不會延後保鮮。
                        if (fruit.expiresAt <= time) { state.fruitLedger.rotten++; state.events.Add(new WorldEvent { kind = "fruit_rotten", fruit = fruit.fruit, food = food.food }); }
                        else state.fruits.Add(fruit);
                        food.nextMaturity++;
                        if (food.nextMaturity == food.maturitySchedule.Length)
                        {
                            food.phase = FoodPhase.Waiting;
                            food.waitUntil = fruit.expiresAt;
                        }
                        continue;
                    }
                    float duration = food.phase == FoodPhase.Recovering ? growth.firstMaturitySeconds : growth.restSeconds;
                    float transition = cursor + Mathf.Max(0, duration - food.phaseProgress) / rate;
                    if (transition > end || transition >= expiry)
                    {
                        food.phaseProgress += (end - cursor) * rate;
                        cursor = end; break;
                    }
                    cursor = transition;
                    BeginCycle(growth, food, random);
                    // 階段終點當下可成熟第一顆，即使恰為本步終點。
                    if (cursor == end && cursor < expiry)
                    {
                        var fruit = new FruitState { fruit = ++state.nextFruit, source = food.food,
                            x = food.x, y = food.y, maturedAt = cursor, expiresAt = cursor + PreservationSeconds };
                        state.fruits.Add(fruit); state.fruitLedger.matured++;
                        food.lastFruitAt = cursor; food.nextMaturity++;
                        state.events.Add(new WorldEvent { kind = "fruit_matured", fruit = fruit.fruit, food = food.food });
                    }
                }
                food.lastAdvancedAt = end;
            }
            for (int i = state.foods.Count - 1; i >= 0; i--)
            {
                var food = state.foods[i];
                if (time < food.bornAt + Growth(config, food).lifetimeSeconds) continue;
                int removed = state.fruits.RemoveAll(value => value.source == food.food);
                state.fruitLedger.sourceExpired += removed;
                state.foods.RemoveAt(i);
                state.events.Add(new WorldEvent { kind = "food_expired", food = food.food, amount = removed, x = food.x, y = food.y });
                foreach (var individual in state.individuals)
                    if (individual.cultivatingFood == food.food) { individual.cultivatingFood = 0; individual.cultivateUntil = individual.cultivateRemaining = 0; }
            }
            foreach (var food in state.foods) food.amount = state.fruits.FindAll(value => value.source == food.food).Count;
        }
    }
}
