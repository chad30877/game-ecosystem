using System;
using System.IO;
using System.Linq;
using UnityEngine;
namespace Ecosystem.Editor
{
    // 透過公開世界狀態建立可見驗收情境；不將診斷快照當作生態觀察。
    public static class ValidateFruitVisuals
    {
        public static void BuildAndWrite() { BuildWorld.Build(); WriteFixture(); }

        public static void WriteFixture()
        {
            var args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "--visual-output");
            string folder = args[option + 1];
            Directory.CreateDirectory(folder);
            var config = new WorldConfig { foraging = true, halfExtent = 20, observationRadius = 8, individualsPerSpecies = 3,
                disableAging = true, disableReproduction = true, idleCost = 0, episodeSteps = 100 };
            var world = new World(config, 101); world.Reset();
            world.State.fruits.Clear();
            float[] ages = { 5, 45, 88, 100 };
            for (int i = 0; i < world.State.foods.Count; i++)
            {
                var source = world.State.foods[i];
                if (i < 4) { source.x = i < 2 ? -5.5f : 5.5f; source.y = i % 2 == 0 ? -5.5f : 5.5f; }
                source.lastFruitAt = -1000; source.cycle = 0;
                FruitRules.Initialize(config, source, -(i < 4 ? ages[i] : 85));
            }
            FruitRules.Advance(config, world.State, new System.Random(101), 0);
            var lowSource = world.State.foods[1].food;
            var waitingSource = world.State.foods[2].food;
            var keep = world.State.fruits.FindAll(f => f.source == lowSource).Take(3)
                .Concat(world.State.fruits.FindAll(f => f.source == waitingSource)).ToArray();
            world.State.fruits.Clear(); world.State.fruits.AddRange(keep);
            var actors = world.State.individuals;
            Vector2[] positions = { new Vector2(-5.1f, 5.5f), new Vector2(-1, 0), new Vector2(-2, 3),
                new Vector2(2, 0), new Vector2(2.5f, 0), new Vector2(0, -3), new Vector2(-5, -5.5f), new Vector2(0, 3), new Vector2(5, -3) };
            for (int i = 0; i < actors.Count; i++) { actors[i].x = positions[i].x; actors[i].y = positions[i].y; actors[i].facing = i == 0 || i == 4 || i == 6 ? 180 : 0; actors[i].satiety = 70; }
            FruitState Add(int source, int holder, Vector2 point, bool old = false)
            {
                var fruit = new FruitState { fruit = ++world.State.nextFruit, source = source, holder = holder,
                    x = point.x, y = point.y, maturedAt = old ? -33 : -2, expiresAt = old ? 7 : 38 };
                world.State.fruits.Add(fruit); return fruit;
            }
            var gather = world.State.fruits.First(f => f.source == lowSource); gather.claimedBy = actors[0].individual;
            var pickup = Add(0, 0, new Vector2(-.6f, 0)); pickup.claimedBy = actors[1].individual;
            var eating = Add(0, actors[2].individual, positions[2], true);
            var delivery = Add(0, actors[3].individual, positions[3]);
            var drop = Add(0, actors[5].individual, positions[5]);
            Add(0, actors[7].individual, positions[7]); Add(0, actors[7].individual, positions[7]); Add(0, actors[7].individual, positions[7], true);
            Add(0, 0, new Vector2(-2, -2)); Add(0, 0, new Vector2(-1, -2), true);
            void Operation(int index, FoodAction action, FruitState fruit, int target = 0)
            { actors[index].foodOperation = action; actors[index].operationFruit = fruit.fruit; actors[index].operationTarget = target; actors[index].operationProgress = .15f; }
            Operation(0, FoodAction.Gather, gather); Operation(1, FoodAction.Pickup, pickup); Operation(2, FoodAction.Eat, eating);
            Operation(3, FoodAction.Deliver, delivery, actors[4].individual); Operation(4, FoodAction.Receive, delivery, actors[3].individual);
            actors[4].reservedCapacity = 1; actors[3].deliveryInviteTo = actors[4].individual; actors[3].deliveryInviteFruit = delivery.fruit;
            Operation(5, FoodAction.Drop, drop);
            actors[6].cultivatingFood = world.State.foods[0].food; actors[6].cultivateUntil = 1; actors[6].cultivateRemaining = 1; actors[6].cultivateGrowth = 1;
            world.State.foods[0].cultivator = actors[6].individual;
            foreach (var source in world.State.foods) source.amount = world.State.fruits.FindAll(f => f.source == source.food).Count;
            world.State.fruitLedger = new FruitLedger { initial = world.State.fruits.Count };
            world.RefreshPerception();
            File.WriteAllText(Path.Combine(folder, "fixture-config.json"), JsonUtility.ToJson(config));
            File.WriteAllText(Path.Combine(folder, "fixture-0.json"), JsonUtility.ToJson(world.State));
            gather.source = 0; gather.holder = actors[0].individual; gather.claimedBy = 0;
            actors[0].foodOperation = FoodAction.None; actors[0].operationFruit = 0; actors[0].operationProgress = 0;
            world.State.foods[1].amount--;
            world.State.step = 1; world.State.time = config.stepSeconds;
            world.RefreshPerception();
            File.WriteAllText(Path.Combine(folder, "fixture-1.json"), JsonUtility.ToJson(world.State));
            int removed = world.State.fruits.RemoveAll(f => f.source == lowSource);
            world.State.fruitLedger.eaten += removed;
            world.State.foods[1].amount = 0;
            world.State.step = 2; world.State.time = config.stepSeconds * 2;
            world.RefreshPerception();
            File.WriteAllText(Path.Combine(folder, "fixture-2.json"), JsonUtility.ToJson(world.State));
            ValidateProductionAI.RunAll();
            Debug.Log("果實畫面固定情境建立及01至09回歸通過");
        }
    }
}
