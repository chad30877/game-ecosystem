using System;
using System.Linq;
using UnityEngine;

namespace Ecosystem.Editor
{
    public static class ValidateSmoke
    {
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        public static void Run()
        {
            var world = new World(new WorldConfig { observationRadius = 6 }, 7);
            world.Reset();
            var observer = world.State.individuals[0];
            observer.x = observer.y = observer.facing = 0;
            world.State.areas.Add(new AreaState { area = 1, species = "Moss", skill = WorldSkill.SporeSmoke,
                x = 7, y = 0, radius = 1, until = 100 });
            world.RefreshPerception(true);
            Require(world.ObserveEntities(observer).Any(row => row[0] == -5f), "相切煙幕必須進入共同觀察");
            Require(observer.smokeKnowledge.Single().harmful, "異種煙幕有害");
            observer.facing = 180;
            world.RefreshPerception();
            Require(!observer.smokeKnowledge.Single().visible, "轉頭保留記憶");
            world.State.areas.Clear();
            world.State.time = 4.99f;
            world.RefreshPerception();
            Require(observer.smokeKnowledge.Count == 1, "視野外消失不能洩漏");
            world.State.time = 5;
            world.RefreshPerception();
            Require(observer.smokeKnowledge.Count == 0, "五秒記憶到期");
            observer.facing = 0;
            world.State.areas.Add(new AreaState { area = 2, species = observer.species, skill = WorldSkill.SporeSmoke,
                x = 2, y = 4, radius = 1, until = 100 });
            world.RefreshPerception();
            Require(observer.smokeKnowledge.Count == 1 && !observer.smokeKnowledge[0].harmful, "中心在視野外仍可察覺無毒煙幕邊緣");
            observer.facing = 180;
            world.RefreshPerception();
            world.State.areas.Clear();
            world.RefreshPerception();
            Require(observer.smokeKnowledge.Count == 1, "未重見不得清除");
            observer.facing = 0;
            world.RefreshPerception();
            Require(observer.smokeKnowledge.Count == 0, "重見確認消失");
            world.State.areas.Add(new AreaState { area = 3, species = "Moss", skill = WorldSkill.SporeSmoke,
                x = 0, y = 0, radius = 2, until = 100 });
            world.State.areas.Add(new AreaState { area = 4, species = "Moss", skill = WorldSkill.SporeSmoke,
                x = -5, y = 0, radius = 1, until = 100 });
            world.RefreshPerception();
            Require(observer.smokeKnowledge.Count == 1, "圓心可見、背後未知不洩漏");
            var row = world.ObserveEntities(observer).First(value => value[0] == -5);
            Require(row.Length == 10 && row[4] == 1 && row[5] == 1 && row[6] == 0 && row.Skip(7).All(value => value == 0), "共同觀察無施放者與失效時間");
            string saved = JsonUtility.ToJson(world.State);
            observer.facing = 180;
            world.RefreshPerception();
            var snapshot = JsonUtility.FromJson<WorldState>(saved);
            Require(snapshot.individuals[0].smokeKnowledge.Single().area == 3, "快照不受後續觀察污染");
            Debug.Log("煙幕世界驗收通過");
        }

