using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ecosystem.Editor
{
    public static class ValidateLoadMovement
    {
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        static void Near(float actual, float expected, string message) => Require(Mathf.Abs(actual - expected) < .0001f, message + $"：{actual} != {expected}");
        static World Make(string species = "Flame", int count = 0, float step = .1f)
        {
            var config = new WorldConfig { foraging = true, halfExtent = 20, naturalFoodLimit = 0, initialSpecies = species,
                individualsPerSpecies = 1, stepSeconds = step, episodeSteps = 500, disableAging = true,
                disableReproduction = true, idleCost = 0, initialSatiety = 50, manaRecoveryRate = 0, staminaRecoveryRate = 0 };
            var world = new World(config, 101); world.Reset();
            var actor = world.State.individuals.Single(); actor.x = actor.y = actor.facing = 0;
            for (int i = 0; i < count; i++) AddFruit(world, actor);
            world.RefreshPerception(true);
            return world;
        }
        static FruitState AddFruit(World world, IndividualState actor)
        {
            var fruit = new FruitState { fruit = ++world.State.nextFruit, holder = actor.individual, maturedAt = 0, expiresAt = 100 };
            world.State.fruits.Add(fruit); world.State.fruitLedger.initial++;
            return fruit;
        }
        static void Step(World world, WorldAction action)
        {
            world.Step(world.State.individuals.ToDictionary(value => value.individual, value => action));
        }
        static void Ordinary()
        {
            string[] species = { "Flame", "Frost", "Moss" };
            float[] speeds = { 2.5f, 3.2f, 2.7f };
            float[] loads = { 1f, 1f, .9f, .8f };
            for (int index = 0; index < species.Length; index++) for (int count = 0; count <= 3; count++)
            foreach (bool run in new[] { false, true }) foreach (bool slow in new[] { false, true })
            {
                var world = Make(species[index], count); var actor = world.State.individuals.Single();
                if (slow) actor.slowUntil = 10;
                for (int i = 0; i < 10; i++) Step(world, new WorldAction(Vector2.right, run: run));
                float expected = speeds[index] * loads[count] * (run ? 1.5f : 1f) * (slow ? .55f : 1f);
                Near(actor.x, expected, "三物種走跑、原減速與負重相乘且不逐步疊加");
                Near(actor.activityStaminaSpent, run ? 10 : 0, "負重不增加體力消耗");
                Near(actor.satiety, 50, "負重不增加飽食消耗");
                Near(actor.skillManaSpent, 0, "普通移動無額外魔耗");
            }
            var rooted = Make(count: 3); var rootedActor = rooted.State.individuals.Single(); rootedActor.rootUntil = 2;
            Step(rooted, new WorldAction(Vector2.right, run: true));
            Near(rootedActor.x, 0, "定身速度仍為零"); Near(rootedActor.activityStaminaSpent, 0, "定身不花奔跑體力");
            var juvenile = Make(count: 1); var child = juvenile.State.individuals.Single(); child.age = 0;
            Step(juvenile, new WorldAction(Vector2.right)); Near(child.x, .25f, "幼體一果不受負重懲罰");
        }
        static void DynamicInventory()
        {
            var world = Make(count: 3); var actor = world.State.individuals.Single();
            Step(world, new WorldAction(Vector2.zero, foodAction: FoodAction.Eat, fruit: 1));
            Step(world, new WorldAction(Vector2.right));
            Near(actor.x, .2f, "正在吃的果實仍算重量，移動中斷不騰空"); Near(actor.satiety, 50, "中斷不給部分營養");
            world = Make(count: 3); actor = world.State.individuals.Single();
            for (int i = 0; i < 20; i++) Step(world, new WorldAction(Vector2.zero, foodAction: FoodAction.Eat, fruit: 1));
            Near(actor.satiety, 70, "負重不拖慢兩秒進食");
            Step(world, new WorldAction(Vector2.right)); Near(actor.x, .225f, "吃完果實後即時減重");
            world = Make(count: 3); actor = world.State.individuals.Single();
            Step(world, new WorldAction(Vector2.zero, foodAction: FoodAction.Drop, fruit: 1));
            Require(world.State.fruits[0].holder == actor.individual, "放下未完成仍持有");
            Step(world, new WorldAction(Vector2.zero, foodAction: FoodAction.Drop, fruit: 1));
            Require(world.State.fruits[0].holder == 0, "負重不拖慢點二秒放下");
            Step(world, new WorldAction(Vector2.right)); Near(actor.x, .225f, "放下後即時減重");
            world = Make(count: 3); actor = world.State.individuals.Single(); world.State.fruits[0].expiresAt = .1f;
            Step(world, new WorldAction(Vector2.zero));
            Step(world, new WorldAction(Vector2.right)); Near(actor.x, .225f, "腐爛後即時減重");
            Require(world.State.fruitLedger.rotten == 1, "腐爛只移除一顆");

            world = Make(count: 3); actor = world.State.individuals.Single();
            var receiver = world.AddIndividual("Flame"); receiver.x = .5f; receiver.y = 0; receiver.facing = 180;
            AddFruit(world, receiver); world.RefreshPerception(true);
            var send = new WorldAction(Vector2.zero, foodAction: FoodAction.Deliver, fruit: 1, recipient: receiver.individual);
            var receive = new WorldAction(Vector2.zero, foodAction: FoodAction.Receive, recipient: actor.individual);
            world.Step(new Dictionary<int, WorldAction> { [actor.individual] = send, [receiver.individual] = receive });
            Require(receiver.reservedCapacity == 1 && world.State.fruits[0].holder == actor.individual, "交付預留尚未到手");
            world.Step(new Dictionary<int, WorldAction> { [actor.individual] = send, [receiver.individual] = new WorldAction(Vector2.up) });
            Near(receiver.y, .25f, "接收預留不算重量");
            world.Step(new Dictionary<int, WorldAction> { [actor.individual] = new WorldAction(Vector2.right), [receiver.individual] = new WorldAction(Vector2.zero) });
            Near(actor.x, .2f, "未送完果實仍算送方重量");

            world = Make(count: 3); actor = world.State.individuals.Single();
            receiver = world.AddIndividual("Flame"); receiver.x = .5f; receiver.y = 0; receiver.facing = 180; world.RefreshPerception(true);
            send = new WorldAction(Vector2.zero, foodAction: FoodAction.Deliver, fruit: 1, recipient: receiver.individual);
            receive = new WorldAction(Vector2.zero, foodAction: FoodAction.Receive, recipient: actor.individual);
            for (int i = 0; i < 10; i++) world.Step(new Dictionary<int, WorldAction> { [actor.individual] = send, [receiver.individual] = receive });
            Require(world.State.fruits[0].holder == receiver.individual, "一秒交付不受負重拖慢");
            world.Step(new Dictionary<int, WorldAction> { [actor.individual] = new WorldAction(Vector2.up), [receiver.individual] = new WorldAction(Vector2.up) });
            Near(actor.y, .225f, "交付完成後送方減重"); Near(receiver.y, .25f, "接收一果仍全速");
        }
        static void MobilitySkills()
        {
            float[] loads = { 1f, 1f, .9f, .8f };
            foreach (bool dash in new[] { false, true }) for (int count = 0; count <= 3; count++)
            foreach (bool slow in new[] { false, true })
            {
                var world = Make(dash ? "Frost" : "Flame", count); var actor = world.State.individuals.Single();
                actor.loadout = dash ? new[] { WorldSkill.ColdMistDash, WorldSkill.FrostTrap } : new[] { WorldSkill.Charge, WorldSkill.Shield };
                if (slow) actor.slowUntil = 10;
                Step(world, new WorldAction(Vector2.zero, aim: new Vector2(10, 0), skill: actor.loadout[0]));
                Near(actor.x, (dash ? .8f : 1f) * loads[count], "技能本步速度只套一次負重");
                for (int i = 0; i < 3; i++) Step(world, new WorldAction(Vector2.zero));
                Near(actor.x, (dash ? 3f : 4f) * loads[count], "技能距離按負重縮短且保留原本減速豁免");
                Near(dash ? actor.dashRemaining : actor.chargeRemaining, 0, "相同四步完成技能，負重不延長持續時間");
                Near(actor.skillManaSpent, 12, "技能魔耗不變"); Near(actor.skillReadyTimes[0], dash ? 7 : 6, "技能冷卻不變");
                Near(actor.activityStaminaSpent, 0, "技能不新增負重體力消耗");
            }
            foreach (int count in new[] { 0, 3 })
            {
                var world = Make(count: count); var actor = world.State.individuals.Single(); actor.loadout = new[] { WorldSkill.Charge, WorldSkill.Shield };
                var enemy = world.AddIndividual("Frost"); enemy.x = 2.7f; enemy.y = 0; enemy.facing = 180;
                float health = enemy.health;
                var charge = new WorldAction(Vector2.zero, aim: new Vector2(10, 0), skill: WorldSkill.Charge);
                world.Step(new Dictionary<int, WorldAction> { [actor.individual] = charge, [enemy.individual] = new WorldAction(Vector2.zero) });
                for (int i = 0; i < 3; i++) world.Step(new Dictionary<int, WorldAction> { [actor.individual] = new WorldAction(Vector2.zero), [enemy.individual] = new WorldAction(Vector2.zero) });
                Near(health - enemy.health, 24, "衝鋒負重不改變命中傷害");
            }
            var rooted = Make(count: 3); var rootedActor = rooted.State.individuals.Single();
            rootedActor.loadout = new[] { WorldSkill.Charge, WorldSkill.Shield }; rootedActor.rootUntil = 1;
            Step(rooted, new WorldAction(Vector2.zero, aim: new Vector2(10, 0), skill: WorldSkill.Charge));
            Near(rootedActor.x, 0, "負重衝鋒不能越過定身");
            var biasWorld = Make(count: 3); var biased = biasWorld.State.individuals.Single();
            biased.movementBiases.Add(new MovementBiasState { skill = WorldSkill.IntimidatingRoar, strength = 1, angle = 90, until = 10 });
            Step(biasWorld, new WorldAction(Vector2.right));
            Near(biased.movementBiases[0].strength, 1, "負重不新增敵方移動偏向重量");
            Near(biased.movementBiases[0].angle, 90, "負重不修改移動偏向方向");
        }
        public static void Run()
        {
            var world = Make(count: 2); var actor = world.State.individuals.Single();
            Step(world, new WorldAction(Vector2.right));
            Near(actor.x, .25f * .9f, "兩顆果實走速為九成");
            world = Make(count: 3); actor = world.State.individuals.Single();
            actor.loadout = new[] { WorldSkill.Charge, WorldSkill.Shield };
            Step(world, new WorldAction(Vector2.zero, aim: new Vector2(10, 0), skill: WorldSkill.Charge));
            Near(actor.x, .8f, "三果衝鋒速度八成");
            for (int i = 0; i < 3; i++) Step(world, new WorldAction(Vector2.zero));
            Near(actor.x, 3.2f, "三果衝鋒距離縮短且持續時間不變");
            Near(actor.chargeRemaining, 0, "負重不延長衝鋒");
            Ordinary(); DynamicInventory(); MobilitySkills();
            ValidateFruitTransfer.Run();
            Debug.Log("攜帶負重驗收通過：三物種走跑、容量變化、原控制效果、技能時間傷害與資源消耗");
        }
    }
}
