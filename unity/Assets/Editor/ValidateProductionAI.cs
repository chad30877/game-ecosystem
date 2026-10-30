using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Ecosystem.Editor
{
    public static class ValidateProductionAI
    {
        static void Require(bool condition,string message) { if(!condition) throw new Exception(message); }
        static World Make(int seed=101)
        {
            var world=new World(new WorldConfig { foraging=true,halfExtent=20,naturalFoodLimit=0,
                initialSpecies="Moss",individualsPerSpecies=1,stepSeconds=.1f,episodeSteps=2000,
                observationRadius=8,disableAging=true,disableReproduction=true,idleCost=0,scanEnabled=false,
                manaRecoveryRate=.00001f,nutritionBudget=.00001f },seed);
            world.Reset();var actor=world.State.individuals[0];actor.x=actor.y=actor.facing=0;
            actor.satiety=70;actor.loadout=new[]{WorldSkill.Cultivate,WorldSkill.Sow};return world;
        }
        static FoodState Source(World world,float x=1,float y=0,FoodPhase phase=FoodPhase.Recovering)
        {
            var source=new FoodState { food=++world.State.nextFood,x=x,y=y };
            FruitRules.Initialize(world.Config,source,world.State.time);source.phase=phase;
            world.State.foods.Add(source);world.RefreshPerception();return source;
        }
        static WorldSkill Skill(IndividualState actor,WorldAction action) => action.skillSlot>=0 ? actor.loadout[action.skillSlot] : action.skill;
        static void Step(World world)
        {
            world.Step(world.State.individuals.ToDictionary(a=>a.individual,a=>ForagingController.Decide(world,a)));
        }
        public static void RunAll()
        {
            RunAI();ValidateFoodCareAI.RunAll();
            Debug.Log("生產AI與01至08及煙幕整合回歸通過");
        }
        public static void RunAI()
        {
            RunCultivate();RunSupply();RunRetry();RunCoordination();RunTarget();RunSafety();RunWait();RunReasons();RunLimits();RunExtended();
            Debug.Log("局部供給、播種與培育選擇AI驗收通過");
        }
        public static void RunCultivate()
        {
            var world=Make();var actor=world.State.individuals[0];actor.mana=1;var source=Source(world);
            var action=ForagingController.Decide(world,actor);
            Require(Skill(actor,action)==WorldSkill.Cultivate && action.movement==Vector2.zero,
                "低魔力有益幼株應停下培育，不能使用舊啟動費門檻");
            Step(world);Require(actor.skillManaSpent>.59f && actor.skillManaSpent<.61f && source.phaseProgress>.19f,
                "規則AI實際培育以有效時間扣魔並於同一步加速");
            Debug.Log("生產AI培育驗收通過");
        }
        public static void RunSupply()
        {
            var world=Make();var actor=world.State.individuals[0];Source(world,6);
            Require(Skill(actor,ForagingController.Decide(world,actor))!=WorldSkill.Sow,
                "半徑八內有已知未成熟來源，不能因暫空就追加播種");
            var empty=Make();var caster=empty.State.individuals[0];empty.RefreshPerception();
            var action=ForagingController.Decide(empty,caster);
            Require(Skill(caster,action)==WorldSkill.Sow,"沒有局部來源且能等待時考慮播種");
            empty.Step(new Dictionary<int,WorldAction>{[caster.individual]=action});
            Require(empty.State.foods.Count==1 && empty.State.foods[0].bornAt==empty.State.time &&
                empty.State.foods[0].amount==0 && caster.skillManaSpent==12,"AI選點後由世界建立零歲零果新來源");
            Debug.Log("生產AI局部供給與播種驗收通過");
        }
        public static void RunRetry()
        {
            var world=Make();var actor=world.State.individuals[0];Source(world,-4);
            Require(actor.foodMemory.Count==0,"阻擋來源仍在視野外");
            var first=ForagingController.Decide(world,actor);Require(Skill(actor,first)==WorldSkill.Sow,"未知阻擋不應預先透露");
            world.Step(new Dictionary<int,WorldAction>{[actor.individual]=first});
            Require(actor.skillManaSpent==0 && actor.skillCooldowns.All(c=>c==0) && actor.foodMemory.Count==0,
                "失敗不扣魔或冷卻，不揭露隱藏來源");
            for(int i=1;i<10;i++)
            {
                var action=ForagingController.Decide(world,actor);
                Require(Skill(actor,action)!=WorldSkill.Sow,"播種失敗後未滿一秒不得每步重試");
                world.Step(new Dictionary<int,WorldAction>{[actor.individual]=action});
            }
            Require(Skill(actor,ForagingController.Decide(world,actor))==WorldSkill.Sow,"至少一秒後重新評估播種");
            Debug.Log("生產AI失敗重試與隱藏來源驗收通過");
        }
        public static void RunCoordination()
        {
            var world=Make();var first=world.State.individuals[0];first.facing=45;
            var second=world.AddIndividual("Moss");second.x=0;second.y=.2f;second.facing=-45;second.satiety=70;
            second.loadout=new[]{WorldSkill.Cultivate,WorldSkill.Sow};var source=Source(world);
            world.Step(new Dictionary<int,WorldAction> { [first.individual]=new WorldAction(Vector2.zero,skill:WorldSkill.Cultivate),
                [second.individual]=new WorldAction(Vector2.zero) });
            Require(Skill(first,ForagingController.Decide(world,first))==WorldSkill.Cultivate,"先開始且有效者續留");
            Require(Skill(second,ForagingController.Decide(world,second))!=WorldSkill.Cultivate,
                "依可見資訊避免對同強度既有培育者重複投入");
            for(int i=0;i<10;i++) Step(world);
            Require(first.cultivatingFood==source.food && first.skillManaSpent>6 && second.skillManaSpent==0,
                "多人規則AI不會同停同開，既有培育者穩定持續");
            Debug.Log("生產AI培育協調驗收通過");
        }
        public static void RunTarget()
        {
            var world=Make();world.Config.smallFood.growthRadius=.1f;var actor=world.State.individuals[0];var original=Source(world);
            Step(world);Source(world,.5f);
            actor.foodMemory.Add(new FoodMemory { food=9999,x=-6,lastSeenAt=-40,stale=true,phase=FoodPhase.Resting });
            var continued=ForagingController.Decide(world,actor);
            Require(Skill(actor,continued)==WorldSkill.Cultivate && continued.productionSource==original.food,
                "有效培育不因較近新株或另一筆陳舊情報改換目標");
            Debug.Log("生產AI目標續留驗收通過");
        }
        public static void RunSafety()
        {
            var world=Make();var actor=world.State.individuals[0];Source(world);
            var enemy=world.AddIndividual("Flame");enemy.x=1;enemy.y=.5f;world.RefreshPerception();
            var action=ForagingController.Decide(world,actor);
            Require(Skill(actor,action)!=WorldSkill.Cultivate && action.movement.sqrMagnitude>0 &&
                Vector2.Dot(action.movement,new Vector2(enemy.x-actor.x,enemy.y-actor.y))<0,
                "近身危險優先於生產，不能滿飽食就繼續施法");
            Debug.Log("生產AI安全優先序驗收通過");
        }
        public static void RunWait()
        {
            var world=Make();var actor=world.State.individuals[0];actor.satiety=30;
            world.Config.idleCost=3;
            Require(Skill(actor,ForagingController.Decide(world,actor))!=WorldSkill.Sow,
                "飽食三十仍須估計能等待新來源，不能只檢查固定門檻");
            var family=Make();var parent=family.State.individuals[0];parent.satiety=70;
            var child=family.AddIndividual("Moss");child.x=6;child.y=0;child.facing=180;child.age=0;
            child.parentId=parent.individual;child.satiety=30;
            for(int i=0;i<3;i++) family.State.fruits.Add(new FruitState { fruit=++family.State.nextFruit,
                holder=parent.individual,expiresAt=9 });
            family.State.fruitLedger.initial=3;family.RefreshPerception();
            Require(Skill(parent,ForagingController.Decide(family,parent))==WorldSkill.Sow,
                "滿載但舊果無法送達且有已知照顧需求，可考慮補種新鮮供給");
            Debug.Log("生產AI等待估計與滿載需求驗收通過");
        }
        public static void RunReasons()
        {
            var world=Make();var actor=world.State.individuals[0];Source(world);Step(world);
            Require(JsonUtility.ToJson(actor).Contains("\"productionDecision\":\"cultivate\""),
                "世界快照保留當時生產選擇原因");
            actor.mana=0;ForagingController.Decide(world,actor);
            Require(JsonUtility.ToJson(actor).Contains("\"productionStopReason\":\"no_mana\""),
                "快照可還原因魔力不足停止培育");
            actor.satiety=45;world.State.fruits.Add(new FruitState { fruit=++world.State.nextFruit,
                holder=actor.individual,expiresAt=40 });world.RefreshPerception();
            Require(ForagingController.Decide(world,actor).foodAction==FoodAction.Eat &&
                JsonUtility.ToJson(actor).Contains("\"productionStopReason\":\"food_priority\""),
                "急需吃果優先且記錄培育停止原因");
            Debug.Log("生產AI選擇與停止原因快照驗收通過");
        }
        public static void RunLimits()
        {
            var packed=Make();packed.Config.halfExtent=4;var trapped=packed.State.individuals[0];
            var source=Source(packed,0,0,FoodPhase.Waiting);source.waitUntil=40;
            trapped.areaMemory.Clear();trapped.explorationTargetX=-1;
            for(int x=0;x<4;x++) for(int y=0;y<4;y++) trapped.areaMemory.Add(new AreaMemory { cellX=x,cellY=y,lastSeenAt=0 });
            var unavailable=ForagingController.Decide(packed,trapped);
            Require(Skill(trapped,unavailable)!=WorldSkill.Sow &&
                (unavailable.movement.sqrMagnitude>0 || unavailable.turn!=0),"沒有候選位置仍移動或掃視探索，不在原地重試");
            var world=Make();var actor=world.State.individuals[0];world.RefreshPerception();actor.mana=39;
            Require(Skill(actor,ForagingController.Decide(world,actor))!=WorldSkill.Sow,"魔力未滿四十不播種");
            actor.mana=40;actor.satiety=29;
            Require(Skill(actor,ForagingController.Decide(world,actor))!=WorldSkill.Sow,"飽食未滿三十不播種");
            actor.satiety=30;
            Require(Skill(actor,ForagingController.Decide(world,actor))==WorldSkill.Sow,"恰四十魔力三十飽食且能等待可播種");
            var limited=Make();limited.Config.observationRadius=1;var viewer=limited.State.individuals[0];limited.RefreshPerception();
            var visible=ForagingController.Decide(limited,viewer);
            Require(Skill(viewer,visible)==WorldSkill.Sow && visible.aim.magnitude<=1.00001f &&
                World.InView(limited.Config,viewer,visible.aim),"自訂短視野中的播種候選仍在可見範圍");
            var full=Make();var carrier=full.State.individuals[0];
            for(int i=0;i<3;i++) full.State.fruits.Add(new FruitState { fruit=++full.State.nextFruit,holder=carrier.individual,expiresAt=40 });
            full.RefreshPerception();Require(Skill(carrier,ForagingController.Decide(full,carrier))!=WorldSkill.Sow,"滿載且無已知照顧需求不播種");
            var resting=Make();var gardener=resting.State.individuals[0];Source(resting,1,0,FoodPhase.Resting);
            Require(Skill(gardener,ForagingController.Decide(resting,gardener))==WorldSkill.Cultivate,"休養空株仍可培育");
            var waiting=Make();var idle=waiting.State.individuals[0];Source(waiting,1,0,FoodPhase.Waiting).waitUntil=40;
            Require(Skill(idle,ForagingController.Decide(waiting,idle))!=WorldSkill.Cultivate,"全部成熟等待期限時不培育");
            var fruitWorld=Make();var gatherer=fruitWorld.State.individuals[0];var ripe=Source(fruitWorld,.5f,0,FoodPhase.Waiting);ripe.waitUntil=40;
            fruitWorld.State.fruits.Add(new FruitState { fruit=++fruitWorld.State.nextFruit,source=ripe.food,x=.5f,expiresAt=40 });
            fruitWorld.RefreshPerception();Require(ForagingController.Decide(fruitWorld,gatherer).foodAction==FoodAction.Gather,"已有可用果先採，不因等待階段改播種");
            var noSkill=Make();var plain=noSkill.State.individuals[0];plain.loadout=new[]{WorldSkill.EntanglingRoots,WorldSkill.SporeSmoke};Source(noSkill);
            var unsupported=ForagingController.Decide(noSkill,plain);
            Require(Skill(plain,unsupported)!=WorldSkill.Cultivate && Skill(plain,unsupported)!=WorldSkill.Sow,"沒有配置的生產技能不能憑空使用");
            Debug.Log("生產AI階段、容量、門檻與候選邊界驗收通過");
        }
        static IndividualState Partner(World world)
        {
            var first=world.State.individuals[0];first.facing=45;
            var second=world.AddIndividual("Moss");second.x=0;second.y=.2f;second.facing=-45;second.satiety=70;
            second.loadout=new[]{WorldSkill.Cultivate,WorldSkill.Sow};return second;
        }
        public static void RunExtended()
        {
            var winners=new HashSet<int>();
            for(int seed=1;seed<=40;seed++)
            {
                var simultaneous=Make(seed);Partner(simultaneous);var source=Source(simultaneous);Step(simultaneous);
                var winner=simultaneous.State.individuals.Single(a=>a.cultivatingFood==source.food);winners.Add(winner.individual);
                Require(simultaneous.State.individuals.Count(a=>a.skillManaSpent>0)==1,"完全同時的規則AI由世界公平選一位，不重複耗魔");
                Step(simultaneous);Require(winner.cultivatingFood==source.food,"同時仲裁後勝者續留，不雙停雙開");
            }
            Require(winners.SetEquals(new[]{1,2}),"四十個種子涵蓋兩位同時候選，沒有固定個體偏好");
            var handoff=Make();var weak=handoff.State.individuals[0];var adult=Partner(handoff);weak.age=0;
            weak.health=CombatRules.CurrentMaxHealth(handoff.Config,weak);var growing=Source(handoff);
            handoff.Step(new Dictionary<int,WorldAction> { [weak.individual]=new WorldAction(Vector2.zero,skill:WorldSkill.Cultivate),
                [adult.individual]=new WorldAction(Vector2.zero) });
            Require(Skill(adult,ForagingController.Decide(handoff,adult))==WorldSkill.Cultivate,"可見較弱既有培育者不阻擋強者接手");
            Step(handoff);Require(adult.cultivatingFood==growing.food && weak.cultivatingFood==0,"規則AI實際交接至較強效果");
            Require(Skill(weak,ForagingController.Decide(handoff,weak))!=WorldSkill.Cultivate,"弱者觀察到強者後避免重複投入");
            var first=Make(505);var second=Make(505);Source(first,-4);second.RefreshPerception();
            var left=ForagingController.Decide(first,first.State.individuals[0]);
            var right=ForagingController.Decide(second,second.State.individuals[0]);
            Require(left.skill==right.skill && left.aim==right.aim && left.movement==right.movement,
                "相同觀察與種子、不同隱藏來源，生產選擇與候選點相同");
            var points=new HashSet<Vector2>();
            for(int seed=1;seed<=12;seed++)
            {
                var seeded=Make(seed);seeded.RefreshPerception();points.Add(ForagingController.Decide(seeded,seeded.State.individuals[0]).aim);
            }
            Require(points.Count>1,"候選安全點依種子抽選，不是固定朝前無效點");
            var nearest=Make();nearest.Config.smallFood.growthRadius=.1f;var gardener=nearest.State.individuals[0];
            Source(nearest,1.3f);var close=Source(nearest,.6f);
            Require(ForagingController.Decide(nearest,gardener).productionSource==close.food,"新培育目標先選較近可見有益來源");
            var hungry=Make();var eater=hungry.State.individuals[0];var plant=Source(hungry);Step(hungry);
            eater.satiety=45;hungry.State.fruits.Add(new FruitState { fruit=++hungry.State.nextFruit,holder=eater.individual,expiresAt=40 });
            hungry.State.fruitLedger.initial++;hungry.RefreshPerception();Step(hungry);
            Require(eater.foodOperation==FoodAction.Eat && eater.cultivatingFood==0 && eater.productionStopReason=="food_priority" &&
                plant.phaseProgress>.299f && plant.phaseProgress<.301f,"急需食物在實際世界中停止培育且不留加速尾效");
            var caring=Make();var parent=caring.State.individuals[0];Source(caring);Step(caring);
            var child=caring.AddIndividual("Moss");child.x=.5f;child.y=0;child.facing=180;child.age=0;child.parentId=parent.individual;child.satiety=30;
            caring.State.fruits.Add(new FruitState { fruit=++caring.State.nextFruit,holder=parent.individual,expiresAt=40 });
            caring.State.fruitLedger.initial++;caring.RefreshPerception();Step(caring);
            Require(parent.cultivatingFood==0 && parent.deliveryInviteTo==child.individual && parent.productionStopReason=="food_priority",
                "已知幼體送果需求打斷培育並發出實際邀請");
            Debug.Log("生產AI公平交接、隱藏對照及急事插入驗收通過");
        }
    }
}
