using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Ecosystem.Editor
{
    public static class ValidateFruitCycle
    {
        static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
        static World Make()
        {
            var world = new World(new WorldConfig { foraging = true, halfExtent = 20, naturalFoodLimit = 0,
                stepSeconds = 1, episodeSteps = 400, disableAging = true, disableReproduction = true, idleCost = 0 }, 101);
            world.Reset();
            world.State.foods.Add(new FoodState { food = ++world.State.nextFood, growthType = FoodGrowthType.Small,
                mature = false, matureAt = 10, capacity = 4 });
            return world;
        }
        static void Until(World world, float time)
        {
            while(world.State.time < time) {
                var actions = new Dictionary<int, WorldAction>();
                foreach(var individual in world.State.individuals) actions[individual.individual] = new WorldAction(Vector2.zero);
                world.Step(actions);
            }
        }
        public static void Run()
        {
            var world = Make();
            Until(world, 9); Require(world.State.foods[0].amount == 0, "首次等待不可產果");
            Until(world, 10); Require(world.State.foods[0].amount == 1, "首次等待結束成熟第一顆，不能連續再生");
            Until(world, 40);
            var source = world.State.foods.Single();
            Require(source.amount >= 2 && source.amount <= 4 && source.phase == FoodPhase.Waiting, "小型單輪額度及等待階段");
            Require(world.State.fruits.Last().maturedAt == 40, "最後一顆在結果期終點成熟");
            Require(world.State.fruits.All(value => Mathf.Abs(value.expiresAt - value.maturedAt - 40) < 0.0001f), "每顆保存四十秒");
            var first = world.State.fruits.First();
            Require(!first.NearRot(39) && first.NearRot(40), "最後十秒才將腐爛");
            first.source = 0; first.holder = world.State.individuals[0].individual;
            var schedule = source.maturitySchedule.ToArray();
            Until(world, 49);
            Require(world.State.fruits.Contains(first), "採走仍保存原期限");
            Until(world, 50);
            Require(!world.State.fruits.Contains(first), "採走後同樣於第五十秒腐爛");
            Require(source.maturitySchedule.SequenceEqual(schedule), "採果或腐爛不重抽當輪排程");
            Until(world, 79); Require(source.phase == FoodPhase.Waiting, "採光不提早結束等待");
            Until(world, 80); Require(source.phase == FoodPhase.Resting && source.amount == 0, "最後一顆腐爛後才開始休養");
            Until(world, 99); Require(source.phase == FoodPhase.Resting, "自然休養二十秒");
            Until(world, 100); Require(source.phase == FoodPhase.Available && source.amount == 1, "休養後不重走首次成長");
            Until(world, 119);
            var detached = world.State.fruits.First(value => value.source == source.food);
            detached.source = 0; detached.holder = world.State.individuals[0].individual;
            float deadline = detached.expiresAt;
            Until(world, 120);
            Require(world.State.foods.Count == 0 && world.State.fruits.Contains(detached), "來源到期只移除未採果");
            Require(detached.expiresAt == deadline, "來源到期不重設已採果期限");
            Until(world, deadline);
            Require(world.State.fruits.Count == 0 && world.State.fruitLedger.Residual(0) == 0, "完整生命週期守恆");
            var twin = Make(); Until(twin, 40);
            Require(twin.State.foods[0].maturitySchedule.SequenceEqual(schedule), "固定種子可重現成熟安排");
            var large = Make(); large.State.foods[0].growthType = FoodGrowthType.Large;
            Until(large, 14); Require(large.State.fruits.Count == 0, "大型首次等待十五秒");
            Until(large, 55);
            Require(large.State.fruitLedger.matured >= 5 && large.State.fruitLedger.matured <= 7, "大型每輪五至七顆");
            Require(large.State.fruits.Last().maturedAt == 55, "大型結果四十秒");
            Until(large, 95); Require(large.State.foods[0].phase == FoodPhase.Resting, "大型等待最後保存期限");
            Until(large, 125); Require(large.State.foods[0].cycle == 2, "大型自然休養三十秒");
            Until(large, 240); Require(large.State.foods.Count == 0, "大型壽命二百四十秒");
            var harvested = Make(); Until(harvested, 40);
            foreach(var fruit in harvested.State.fruits) { fruit.source = 0; fruit.holder = harvested.State.individuals[0].individual; }
            Until(harvested, 79);
            Require(harvested.State.foods[0].phase == FoodPhase.Waiting && harvested.State.foods[0].amount == 0, "全部採光仍等最後腐爛期限");
            Until(harvested, 80); Require(harvested.State.foods[0].phase == FoodPhase.Resting, "已採光仍在原期限進入休養");
            var accelerated = Make(); accelerated.State.foods[0].growthMultiplier = 99;
            Until(accelerated, 4); Require(accelerated.State.fruits.Count == 0, "加速上限不可突破首次等待一半");
            Until(accelerated, 5); Require(accelerated.State.fruits.Single().expiresAt == 45, "加速成熟仍保存四十秒");
            Until(accelerated, 20); Require(accelerated.State.foods[0].phase == FoodPhase.Waiting, "結果最多兩倍");
            Until(accelerated, 59); Require(accelerated.State.foods[0].phase == FoodPhase.Waiting, "等待腐爛不可加速");
            Until(accelerated, 70); Require(accelerated.State.foods[0].cycle == 2, "最大加速下相鄰結果期開始相隔六十五秒");
            var counts = new HashSet<int>();
            for(int seed = 1; seed <= 60; seed++) {
                var sample = new World(new WorldConfig { foraging = true, halfExtent = 20, naturalFoodLimit = 0,
                    stepSeconds = 1, episodeSteps = 200, disableAging = true, disableReproduction = true, idleCost = 0 }, seed);
                sample.Reset(); sample.State.foods.Add(new FoodState { food = ++sample.State.nextFood });
                Until(sample, 40); counts.Add(sample.State.fruitLedger.matured);
                Require(sample.State.fruitLedger.Residual(sample.State.fruits.Count) == 0, "產果存量守恆");
            }
            Require(counts.SetEquals(new[] { 2, 3, 4 }), "每個整數額度均可抽到");
            var invalid = new WorldConfig { foraging = true };
            invalid.smallFood.firstMaturitySeconds = 0;
            bool rejected = false;
            try { new World(invalid, 1); } catch(ArgumentException) { rejected = true; }
            Require(rejected, "拒絕零成長時間以避免無界生產迴圈");
            Debug.Log("果實生命週期驗收通過：首次成熟、自然週期、保存、採走、到期、守恆及種子重現");
        }
    }
}
