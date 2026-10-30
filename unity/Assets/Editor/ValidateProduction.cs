using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Ecosystem.Editor
{
    public static class ValidateProduction
    {
        static void Require(bool value, string message) { if (!value) throw new Exception(message); }
        static World Make(int seed = 101, float step = 1)
        {
            var world = new World(new WorldConfig { foraging = true, halfExtent = 20, naturalFoodLimit = 0,
                initialSpecies = "Moss", individualsPerSpecies = 1, stepSeconds = step, episodeSteps = 1000,
                disableAging = true, disableReproduction = true, scanEnabled = false, idleCost = 0, manaRecoveryRate = 0.00001f,
                nutritionBudget = 0.00001f }, seed);
            world.Reset();
            var actor = world.State.individuals[0]; actor.x = actor.y = actor.facing = 0;
            actor.loadout = new[] { WorldSkill.Cultivate, WorldSkill.Sow }; actor.mana = 100;
            world.State.foods.Add(new FoodState { food = ++world.State.nextFood, x = 1, y = 0 });
            return world;
        }
        static void Step(World world, WorldAction action)
        {
            var actions = world.State.individuals.ToDictionary(a => a.individual, a => new WorldAction(Vector2.zero));
            actions[world.State.individuals[0].individual] = action; world.Step(actions);
        }

        static void Until(World world, float time)
        {
            while (world.State.time < time) Step(world, new WorldAction(Vector2.zero));
        }
        static void Switching()
        {
            var shield = Make(); var actor = shield.State.individuals[0];
            actor.loadout = new[] { WorldSkill.Cultivate, WorldSkill.Shield };
            Step(shield, new WorldAction(Vector2.zero, skill: WorldSkill.Cultivate));
            Step(shield, new WorldAction(Vector2.zero, skill: WorldSkill.Shield));
            Require(actor.skillManaSpent == 16 && actor.shieldUntil > shield.State.time && actor.cultivatingFood == 0 &&
                shield.State.foods[0].phaseProgress == 3, "培育可立即換防禦技能，不被舊培育時間阻擋");
            var attacking = Make();
            Step(attacking, new WorldAction(Vector2.zero, skill: WorldSkill.Cultivate));
            Step(attacking, new WorldAction(Vector2.zero, attack: true));
            Require(attacking.State.individuals[0].skillManaSpent == 6 && attacking.State.foods[0].phaseProgress == 3,
                "切換攻擊立即停止培育");
            var eat = Make(); var eater = eat.State.individuals[0];
            eat.State.fruits.Add(new FruitState { fruit = ++eat.State.nextFruit, holder = eater.individual, expiresAt = 100 });
            eat.State.fruitLedger.initial++;
            Step(eat, new WorldAction(Vector2.zero, skill: WorldSkill.Cultivate));
            Step(eat, new WorldAction(Vector2.zero, foodAction: FoodAction.Eat));
            Step(eat, new WorldAction(Vector2.zero, foodAction: FoodAction.Eat));
            Require(eat.State.fruitLedger.eaten == 1 && eater.skillManaSpent == 6, "切換食物操作立即釋放培育狀態");
            var combat = Make(); var defender = combat.State.individuals[0]; var enemy = combat.AddIndividual("Flame");
            enemy.x = -.5f; enemy.y = enemy.facing = 0;
            Step(combat, new WorldAction(Vector2.zero, skill: WorldSkill.Cultivate));
            combat.Step(new Dictionary<int, WorldAction> {
                [defender.individual] = new WorldAction(Vector2.zero, skill: WorldSkill.Cultivate),
                [enemy.individual] = new WorldAction(Vector2.zero, aim: Vector2.zero, attack: true) });
            Require(defender.health < 90 && defender.skillManaSpent == 12 && combat.State.foods[0].phaseProgress == 4,
                "實際普通近戰傷害不強制中斷培育");
            var dead = Make(); var doomed = dead.State.individuals[0]; var killer = dead.AddIndividual("Flame");
            killer.x = -.5f; killer.y = killer.facing = 0; doomed.health = 1;
            dead.Step(new Dictionary<int, WorldAction> {
                [doomed.individual] = new WorldAction(Vector2.zero, skill: WorldSkill.Cultivate),
                [killer.individual] = new WorldAction(Vector2.zero, aim: Vector2.zero, attack: true) });
            Require(doomed.health == 0 && doomed.skillManaSpent == 0 && dead.State.foods[0].phaseProgress == 1,
                "同一步死亡者不能提供免費培育");
        }
        static void Targets()
        {
            var world = Make(); world.Config.smallFood.growthRadius = .1f;
            var actor = world.State.individuals[0];
            Step(world, new WorldAction(Vector2.zero, skill: WorldSkill.Cultivate));
            int original = actor.cultivatingFood;
            var closer = new FoodState { food = ++world.State.nextFood, x = .5f, bornAt = world.State.time };
            world.State.foods.Add(closer);
            Step(world, new WorldAction(Vector2.zero, skill: WorldSkill.Cultivate));
            Require(actor.cultivatingFood == original && world.State.foods[0].phaseProgress == 4 && closer.phaseProgress == 1,
                "已開始有效來源不為稍近新來源切換");
            Step(world, new WorldAction(Vector2.zero, skill: WorldSkill.Cultivate, productionSource: closer.food));
            Require(actor.cultivatingFood == closer.food && closer.phaseProgress == 3, "指定來源可明確改變培育目標");
            Step(world, new WorldAction(Vector2.zero, skill: WorldSkill.Cultivate, productionSource: 999));
            Require(actor.cultivatingFood == 0 && actor.skillManaSpent == 18, "指定失效來源不改成另一隱藏目標");
            var range = Make(); range.State.individuals[0].x = -.5f;
            Step(range, new WorldAction(Vector2.zero, skill: WorldSkill.Cultivate));
            Require(range.State.individuals[0].skillManaSpent == 6, "培育距離一點五可施放");
            var outside = Make(); outside.State.individuals[0].x = -.5001f;
            Step(outside, new WorldAction(Vector2.zero, skill: WorldSkill.Cultivate));
            Require(outside.State.individuals[0].skillManaSpent == 0, "超過培育距離不付費");
        }
        static void Boundaries()
        {
            var stopped = Make();
            Step(stopped, new WorldAction(Vector2.zero, skill: WorldSkill.Cultivate));
            Step(stopped, new WorldAction(Vector2.zero));
            Require(Mathf.Abs(stopped.State.foods[0].phaseProgress - 3) < .001f && stopped.State.individuals[0].skillManaSpent == 6,
                "停止培育不留加速尾效且來源進度保留");
            var moving = Make();
            Step(moving, new WorldAction(Vector2.up, skill: WorldSkill.Cultivate));
            Require(moving.State.individuals[0].skillManaSpent == 0 && moving.State.foods[0].phaseProgress == 1,
                "主動移動時不培育");
            var scan = Make(step: .1f);
            Step(scan, new WorldAction(Vector2.zero, skill: WorldSkill.Cultivate, turn: 1));
            Require(Mathf.Abs(scan.State.individuals[0].skillManaSpent - .6f) < .001f, "有效視線內掃視不打斷培育");
            var hurt = Make(); hurt.State.individuals[0].health -= 1;
            Step(hurt, new WorldAction(Vector2.zero, skill: WorldSkill.Cultivate));
            Require(hurt.State.individuals[0].skillManaSpent == 6, "普通受傷不強制打斷培育");
            var young = Make(); young.State.individuals[0].age = 0;
            Step(young, new WorldAction(Vector2.zero, skill: WorldSkill.Cultivate));
            Require(Mathf.Abs(young.State.foods[0].phaseProgress - 1.3f) < .001f && young.State.individuals[0].skillManaSpent == 6,
                "幼體成長只縮放強度不縮放費用");
            var resting = Make(); Until(resting, 80);
            int grown = resting.State.fruitLedger.matured;
            Step(resting, new WorldAction(Vector2.zero, skill: WorldSkill.Cultivate));
            Require(resting.State.foods[0].phase == FoodPhase.Resting && resting.State.foods[0].phaseProgress == 2 &&
                resting.State.fruitLedger.matured == grown, "休養只加速進度不另產果");
            var waiting = Make(); Until(waiting, 40);
            Step(waiting, new WorldAction(Vector2.zero, skill: WorldSkill.Cultivate));
            Require(waiting.State.individuals[0].skillManaSpent == 0 && waiting.State.foods[0].phase == FoodPhase.Waiting,
                "全部成熟等待期限時不空耗培育");
            var last = Make(); Until(last, 39);
            Step(last, new WorldAction(Vector2.zero, skill: WorldSkill.Cultivate));
            Require(Mathf.Abs(last.State.individuals[0].skillManaSpent - 3) < .001f &&
                Mathf.Abs(last.State.fruits.Last().maturedAt - 39.5f) < .001f &&
                Mathf.Abs(last.State.fruits.Last().expiresAt - 79.5f) < .001f,
                "末果半步成熟只收半步費且成熟後保存不加速");
            var expiry = Make(); expiry.Config.smallFood.lifetimeSeconds = .5f;
            Step(expiry, new WorldAction(Vector2.zero, skill: WorldSkill.Cultivate));
            Require(expiry.State.foods.Count == 0 && Mathf.Abs(expiry.State.individuals[0].skillManaSpent - 3) < .001f,
                "來源半步到期只付有效半步費且不延壽");
            var missing = Make(); missing.State.individuals[0].loadout = new[] { WorldSkill.Sow, WorldSkill.Shield };
            Step(missing, new WorldAction(Vector2.zero, skill: WorldSkill.Cultivate));
            Require(missing.State.individuals[0].skillManaSpent == 0, "沒有培育配置不能施放");
        }
        static void Arbitration()
        {
            var prior = Make(); var other = prior.AddIndividual("Moss");
            other.x = other.y = other.facing = 0; other.loadout = new[] { WorldSkill.Cultivate, WorldSkill.Sow };
            var incumbent = prior.State.individuals[0];
            Step(prior, new WorldAction(Vector2.zero, skill: WorldSkill.Cultivate));
            prior.State.individuals.Reverse();
            prior.Step(prior.State.individuals.ToDictionary(a => a.individual, a => new WorldAction(Vector2.zero, skill: WorldSkill.Cultivate)));
            Require(incumbent.skillManaSpent == 12 && other.skillManaSpent == 0, "同強度先開始者續留，不隨清單順序切換");
            var winners = new HashSet<int>();
            for (int seed = 1; seed <= 40; seed++)
            {
                var world = Make(seed); var second = world.AddIndividual("Moss");
                second.x = second.y = second.facing = 0; second.loadout = new[] { WorldSkill.Cultivate, WorldSkill.Sow };
                world.Step(world.State.individuals.ToDictionary(a => a.individual, a => new WorldAction(Vector2.zero, skill: WorldSkill.Cultivate)));
                winners.Add(world.State.individuals.Single(a => a.skillManaSpent > 0).individual);
                Require(Mathf.Abs(world.State.individuals.Sum(a => a.skillManaSpent) - 6) < .001f,
                    "同時競爭只產生單一有效效果與費用");
            }
            Require(winners.SetEquals(new[] { 1, 2 }), "完全同時的同強度施法者皆可公平抽中");
            var handoff = Make(); var adult = handoff.State.individuals[0]; var child = handoff.AddIndividual("Moss");
            child.x = child.y = child.facing = 0; child.age = 0; child.loadout = new[] { WorldSkill.Cultivate, WorldSkill.Sow };
            handoff.Step(handoff.State.individuals.ToDictionary(a => a.individual,
                a => new WorldAction(Vector2.zero, skill: a == child ? WorldSkill.Cultivate : WorldSkill.None)));
            handoff.Step(handoff.State.individuals.ToDictionary(a => a.individual, a => new WorldAction(Vector2.zero, skill: WorldSkill.Cultivate)));
            Require(Mathf.Abs(handoff.State.foods[0].phaseProgress - 3.3f) < .001f && adult.skillManaSpent == 6 && child.skillManaSpent == 6,
                "強者可接手，交接不重置或再次乘來源進度");
        }
        static void Sowing()
        {
            var smallOnly = Make(); smallOnly.State.foods.Clear(); var planter = smallOnly.State.individuals[0]; planter.x = 14;
            Step(smallOnly, new WorldAction(Vector2.zero, aim: new Vector2(16, 0), skill: WorldSkill.Sow));
            var seed = smallOnly.State.foods.Single();
            Require(seed.growthType == FoodGrowthType.Small && seed.bornAt == smallOnly.State.time && seed.amount == 0 &&
                smallOnly.State.fruits.Count == 0 && !seed.natural && planter.skillManaSpent == 12 &&
                planter.skillReadyTimes[1] - smallOnly.State.time == 8,
                "只容小型時合法播種，零歲零果、費十二與冷卻八秒");
            var cooldown = Make(); cooldown.State.foods.Clear();
            Step(cooldown, new WorldAction(Vector2.zero, skill: WorldSkill.Sow));
            var actor = cooldown.State.individuals[0]; actor.x = 12;
            Step(cooldown, new WorldAction(Vector2.zero, aim: new Vector2(12, 0), skill: WorldSkill.Sow));
            Require(cooldown.State.foods.Count == 1 && actor.skillManaSpent == 12, "冷卻中合法位置也不能重播");
            Until(cooldown, 8);
            Step(cooldown, new WorldAction(Vector2.zero, aim: new Vector2(12, 0), skill: WorldSkill.Sow));
            Require(cooldown.State.foods.Count == 2 && actor.skillManaSpent == 24, "滿八秒後可再次播種");
            var body = Make(); body.State.foods.Clear();
            Step(body, new WorldAction(Vector2.zero, skill: WorldSkill.Sow));
            Require(body.State.foods.Count == 1, "生物本身不阻擋生長圓");
            var rejected = Make(); rejected.State.foods.Clear();
            Step(rejected, new WorldAction(Vector2.zero, aim: new Vector2(4, 0), skill: WorldSkill.Sow));
            Step(rejected, new WorldAction(Vector2.zero, aim: new Vector2(-2, 0), skill: WorldSkill.Sow));
            Require(rejected.State.foods.Count == 0 && rejected.State.individuals[0].skillManaSpent == 0,
                "超距或不可見位置不播種、不付費");
            var ownerDies = Make(); ownerDies.State.foods.Clear();
            Step(ownerDies, new WorldAction(Vector2.zero, skill: WorldSkill.Sow));
            ownerDies.State.individuals[0].health = 0;
            Step(ownerDies, new WorldAction(Vector2.zero));
            Require(ownerDies.State.foods.Count == 1 && ownerDies.State.foods[0].phaseProgress == 1,
                "來源自行生長，不依賴播種者存活");
            int large = 0;
            for (int randomSeed = 1; randomSeed <= 250; randomSeed++)
            {
                var sample = Make(randomSeed); sample.State.foods.Clear();
                Step(sample, new WorldAction(Vector2.zero, skill: WorldSkill.Sow));
                if (sample.State.foods.Single().growthType == FoodGrowthType.Large) large++;
                Require(sample.State.fruitLedger.Residual(sample.State.fruits.Count) == 0, "播種不直接創造果實");
            }
            Require(large >= 30 && large <= 70, "兩型都合法時按八二權重抽選，不能固定小型或大型");
            Debug.Log("播種二百五十種子大型數：" + large);
        }

        public static void RunRegression()
        {
            ValidateFruitCycle.Run();
            ValidateSourcePlacement.Run();
            ValidateFruitOperations.Run();
            ValidateFruitTransfer.Run();
            Run();
        }
        public static void Run()
        {
            var world = Make(); var actor = world.State.individuals[0];
            Step(world, new WorldAction(Vector2.zero, skill: WorldSkill.Cultivate));
            Require(Mathf.Abs(actor.skillManaSpent - 6) < .001f, "培育只按有效一秒耗六魔力，沒有啟動費");
            Require(Mathf.Abs(world.State.foods[0].phaseProgress - 2) < .001f, "培育於同一步加速首次成長兩倍");
            var partial = Make(); var poor = partial.State.individuals[0]; poor.mana = 3;
            Step(partial, new WorldAction(Vector2.zero, skill: WorldSkill.Cultivate));
            Require(Mathf.Abs(poor.skillManaSpent - 3) < .001f && poor.mana >= 0, "不足一秒魔力仍按有效半秒付費");
            Require(Mathf.Abs(partial.State.foods[0].phaseProgress - 1.5f) < .001f, "付得起半秒才加速半秒");
            var shared = Make(); var small = shared.AddIndividual("Moss");
            small.x = small.y = small.facing = 0; small.age = 0; small.loadout = new[] { WorldSkill.Cultivate, WorldSkill.Sow };
            shared.Step(shared.State.individuals.ToDictionary(a => a.individual, a => new WorldAction(Vector2.zero, skill: WorldSkill.Cultivate)));
            Require(Mathf.Abs(shared.State.foods[0].phaseProgress - 2) < .001f &&
                Mathf.Abs(shared.State.individuals.Sum(a => a.skillManaSpent) - 6) < .001f, "同來源只取最強培育且不重複扣費");
            var overlap = Make(); var planter = overlap.State.individuals[0];
            Step(overlap, new WorldAction(Vector2.zero, aim: new Vector2(3, 0), skill: WorldSkill.Sow));
            Require(overlap.State.foods.Count == 1 && planter.skillManaSpent == 0 && planter.skillReadyTimes[1] == 0,
                "生長圓重疊播種失敗不扣魔不冷卻");
            Switching();
            Targets();
            Boundaries();
            Arbitration();
            Sowing();
            Debug.Log("生產技能驗收通過：有效耗魔、成長界限、交接、中斷、合法播種與種子重現");
        }
    }
}
