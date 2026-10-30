using System;
using System.Linq;
using UnityEngine;
namespace Ecosystem.Editor
{
    public static class ValidateSourcePlacement
    {
        static void Require(bool value, string message) { if (!value) throw new Exception(message); }
        static void IdleStep(World world)
        {
            world.Step(world.State.individuals.ToDictionary(value => value.individual, value => new WorldAction(Vector2.zero)));
        }
        static void Legal(World world)
        {
            foreach (var food in world.State.foods)
            {
                float radius = food.growthType == FoodGrowthType.Large ? world.Config.largeFood.growthRadius : world.Config.smallFood.growthRadius;
                Require(Mathf.Abs(food.x) + radius <= world.Config.halfExtent && Mathf.Abs(food.y) + radius <= world.Config.halfExtent, "補生整圓在界內");
                foreach (var other in world.State.foods.Where(value => value.food > food.food))
                    Require(Vector2.Distance(new Vector2(food.x,food.y), new Vector2(other.x,other.y)) >= radius + (other.growthType == FoodGrowthType.Large ? world.Config.largeFood.growthRadius : world.Config.smallFood.growthRadius), "補生整圓不可重疊");
            }
        }
        public static void Run()
        {
            var world = new World(new WorldConfig { foraging = true, halfExtent = 20, individualsPerSpecies = 10 }, 101);
            world.Reset();
            Require(world.State.foods.Count == 16, "正式世界必須有十六處來源");
            foreach (var food in world.State.foods)
            {
                float radius = food.growthType == FoodGrowthType.Large ? 5.5f : 4f;
                Require(Mathf.Abs(food.x) + radius <= 20 && Mathf.Abs(food.y) + radius <= 20, "整個生長圓必須在界內");
                foreach (var other in world.State.foods.Where(value => value.food > food.food))
                    Require(Vector2.Distance(new Vector2(food.x,food.y), new Vector2(other.x,other.y)) >= radius + (other.growthType == FoodGrowthType.Large ? 5.5f : 4f), "生長圓不可重疊");
            }
            Require(world.State.foods.Count(value => value.growthType == FoodGrowthType.Large) == 4, "正式世界須十二小四大");
            Require(world.State.foods.All(value => value.bornAt < 0 && -value.bornAt < (value.growthType == FoodGrowthType.Large ? 240 : 120)), "初始年齡須在壽命前錯開");
            Require(world.State.fruits.Count > 0, "初始至少有可採果");
            Require(world.State.fruits.All(value => value.maturedAt < 0 && value.expiresAt > 0 && value.expiresAt < 40), "初始果實不得重新保鮮");
            Require(world.State.fruitLedger.initial == world.State.fruits.Count && world.State.fruitLedger.matured == 0 && world.State.fruitLedger.Residual(world.State.fruits.Count) == 0, "起始存量分開統計");
            var moss = new World(new WorldConfig { foraging = true, halfExtent = 20, individualsPerSpecies = 30, initialSpecies = "Moss" }, 101);
            moss.Reset();
            for (int i = 0; i < world.State.foods.Count; i++) Require(JsonUtility.ToJson(world.State.foods[i]) == JsonUtility.ToJson(moss.State.foods[i]), "兩組來源與成熟安排對齊");
            for (int i = 0; i < 30; i++) Require(world.State.individuals[i].x == moss.State.individuals[i].x && world.State.individuals[i].y == moss.State.individuals[i].y, "兩組起點對齊");
            var renew = new World(new WorldConfig { foraging = true, halfExtent = 20, individualsPerSpecies = 1, stepSeconds = 1, episodeSteps = 500, idleCost = 0, disableAging = true, disableReproduction = true }, 101);
            renew.Reset(); renew.State.foods.Clear(); renew.State.fruits.Clear(); renew.State.fruitLedger = new FruitLedger();
            for (int step = 0; step < 9; step++) IdleStep(renew);
            Require(renew.State.foods.Count == 0, "十秒前不可補生");
            IdleStep(renew);
            Require(renew.State.foods.Count == 2 && renew.State.foods.All(value => value.bornAt == 10 && value.amount == 0 && !value.mature), "每十秒至多兩處新生零果來源");
            Legal(renew);
            for (int step = 10; step < 80; step++) { IdleStep(renew); Legal(renew); }
            Require(renew.State.fruitLedger.Residual(renew.State.fruits.Count) == 0, "持續天然生產守恆");
            var countedConfig = new WorldConfig { foraging = true, halfExtent = 20, naturalFoodLimit = 0, individualsPerSpecies = 1, stepSeconds = 1, episodeSteps = 100, idleCost = 0, disableAging = true, disableReproduction = true };
            var counted = new World(countedConfig, 9); counted.Reset(); countedConfig.naturalFoodLimit = 2; counted.State.nextNaturalSpawnAt = 1;
            for (int i = 0; i < 20; i++) IdleStep(counted);
            Require(counted.State.foods.Count == 2, "未成熟與其他存續階段都計入天然目標");
            counted.State.foods[0].natural = false; int plantedId = counted.State.foods[0].food; counted.State.nextNaturalSpawnAt = 21;
            IdleStep(counted);
            Require(counted.State.foods.Count == 3 && counted.State.foods.Any(value => value.food == plantedId), "播種不占天然名額但保留共同占地");
            for (int seed = 1; seed <= 50; seed++)
            {
                var sampled = new World(new WorldConfig { foraging = true, halfExtent = 20, individualsPerSpecies = 1 }, seed); sampled.Reset(); Legal(sampled);
                Require(sampled.State.foods.Count == 16 && sampled.State.foods.Count(value => value.growthType == FoodGrowthType.Large) == 4 && sampled.State.fruits.Count > 0, "多種子正式數量與可採果保證");
                Require(sampled.State.fruitLedger.Residual(sampled.State.fruits.Count) == 0, "多種子初始守恆");
            }
            var tiny = new World(new WorldConfig { foraging = true, halfExtent = 3, individualsPerSpecies = 1 }, 4); tiny.Reset();
            Require(tiny.State.foods.Count == 0 && tiny.State.events.Any(value => value.kind == "food_initial_shortfall" && value.amount == 16), "小於生長圓的世界清楚記錄不足且不硬塞");
            var tangent = new World(new WorldConfig { foraging = true, halfExtent = 4, individualsPerSpecies = 1, stepSeconds = 1, episodeSteps = 500, idleCost = 0, disableReproduction = true, disableAging = true }, 5); tangent.Reset();
            Require(tangent.State.foods.Count == 1 && tangent.State.foods[0].growthType == FoodGrowthType.Small && tangent.State.foods[0].x == 0 && tangent.State.foods[0].y == 0, "剛好相切且只容小型");
            for (int i = 0; i < 10; i++) IdleStep(tangent);
            Require(tangent.State.foods.Count == 1, "滿場不擠掉來源");
            tangent.State.foods[0].bornAt = -110;
            for (int i = 0; i < 10; i++) IdleStep(tangent);
            Require(tangent.State.foods.Count == 1 && tangent.State.foods[0].bornAt == 20 && tangent.State.foods[0].amount == 0, "來源到期同刻釋放空間並補零果新株");
            int blockedAt = tangent.State.nextFood;
            tangent.State.foods[0].natural = false;
            for (int i = 0; i < 10; i++) IdleStep(tangent);
            Require(tangent.State.nextFood == blockedAt, "播種占地時天然補生等待");
            tangent.State.foods.Clear(); tangent.State.fruits.Clear();
            IdleStep(tangent);
            Require(tangent.State.foods.Count == 0, "失敗不欠額補發");
            for (int i = 0; i < 9; i++) IdleStep(tangent);
            Require(tangent.State.foods.Count == 1 && tangent.State.foods[0].bornAt == 40, "等下一個天然補生時點");
            int largeCount = 0;
            for (int seed = 1; seed <= 500; seed++)
            {
                var config = new WorldConfig { foraging = true, halfExtent = 5.5f, naturalFoodLimit = 0, individualsPerSpecies = 1, stepSeconds = 1, idleCost = 0 };
                config.smallFood.growthRadius = 5.5f;
                var weighted = new World(config, seed); weighted.Reset();
                config.naturalFoodLimit = 1; weighted.State.nextNaturalSpawnAt = 1; IdleStep(weighted);
                Require(weighted.State.foods.Count == 1, "同點兩型均合法的天然補生");
                if (weighted.State.foods[0].growthType == FoodGrowthType.Large) largeCount++;
            }
            Require(largeCount > 100 && largeCount < 200, "天然兩型可放時採七三權重");
            foreach (var path in new[] { "foraging.json", "evaluation.json", "scenarios/world.json", "scenarios/survival-isolation/Flame.json", "scenarios/survival-isolation/Frost.json", "scenarios/survival-isolation/Moss.json" })
            {
                var config = JsonUtility.FromJson<WorldConfig>(System.IO.File.ReadAllText("../training/" + path));
                var configured = new World(config, 101); configured.Reset();
                Require(configured.Config.smallFood.maxFruit == 4 && configured.Config.smallFood.restSeconds == 20 && configured.Config.largeFood.minFruit == 5 && configured.Config.largeFood.maxFruit == 7 && configured.Config.largeFood.restSeconds == 30 && configured.Config.largeFood.firstMaturitySeconds == 15, "現行配置必須使用果實規則：" + path);
            }
            Debug.Log("來源配置完整驗收通過，天然大型樣本=" + largeCount);
        }
    }
}
