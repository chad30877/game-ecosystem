using System;
using System.Linq;
using UnityEngine;
namespace Ecosystem.Editor
{
    public static class ValidateFruitPerception
    {
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        static World Make()
        {
            var world = new World(new WorldConfig { foraging=true, halfExtent=20, naturalFoodLimit=0, observationRadius=6,
                individualsPerSpecies=2, disableReproduction=true, disableAging=true, idleCost=0, scanEnabled=false }, 101);
            world.Reset();
            foreach(var actor in world.State.individuals) { actor.x=-15; actor.y=-15; }
            var observer=world.State.individuals[0]; observer.x=observer.y=observer.facing=0;
            return world;
        }
        public static void RunAll()
        {
            RunOccludedTarget(); Limits(); Family(); Isolation();
            ValidateSmoke.Run();ValidateLoadMovement.Run();
            Debug.Log("果實感知與有限記憶驗收通過：粗略新鮮度、資訊隔離、三種記憶及親子關係");
        }
        static void Limits()
        {
            var world=Make();var observer=world.State.individuals[0];world.Config.foodMemoryLimit=64;
            for(int id=1;id<=40;id++) world.State.foods.Add(new FoodState {
                food=id,x=1+id*.01f,capacity=4,phase=(FoodPhase)(id%4),mature=id%4!=2 });
            world.RefreshPerception(true);Require(observer.foodMemory.Count==32 && observer.visibleFoods.Count==32,"來源記憶最多三十二筆");
            Require(observer.foodMemory.Select(f=>f.phase).Distinct().Count()==4,"來源四階段皆可辨識");
            observer.facing=180;world.State.time=31;world.RefreshPerception();
            Require(observer.foodMemory.Count==32 && observer.foodMemory.All(f=>f.stale && !f.possiblyAvailable),
                "三十秒來源只變陳舊，不刪除或推測回滿");
            world.State.foods.Clear();world.RefreshPerception();Require(observer.foodMemory.Count==32,"視野外來源移除不更新記憶");
            observer.facing=0;world.RefreshPerception();Require(observer.foodMemory.Count==0,"回訪來源落空清除");
            var ground=Make();var viewer=ground.State.individuals[0];
            for(int id=1;id<=20;id++) ground.State.fruits.Add(new FruitState { fruit=id,x=1+id*.01f,expiresAt=40 });
            ground.RefreshPerception(true);Require(viewer.groundFruitMemory.Count==16,"地面記憶最多十六顆");
            viewer.facing=180;ground.State.fruits.Clear();ground.State.time=9.99f;ground.RefreshPerception();
            Require(viewer.groundFruitMemory.Count==16,"視野外被拾取不直接清除且十秒前仍可回訪");
            ground.State.time=10;ground.RefreshPerception();Require(viewer.groundFruitMemory.Count==0,"地面線索十秒失效");
            var revisit=Make();var eye=revisit.State.individuals[0];
            revisit.State.fruits.Add(new FruitState { fruit=1,x=1,expiresAt=40 });revisit.RefreshPerception(true);
            eye.facing=180;revisit.State.fruits.Clear();revisit.State.time=1;revisit.RefreshPerception();
            Require(eye.groundFruitMemory.Count==1,"地面失去視線保留最後觀察");
            eye.facing=0;revisit.RefreshPerception();Require(eye.groundFruitMemory.Count==0,"回訪地面落空清除");
        }
        static void Family()
        {
            var world=Make();var parent=world.State.individuals[0];var child=world.State.individuals[1];
            child.x=1;child.y=0;child.age=0;child.parentId=parent.individual;
            world.State.foods.Add(new FoodState { food=1,x=1.5f,capacity=4,phase=FoodPhase.Available,mature=true });
            world.State.fruits.Add(new FruitState { fruit=1,holder=child.individual,expiresAt=40 });
            world.State.fruits.Add(new FruitState { fruit=2,source=1,x=1.5f,expiresAt=40,claimedBy=child.individual });
            child.foodOperation=FoodAction.Gather;child.operationFruit=2;world.RefreshPerception(true);
            var known=parent.foodInteractions.Single();var family=parent.children.Single();
            Require(known.carriedCount==1 && known.operation==FoodAction.Gather && known.hasSource && known.sourceX==1.5f,
                "持果、採集互動及可見目標位置可辨識");
            Require(parent.visibleFoods.Single().fruits.Single().claimedBy==child.individual,"可見暫占者可辨識");
            Require(family.food!=null && family.food.carriedCount==1,"親子記憶有已知持果狀態");
            child.foodOperation=FoodAction.None;child.cultivatingFood=1;child.cultivateGrowth=1;world.RefreshPerception();
            Require(known.cultivatingFood==1 && known.cultivationStrength==1,"可見培育來源與強度可辨識");
            parent.facing=180;world.State.time=2;child.cultivatingFood=0;world.State.fruits.RemoveAll(f=>f.holder==child.individual);
            family.searched=true;world.RefreshPerception();
            Require(known.cultivatingFood==1 && known.carriedCount==1 && known.lastSeenAt==0,"他人視野外變化不更新觀察");
            world.State.time=4.99f;world.RefreshPerception();Require(family.food!=null,"五秒前持果記憶仍已知");
            world.State.time=5;world.RefreshPerception();
            Require(family.food==null && parent.foodInteractions.Count==0 && known.carriedCount==-1,
                "五秒後未知，不當成零顆");
            Require(parent.children.Count==1 && family.searched && !family.confirmedDead,"持果過時不忘記親子或重設已搜尋位置");
            parent.facing=0;world.RefreshPerception();Require(family.food!=null && family.food.carriedCount==0 && !family.searched,
                "重見更新零顆與搜尋狀態");
            var infant=Make();var juvenile=infant.State.individuals[0];var adult=infant.State.individuals[1];
            juvenile.parentId=adult.individual;adult.x=1;adult.y=0;infant.RefreshPerception(true);
            juvenile.facing=180;infant.RefreshPerception();
            Require(!infant.ObserveEntities(juvenile).Any(row=>row[0]==-3),"幼體不新增視野外親代追蹤");
        }
        static string Rows(World world,IndividualState observer) => string.Join(";",world.ObserveEntities(observer)
            .Select(row=>string.Join(",",row.Select(v=>v.ToString("R",System.Globalization.CultureInfo.InvariantCulture)))));
        static World Twin(float deadline)
        {
            var world=Make();var actor=world.State.individuals[1];actor.x=1;actor.y=0;
            world.State.foods.Add(new FoodState { food=1,x=2,capacity=4,phase=FoodPhase.Available,mature=true,
                maturitySchedule=deadline==40 ? new[]{0f,30f} : new[]{0f,14f,30f}, bornAt=deadline==40 ? 0 : -10 });
            world.State.fruits.Add(new FruitState { fruit=1,source=1,x=2,expiresAt=deadline });
            world.State.fruits.Add(new FruitState { fruit=2,x=1.5f,expiresAt=deadline });
            world.State.fruits.Add(new FruitState { fruit=3,holder=actor.individual,expiresAt=deadline });
            world.RefreshPerception(true);return world;
        }
        static void Isolation()
        {
            var left=Twin(40);var right=Twin(35);var a=left.State.individuals[0];var b=right.State.individuals[0];
            Require(JsonUtility.ToJson(a)==JsonUtility.ToJson(b) && Rows(left,a)==Rows(right,b),
                "同粗略可見資訊不得因精確期限、年齡或未來額度不同而洩漏");
            a.facing=b.facing=180;left.State.time=right.State.time=3;
            right.State.foods.Clear();right.State.fruits.Clear();right.State.individuals.RemoveAt(1);
            left.RefreshPerception();right.RefreshPerception();
            Require(JsonUtility.ToJson(a)==JsonUtility.ToJson(b) && Rows(left,a)==Rows(right,b),
                "視野外果實、來源、持果與死亡變化不改觀察");
            Require(left.Observe(a).Length==19 && left.ObserveEntities(a).All(row=>row.Length==10 && row.All(v=>float.IsFinite(v))),
                "自身十九維與十維列契約維持且無非有限值");
            a.facing=b.facing=0;left.RefreshPerception();right.RefreshPerception();
            Require(a.foodMemory.Count==1 && b.foodMemory.Count==0 && a.groundFruitMemory.Count==1 && b.groundFruitMemory.Count==0,
                "重新觀察才修正已消失來源與地面果實");
        }
        public static void RunOccludedTarget()
        {
            RunAnchors();var world=Make();var observer=world.State.individuals[0];var actor=world.State.individuals[1];
            actor.x=Mathf.Cos(58*Mathf.Deg2Rad)*2;actor.y=Mathf.Sin(58*Mathf.Deg2Rad)*2;
            var point=new Vector2(Mathf.Cos(65*Mathf.Deg2Rad)*2,Mathf.Sin(65*Mathf.Deg2Rad)*2);
            world.State.foods.Add(new FoodState { food=1,x=point.x,y=point.y,capacity=4 });
            world.State.fruits.Add(new FruitState { fruit=1,source=1,x=point.x,y=point.y,expiresAt=40 });
            actor.foodOperation=FoodAction.Gather;actor.operationFruit=1;world.RefreshPerception(true);
            Require(observer.foodInteractions.Single().operation==FoodAction.Gather &&
                observer.foodInteractions.Single().operationFruit==0 && !observer.foodInteractions.Single().hasSource,
                "可見採集者不能揭露視野外果實識別與位置");
            Debug.Log("互動目標視野邊界驗收通過");
        }
        public static void RunAnchors()
        {
            RunIndividuals(); var world=Make();var observer=world.State.individuals[0];
            world.State.foods.Add(new FoodState { food=1,x=1,capacity=4,phase=FoodPhase.Available,mature=true });
            var fruit=new FruitState { fruit=1,source=1,x=1,expiresAt=40 };world.State.fruits.Add(fruit);
            world.RefreshPerception(true);world.State.time=8;fruit.source=0;fruit.holder=observer.individual;
            world.RefreshPerception();
            Require(observer.ownFruits.Single().visible && observer.ownFruits.Single().firstSeenAt==0,
                "採入自己時須沿用已知果實新鮮度，且保持自身可見");
            world.State.time=12;fruit.holder=0;fruit.x=1;world.RefreshPerception();
            Require(observer.groundFruitMemory.Single().firstSeenAt==0, "放下不得重設已知果實觀察區間");
            var offered=Make();var receiver=offered.State.individuals[0];var sender=offered.State.individuals[1];
            sender.x=1;sender.y=0;sender.deliveryInviteTo=receiver.individual;sender.deliveryInviteFruit=1;
            offered.State.fruits.Add(new FruitState { fruit=1,holder=sender.individual,expiresAt=40 });
            offered.RefreshPerception(true);offered.State.time=12;offered.RefreshPerception();
            Require(receiver.foodInteractions.Single().offeredFruit.firstSeenAt==0, "重見邀請不刷新果實區間");
            offered.State.time=13;sender.deliveryInviteTo=0;offered.RefreshPerception();
            offered.State.time=14;sender.deliveryInviteTo=receiver.individual;offered.RefreshPerception();
            Require(receiver.foodInteractions.Single().offeredFruit.firstSeenAt==0, "同顆果實暫停邀請再顯示不得刷新區間");
            offered.State.time=31;offered.RefreshPerception();offered.State.time=34;offered.RefreshPerception();
            Require(receiver.foodInteractions.Single().offeredFruit.freshnessSince==31 &&
                receiver.foodInteractions.Single().offeredFruit.EstimateRemaining(34)<=3.5f, "重見將腐爛狀態不刷新十秒區間");
            Debug.Log("果實觀察區間驗收通過");
        }
        public static void RunIndividuals()
        {
            RunGround(); var world=Make(); var observer=world.State.individuals[0];var target=world.State.individuals[1];
            target.x=1;target.y=0;
            world.State.fruits.Add(new FruitState { fruit=1,holder=target.individual,expiresAt=40 });
            world.RefreshPerception(true);
            Require(world.ObserveEntities(observer).Any(row=>row[0]==-8 && row[4]>0), "他人持果與當前食物互動須可觀察");
            Debug.Log("個體果實互動感知驗收通過");
        }
        public static void RunGround()
        {
            RunSources(); var world=Make(); var observer=world.State.individuals[0];
            world.State.fruits.Add(new FruitState { fruit=1,x=1,expiresAt=40 });
            world.RefreshPerception(true);
            Require(world.ObserveEntities(observer).Any(row=>row[0]==-7), "地面果實須提供可見與有限記憶");
            Debug.Log("地面果實感知驗收通過");
        }
        public static void RunSources()
        {
            Run(); var world=Make(); var observer=world.State.individuals[0];
            world.State.foods.Add(new FoodState { food=1,x=1,capacity=4,phase=FoodPhase.Available,mature=true,amount=1 });
            world.State.fruits.Add(new FruitState { fruit=1,source=1,x=1,expiresAt=35 });
            world.RefreshPerception(true);
            Require(world.ObserveEntities(observer).Any(row=>row[0]==-9), "可見來源須逐顆提供粗略果實觀察");
            Debug.Log("來源果實感知驗收通過");
        }
        public static void Run()
        {
            var world=Make(); var observer=world.State.individuals[0];
            world.State.fruits.Add(new FruitState { fruit=++world.State.nextFruit, holder=observer.individual, expiresAt=40 });
            world.RefreshPerception(true);
            Require(world.ObserveEntities(observer).Any(row=>row[0]==-6), "自身果實須提供粗略觀察，不能由 AI 讀精確期限");
            Debug.Log("果實感知首輪驗收通過");
        }
    }
}