        public static void RunAvoidance()
        {
            Run();
            var config = JsonUtility.FromJson<WorldConfig>(System.IO.File.ReadAllText("../training/foraging.json"));
            config.scanEnabled = false;
            var world = new World(config, 7);
            world.Reset();
            var observer = world.State.individuals[0];
            observer.x = observer.y = observer.facing = 0;
            foreach (var other in world.State.individuals.Skip(1)) { other.x = 15; other.y = 15; }
            world.State.areas.Add(new AreaState { area = 1, skill = WorldSkill.SporeSmoke, species = "Moss", radius = 2, until = 100 });
            world.RefreshPerception(true);
            var actions = world.State.individuals.ToDictionary(value => value.individual, value => new WorldAction(Vector2.zero));
            actions[observer.individual] = ForagingController.Decide(world, observer);
            world.Step(actions);
            Require(observer.decisionReason == "smoke_exit" && CombatRules.Position(observer).sqrMagnitude > 0,
                "身在有害煙幕圓心時先離開");
            observer.x = observer.y = 0;
            observer.retreatUntil = 2;
            observer.retreatX = 1; observer.retreatY = 0;
            world.State.areas[0].x = 1.5f;
            world.RefreshPerception(true);
            var escape = ForagingController.Decide(world, observer);
            Require(escape.movement.x < 0.9f && observer.decisionReason == "smoke_exit", "撤退有較安全離開方向時優先使用");
            world.State.areas[0].x = 1;
            world.State.areas[0].radius = 0.9f;
            world.RefreshPerception(true);
            escape = ForagingController.Decide(world, observer);
            Require(observer.decisionReason == "smoke_avoid" && escape.movement.x < 0.01f, "緩衝帶不能阻擋安全側向撤退");
            observer.x = config.halfExtent;
            world.State.areas[0].x = config.halfExtent - 1; world.State.areas[0].radius = 2;
            world.RefreshPerception(true);
            escape = ForagingController.Decide(world, observer);
            Require(observer.decisionReason == "smoke_exit" && Mathf.Abs(escape.movement.y) > 0.9f, "邊界撤退優先安全側向離開");
            observer.x = 0;
            world.State.areas.Clear();
            foreach (var center in new[] { new Vector2(1.2f, 0), new Vector2(0, 1.2f), new Vector2(0, -1.2f) })
                world.State.areas.Add(new AreaState { area = world.State.areas.Count + 1, skill = WorldSkill.SporeSmoke,
                    species = "Moss", x = center.x, y = center.y, radius = 1, until = 100 });
            world.RefreshPerception(true);
            escape = ForagingController.Decide(world, observer);
            Require(observer.decisionReason == "smoke_cross" && escape.movement.x > 0.9f, "前方及兩側皆有煙幕時緊急穿越");
            world.State.areas.RemoveRange(1, 2);
            world.State.areas[0].species = observer.species;
            world.RefreshPerception(true);
            escape = ForagingController.Decide(world, observer);
            Require(observer.decisionReason == "retreat" && escape.movement.x > 0.9f, "同種煙幕無毒不迴避");
            observer.retreatUntil = 0;
            observer.x = observer.y = observer.facing = 0;
            world.State.areas[0].species = "Moss";
            world.State.areas[0].x = 1.5f; world.State.areas[0].radius = 1;
            world.State.foods.Clear();
            world.State.foods.Add(new FoodState { food = 1, x = 4, capacity = 30, amount = 30 });
            world.RefreshPerception(true);
            var avoid = ForagingController.Decide(world, observer);
            Require(observer.decisionReason == "smoke_avoid" && avoid.movement.x < 0.9f, "覓食路線繞避已知有害煙幕");
            observer.facing = 180;
            world.RefreshPerception();
            world.State.areas.Clear();
            avoid = ForagingController.Decide(world, observer);
            Require(observer.decisionReason == "smoke_avoid", "視野外過時記憶仍用於避險");
            observer.facing = 0;
            world.RefreshPerception();
            avoid = ForagingController.Decide(world, observer);
            Require(observer.decisionReason != "smoke_avoid", "確認消失後不再繞避");
            world.State.areas.Add(new AreaState { area = 5, skill = WorldSkill.SporeSmoke, species = "Moss", x = 1.5f, radius = 1, until = 100 });
            world.State.foods.Clear();
            var child = world.State.individuals[1];
            child.x = 4; child.y = 0; child.age = 1; child.satiety = 10; child.parentId = observer.individual;
            observer.children.Clear();
            observer.satiety = 80;
            world.RefreshPerception(true);
            avoid = ForagingController.Decide(world, observer);
            Require(observer.decisionReason == "smoke_avoid" && avoid.movement.x < 0.9f, "照顧幼體不能無條件進入煙幕");
            var threat = world.State.individuals.First(value => value.species == "Frost");
            threat.x = 4.5f; threat.y = 0;
            world.RefreshPerception();
            avoid = ForagingController.Decide(world, observer);
            Require(observer.decisionReason == "smoke_avoid" && avoid.movement.x < 0.9f, "保護幼體也須繞避煙幕");
            observer.x = observer.y = 0;
            world.State.areas[0].x = 0; world.State.areas[0].radius = 2;
            world.State.areas.Add(new AreaState { area = 6, skill = WorldSkill.SporeSmoke, species = "Moss", x = 0.5f, radius = 2, until = 100 });
            world.RefreshPerception();
            avoid = ForagingController.Decide(world, observer);
            Require(observer.decisionReason == "smoke_exit" && float.IsFinite(avoid.movement.x) && avoid.movement.sqrMagnitude > 0 &&
                !avoid.forage && !avoid.attack && avoid.feedChild == 0 && avoid.skillSlot == -1, "重疊煙幕先離開並停止主動採食、餵養及技能");
            observer.rootUntil = world.State.time + 1;
            Vector2 rooted = CombatRules.Position(observer);
            actions = world.State.individuals.ToDictionary(value => value.individual, value => new WorldAction(Vector2.zero));
            actions[observer.individual] = avoid;
            world.Step(actions);
            Require(CombatRules.Position(observer) == rooted, "煙幕離開決策不繞過定身限制");
            Debug.Log("煙幕避險驗收通過");
        }
    }
}
