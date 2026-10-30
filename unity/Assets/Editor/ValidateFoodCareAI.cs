using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Ecosystem.Editor
{
    public static class ValidateFoodCareAI
    {
        static void Require(bool value,string message) { if(!value) throw new Exception(message); }
        static World Make(int seed=101)
        {
            var world=new World(new WorldConfig { foraging=true,halfExtent=20,naturalFoodLimit=0,
                initialSpecies="Flame",individualsPerSpecies=3,stepSeconds=.1f,episodeSteps=2000,
                observationRadius=8,disableAging=true,disableReproduction=true,idleCost=0,scanEnabled=false },seed);
            world.Reset();
            foreach(var actor in world.State.individuals) { actor.x=-15;actor.y=-15;actor.satiety=70; }
            var first=world.State.individuals[0];first.x=first.y=first.facing=0;
            world.RefreshPerception(true);return world;
        }
        static FruitState Give(World world,IndividualState actor,float expiry=40)
        {
            var fruit=new FruitState { fruit=++world.State.nextFruit,holder=actor.individual,expiresAt=expiry };
            world.State.fruits.Add(fruit);world.State.fruitLedger.initial++;return fruit;
        }
        static void Step(World world,bool all=false)
        {
            var actions=world.State.individuals.ToDictionary(a=>a.individual,a=>all||a==world.State.individuals[0] ?
                ForagingController.Decide(world,a) : new WorldAction(Vector2.zero));
            world.Step(actions);
        }
        public static void RunAll()
        {
            RunAI();ValidateFruitPerception.RunAll();
            Debug.Log("採食照顧AI與01至07整合回歸通過");
        }
        public static void RunAI()
        {
            RunEating();RunCare();RunGather();RunEscape();RunInvitation();RunTies();RunSearch();RunSwap();
            RunReceiverRisk();RunUnsafeChild();Thresholds();Selection();Continuation();IsolationAndBirth();
            Debug.Log("採食、送果、接收與逃生減重AI驗收通過");
        }
        static void Family(World world,float parentSatiety=70,float childSatiety=30,float childX=.5f)
        {
            var parent=world.State.individuals[0];var child=world.State.individuals[1];
            parent.satiety=parentSatiety;child.x=childX;child.y=0;child.facing=180;child.age=0;child.parentId=parent.individual;child.satiety=childSatiety;
        }
        static void Thresholds()
        {
            var world=Make();var parent=world.State.individuals[0];var child=world.State.individuals[1];
            Family(world,45);Give(world,parent);world.RefreshPerception(true);
            Require(ForagingController.Decide(world,parent).foodAction==FoodAction.Eat,"親代四十五自救優先於送果");
            parent.satiety=49;world.RefreshPerception();Require(ForagingController.Decide(world,parent).foodAction!=FoodAction.Deliver,"親代未滿五十不開始新交付");
            parent.satiety=50;world.RefreshPerception();Require(ForagingController.Decide(world,parent).foodAction==FoodAction.Deliver,"親代五十開始交付");
            child.satiety=50;world.RefreshPerception();Require(ForagingController.Decide(world,parent).foodAction==FoodAction.Deliver,"照顧啟動後超過四十仍持續至六十");
            child.satiety=60;world.RefreshPerception();Require(ForagingController.Decide(world,parent).foodAction!=FoodAction.Deliver,"幼體六十停止照顧");
            child.satiety=30;child.age=world.Config.maturityAge;world.RefreshPerception();
            Require(ForagingController.Decide(world,parent).foodAction!=FoodAction.Deliver,"已成年子女停止主動送果");
            child.age=0;child.satiety=30;parent.satiety=70;Give(world,child);world.RefreshPerception();
            Require(ForagingController.Decide(world,parent).foodAction!=FoodAction.Deliver,"已知幼體有果不再送果");
            Require(ForagingController.Decide(world,child).foodAction==FoodAction.Eat,"有果幼體先吃");
            world.State.fruits.RemoveAll(f=>f.holder==child.individual);parent.facing=180;world.State.time=6;world.RefreshPerception();
            Require(parent.children.Single().food==null && ForagingController.Decide(world,parent).foodAction!=FoodAction.Deliver,"持果未知不能當成零顆啟動交付");
        }
        static void Selection()
        {
            var world=Make();var parent=world.State.individuals[0];var near=world.State.individuals[1];var far=world.State.individuals[2];
            Family(world,70,38);parent.facing=45;far.parentId=parent.individual;far.age=0;far.satiety=30;far.x=0;far.y=3;Give(world,parent);world.RefreshPerception(true);
            Require(ForagingController.Decide(world,parent).recipient==near.individual,"最低需求相差十內選較近幼體");
            far.satiety=20;world.RefreshPerception();var toward=ForagingController.Decide(world,parent);
            Require(parent.decisionReason=="care_approach" && toward.movement.y>0,"超過十差距先照顧較低飽食幼體");
            var fruitWorld=Make();var actor=fruitWorld.State.individuals[0];var old=Give(fruitWorld,actor);fruitWorld.RefreshPerception(true);
            fruitWorld.State.time=2;fruitWorld.State.step=20;var fresh=Give(fruitWorld,actor,42);actor.satiety=65;fruitWorld.RefreshPerception();
            Require(ForagingController.Decide(fruitWorld,actor).fruit==old.fruit,"自用偏較舊且估計吃得完者");
            Family(fruitWorld);fruitWorld.RefreshPerception();Require(ForagingController.Decide(fruitWorld,actor).fruit==fresh.fruit,"送果偏較新鮮者");
            var loaded=Make();var heavy=loaded.State.individuals[0];Family(loaded,70,30,4.9f);
            for(int i=0;i<3;i++) Give(loaded,heavy,9);loaded.RefreshPerception(true);
            ForagingController.Decide(loaded,heavy);Require(heavy.decisionReason!="care_approach","交付估計使用三果負重走速而非奔跑或零負重");
            var light=Make();var carrier=light.State.individuals[0];Family(light,70,30,4.9f);Give(light,carrier,9);light.RefreshPerception(true);
            ForagingController.Decide(light,carrier);Require(carrier.decisionReason=="care_approach","同粗略期限單果走速可估計送達");
            var close=Make();var eater=close.State.individuals[0];eater.satiety=65;Give(close,eater,9);close.RefreshPerception(true);
            close.State.time=5.8f;close.State.step=58;close.RefreshPerception();
            Require(eater.ownFruits.Single().EstimateRemaining(close.State.time)>2 && eater.ownFruits.Single().EstimateRemaining(close.State.time)<2.2f,
                "固定估計略多於兩秒");
            Require(ForagingController.Decide(close,eater).foodAction==FoodAction.Eat,"不加入額外兩秒緩衝");
            for(int i=0;i<20;i++) Step(close);Require(eater.satiety==85,"略多於兩秒估計可實際完成進食");
        }
        static void Continuation()
        {
            var world=Make();var parent=world.State.individuals[0];var child=world.State.individuals[1];Family(world);var fruit=Give(world,parent);
            parent.deliveryInviteTo=child.individual;parent.deliveryInviteFruit=fruit.fruit;world.RefreshPerception(true);Step(world,true);
            Require(parent.foodOperation==FoodAction.Deliver,"固定情境已開始有效交付");
            var other=world.State.individuals[2];other.age=0;other.parentId=parent.individual;other.x=.2f;other.y=0;other.satiety=10;
            parent.satiety=47;world.RefreshPerception();var ongoing=ForagingController.Decide(world,parent);
            Require(ongoing.foodAction==FoodAction.Deliver && ongoing.recipient==child.individual,"有效交付維持原對象且四十七可續行");
            parent.satiety=45;world.RefreshPerception();Require(ForagingController.Decide(world,parent).foodAction==FoodAction.Eat,"交付中降至四十五先自救");
            Step(world,true);Require(child.reservedCapacity==0 && parent.careGiven==0 && child.careReceived==0,"中止釋放預留且不直接轉營養");
            var claim=Make();var receiver=claim.State.individuals[0];receiver.satiety=30;Source(claim,.5f,1);claim.RefreshPerception(true);Step(claim);
            var sender=claim.State.individuals[1];sender.x=.4f;sender.y=.1f;sender.facing=180;var gift=Give(claim,sender);
            sender.deliveryInviteTo=receiver.individual;sender.deliveryInviteFruit=gift.fruit;claim.RefreshPerception();
            Require(receiver.foodOperation==FoodAction.Gather && ForagingController.Decide(claim,receiver).foodAction==FoodAction.Gather,
                "有效採集不為新邀請中斷");
            var risk=Make();var young=risk.State.individuals[0];var giver=risk.State.individuals[1];
            young.age=0;young.parentId=giver.individual;young.satiety=30;giver.x=.5f;giver.y=0;giver.facing=180;giver.satiety=70;
            var fading=Give(risk,giver,9);giver.deliveryInviteTo=young.individual;giver.deliveryInviteFruit=fading.fruit;risk.RefreshPerception(true);
            risk.State.time=3.6f;risk.State.step=36;risk.RefreshPerception();
            for(int i=0;i<10;i++) Step(risk,true);
            Require(risk.State.fruitLedger.delivered==1,"已開始有效接收不因每步重套完整三秒估計而中斷");
        }
        static void IsolationAndBirth()
        {
            var a=Make();var b=Make();var one=a.State.individuals[0];var two=b.State.individuals[0];one.satiety=two.satiety=50;
            Give(a,one,20);Give(b,two,39);a.RefreshPerception(true);b.RefreshPerception(true);
            Require(a.ObserveEntities(one).SelectMany(r=>r).SequenceEqual(b.ObserveEntities(two).SelectMany(r=>r)),"粗略觀察不含不同精確期限");
            var left=ForagingController.Decide(a,one);var right=ForagingController.Decide(b,two);
            Require(left.foodAction==right.foodAction && left.fruit==right.fruit && left.movement==right.movement,"同粗略自身果實不讀精確期限決策");
            var danger=Offers(1);var needy=danger.State.individuals[0];Enemy(danger,1);danger.RefreshPerception();
            var escape=ForagingController.Decide(danger,needy);Require(escape.foodAction!=FoodAction.Receive && escape.movement.sqrMagnitude>0,"近身危險不接收");
            var guard=Make();var protector=guard.State.individuals[0];Family(guard,70,30,4);Give(guard,protector);Enemy(guard,4.5f);guard.RefreshPerception(true);
            ForagingController.Decide(guard,protector);Require(protector.decisionReason=="protect_child","既有親代保護仍高於送果");
            var birth=Make();birth.Config.disableReproduction=false;var adult=birth.State.individuals[0];adult.satiety=80;
            Give(birth,adult);Give(birth,adult);Give(birth,adult);birth.RefreshPerception();Step(birth);
            var newborn=birth.State.individuals.Single(i=>i.parentId==adult.individual);
            Require(adult.satiety==40 && newborn.satiety==30 && newborn.ownFruits.Count==0,"繁殖條件代價保留且新生不帶果實");
            Require(birth.State.fruitLedger.Residual(birth.State.fruits.Count)==0,"繁殖與攜帶營養不改果實守恆");
        }
        public static void RunUnsafeChild()
        {
            var world=Make();var parent=world.State.individuals[0];var child=world.State.individuals[1];
            parent.health=CombatRules.CurrentMaxHealth(world.Config,parent)*.35f;Give(world,parent);
            child.parentId=parent.individual;child.age=0;child.satiety=30;child.x=4;child.y=0;Enemy(world,4.5f);
            world.RefreshPerception(true);var action=ForagingController.Decide(world,parent);
            Require(parent.decisionReason!="care_approach" && action.foodAction!=FoodAction.Deliver,
                "無力接戰的親代不為送果走入已知幼體旁威脅");
            Debug.Log("已知送果目標威脅避險驗收通過");
        }
        public static void RunReceiverRisk()
        {
            var world=Make();var receiver=world.State.individuals[0];var sender=world.State.individuals[1];
            receiver.satiety=40;sender.x=.5f;sender.y=0;sender.facing=180;var fruit=Give(world,sender,9);
            sender.deliveryInviteTo=receiver.individual;sender.deliveryInviteFruit=fruit.fruit;world.RefreshPerception(true);
            world.State.step=58;world.State.time=5.8f;world.RefreshPerception();
            Require(receiver.foodInteractions.Single().offeredFruit.EstimateRemaining(world.State.time)<3,"固定情境果實估計為高風險");
            Require(ForagingController.Decide(world,receiver).foodAction==FoodAction.Receive,"危急無替代食物時可接受高風險果實");
            Debug.Log("危急接收高風險果實驗收通過");
        }
        public static void RunSwap()
        {
            var world=Make();var parent=world.State.individuals[0];var child=world.State.individuals[1];
            child.parentId=parent.individual;child.age=0;child.satiety=30;child.x=7;child.y=0;child.facing=180;
            for(int i=0;i<3;i++) Give(world,parent,5);
            Source(world,.5f,1);world.RefreshPerception(true);
            var action=ForagingController.Decide(world,parent);
            Require(action.foodAction==FoodAction.Drop && parent.decisionReason=="care_swap","滿載舊果送不到幼體時才為可行送果換鮮果");
            for(int i=0;i<2;i++) Step(world);
            Require(parent.ownFruits.Count==2 && world.State.fruitLedger.dropped==1,"換果只先騰出一格");
            Require(ForagingController.Decide(world,parent).foodAction==FoodAction.Gather,"騰出容量後採目標鮮果而非撿回舊果");
            for(int i=0;i<10;i++) Step(world);
            Require(parent.ownFruits.Count==3 && world.State.fruitLedger.dropped==1,"有明確用途的換果不囤地循環");
            Debug.Log("親代送果用途換果驗收通過");
        }
        public static void RunSearch()
        {
            var world=Make();var parent=world.State.individuals[0];var child=world.State.individuals[1];
            child.parentId=parent.individual;child.age=0;child.satiety=30;child.x=4;child.y=0;
            world.RefreshPerception(true);parent.facing=180;world.RefreshPerception();
            Require(ForagingController.Decide(world,parent).movement.x>0 && parent.decisionReason=="search_child",
                "親代保留失聯幼體最後位置搜尋");
            parent.x=4;child.x=15;world.RefreshPerception();ForagingController.Decide(world,parent);
            Require(parent.children.Single().searched,"到達失聯位置標示已搜尋");
            Require(ForagingController.Decide(world,parent).movement.x<=0 || parent.decisionReason!="search_child","不反覆追同一落空子女位置");
            var juvenile=Make();var young=juvenile.State.individuals[0];var adult=juvenile.State.individuals[1];
            young.age=0;young.satiety=50;young.parentId=adult.individual;adult.x=1;adult.y=0;Give(juvenile,adult);
            juvenile.RefreshPerception(true);young.facing=180;juvenile.State.time=1;juvenile.RefreshPerception();
            ForagingController.Decide(juvenile,young);Require(young.decisionReason!="approach_parent","幼體不靠五秒互動記憶新增視野外追親代");
            Debug.Log("失聯親子搜尋與未知持果驗收通過");
        }
        static World Offers(int seed)
        {
            var world=Make(seed);var receiver=world.State.individuals[0];receiver.satiety=30;
            for(int i=1;i<=2;i++) { var sender=world.State.individuals[i];sender.x=.5f;sender.y=i==1 ? .2f : -.2f;sender.facing=180;
                var fruit=Give(world,sender);sender.deliveryInviteTo=receiver.individual;sender.deliveryInviteFruit=fruit.fruit; }
            world.RefreshPerception(true);return world;
        }
        public static void RunTies()
        {
            var winners=new HashSet<int>();
            for(int seed=1;seed<=40;seed++) { var world=Offers(seed);var receiver=world.State.individuals[0];
                var action=ForagingController.Decide(world,receiver);winners.Add(action.recipient);
                var twin=Offers(seed);Require(ForagingController.Decide(twin,twin.State.individuals[0]).recipient==action.recipient,
                    "同種子接收競爭可重現"); }
            Require(winners.Count==2,"同條件邀請須公平種子抽選而非固定最小識別");
            var parentCase=Offers(101);var child=parentCase.State.individuals[0];var parent=parentCase.State.individuals[2];
            child.parentId=parent.individual;parent.x=.6f;parent.y=0;parentCase.RefreshPerception();
            Require(ForagingController.Decide(parentCase,child).recipient==parent.individual,"可完成的相近邀請親代優先");
            Debug.Log("接收邀請親代距離與公平競爭驗收通過");
        }
        public static void RunInvitation()
        {
            var world=Make();var child=world.State.individuals[0];var parent=world.State.individuals[1];
            child.satiety=30;child.age=0;child.parentId=parent.individual;
            parent.x=.5f;parent.y=0;parent.facing=180;parent.satiety=70;var fruit=Give(world,parent);
            Source(world,3,1);parent.deliveryInviteTo=child.individual;parent.deliveryInviteFruit=fruit.fruit;
            world.RefreshPerception(true);
            Require(ForagingController.Decide(world,child).foodAction==FoodAction.Receive,"有可接受親代邀請時不先走向遠處新果實");
            for(int i=0;i<10;i++) Step(world,true);
            Require(world.State.fruitLedger.delivered==1 && child.ownFruits.Count==1,"遠處有果時仍自主完成近處親代交付");
            Debug.Log("接收邀請優先於新補貨驗收通過");
        }
        static IndividualState Enemy(World world,float x)
        {
            var enemy=world.AddIndividual("Moss");enemy.x=x;enemy.y=0;enemy.facing=180;return enemy;
        }
        public static void RunEscape()
        {
            var world=Make();var actor=world.State.individuals[0];var oldest=Give(world,actor,5);Give(world,actor);Give(world,actor);
            Enemy(world,5);actor.retreatUntil=2;actor.retreatX=-1;world.RefreshPerception(true);
            var action=ForagingController.Decide(world,actor);
            Require(action.foodAction==FoodAction.Drop && action.fruit==oldest.fruit && action.movement==Vector2.zero,
                "逃生安全空檔先逐顆放較舊果實");
            for(int i=0;i<4;i++) Step(world);
            Require(actor.ownFruits.Count==1 && world.State.fruitLedger.dropped==2,"每顆放下後重判直到剩一顆");
            for(int i=0;i<30;i++) { var next=ForagingController.Decide(world,actor);
                Require(next.foodAction!=FoodAction.Pickup,"逃生不回頭撿回剛放下果實");Step(world); }
            var close=Make();var fleeing=close.State.individuals[0];Give(close,fleeing);Give(close,fleeing);Give(close,fleeing);
            Enemy(close,1);fleeing.retreatUntil=2;fleeing.retreatX=-1;close.RefreshPerception(true);
            var immediate=ForagingController.Decide(close,fleeing);
            Require(immediate.movement.sqrMagnitude>0 && immediate.foodAction!=FoodAction.Drop,"近身威脅先逃不原地放果");
            Debug.Log("逃生減重與避免撿回循環驗收通過");
        }
        static void Source(World world,float x,int count)
        {
            var source=new FoodState { food=++world.State.nextFood,x=x,capacity=4,mature=true,
                cycleInitialized=true,phase=FoodPhase.Waiting,waitUntil=40,bornAt=-40,lastAdvancedAt=0 };
            world.State.foods.Add(source);
            for(int i=0;i<count;i++) { var fruit=Give(world,world.State.individuals[0]);fruit.holder=0;fruit.source=source.food;fruit.x=x; }
            source.amount=count;
        }
        public static void RunGather()
        {
            var world=Make();var actor=world.State.individuals[0];Source(world,.5f,3);world.RefreshPerception(true);
            Require(ForagingController.Decide(world,actor).foodAction==FoodAction.Gather,"安全時明確採集補滿而非自動改成進食");
            for(int i=0;i<30;i++) Step(world);
            Require(actor.ownFruits.Count==3 && actor.satiety==70 && world.State.fruitLedger.eaten==0,"安全七十飽食應裝滿三顆而不多吃");
            Require(ForagingController.Decide(world,actor).foodAction!=FoodAction.Gather && world.State.fruitLedger.dropped==0,"滿載不繼續採集或無目的囤地");
            var ground=Make();var gatherer=ground.State.individuals[0];var loose=Give(ground,gatherer);loose.holder=0;loose.x=.5f;
            ground.RefreshPerception(true);
            Require(ForagingController.Decide(ground,gatherer).foodAction==FoodAction.Pickup,"可見地面果實可明確拾取");
            for(int i=0;i<5;i++) Step(ground);
            Require(gatherer.ownFruits.Count==1,"規則AI經零點五秒拾取");
            Debug.Log("安全補滿與地面拾取驗收通過");
        }
        public static void RunCare()
        {
            var world=Make();var parent=world.State.individuals[0];var child=world.State.individuals[1];
            parent.satiety=50;child.x=.5f;child.y=0;child.facing=180;child.satiety=30;child.age=0;child.parentId=parent.individual;
            Give(world,parent);world.RefreshPerception(true);
            var action=ForagingController.Decide(world,parent);
            Require(action.foodAction==FoodAction.Deliver && action.recipient==child.individual,"親代五十可優先交付自己無果的飢餓幼體");
            for(int i=0;i<12;i++) Step(world,true);
            Require(world.State.fruitLedger.delivered==1 && child.satiety==30,"自主邀請與接收完成只轉果不補營養");
            for(int i=0;i<20;i++) Step(world,true);
            Require(child.satiety==50 && world.State.fruitLedger.eaten==1,"幼體收到果實先吃完整顆");
            Debug.Log("親代送果與自主接收驗收通過");
        }
        public static void RunEating()
        {
            var world=Make();var actor=world.State.individuals[0];actor.satiety=65;Give(world,actor);world.RefreshPerception();
            var action=ForagingController.Decide(world,actor);
            Require(action.foodAction==FoodAction.Eat && action.movement==Vector2.zero,"飽食六十五有果應停下吃完整顆");
            for(int i=0;i<19;i++) Step(world);
            Require(actor.satiety==65,"未吃完不能補部分營養");Step(world);
            Require(actor.satiety==85 && actor.ownFruits.Count==0,"吃完一顆加二十再重判");
            Debug.Log("採食AI首輪驗收通過");
        }
    }
}
