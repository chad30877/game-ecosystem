using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Ecosystem.Editor
{
    public static class ValidateFruitTransfer
    {
        static void Require(bool value, string message) { if (!value) throw new Exception(message); }
        static World Make(int seed = 101)
        {
            var w = new World(new WorldConfig { foraging=true, halfExtent=20, naturalFoodLimit=0,
                stepSeconds=.1f, scanEnabled=false, episodeSteps=2000, disableAging=true, disableReproduction=true, idleCost=0,
                healthRecoveryFraction=0, staminaRecoveryRate=0, manaRecoveryRate=0, nutritionBudget=0 }, seed);
            w.Reset();
            foreach (var actor in w.State.individuals) { actor.x=15; actor.y=15; actor.facing=0; }
            var a=w.State.individuals[0]; var b=w.State.individuals[1];
            a.x=a.y=0; a.satiety=70; b.x=.5f; b.y=0; b.facing=180; b.satiety=30;
            w.State.fruits.Add(new FruitState { fruit=1, holder=a.individual, expiresAt=40 });
            w.State.nextFruit=1; w.State.fruitLedger.initial=1;
            return w;
        }
        static void Tick(World w, params (int id, WorldAction action)[] commands)
        {
            var actions=w.State.individuals.ToDictionary(i=>i.individual,i=>new WorldAction(Vector2.zero));
            foreach(var command in commands) actions[command.id]=command.action;
            w.Step(actions);
        }
        static WorldAction Deliver(int target, int fruit=1) => new WorldAction(Vector2.zero,foodAction:FoodAction.Deliver,fruit:fruit,recipient:target);
        static WorldAction Receive(int sender) => new WorldAction(Vector2.zero,foodAction:FoodAction.Receive,recipient:sender);
        static FruitState AddFruit(World w, int holder=0, float expiry=40)
        {
            var fruit=new FruitState { fruit=++w.State.nextFruit, holder=holder, x=.5f, expiresAt=expiry };
            w.State.fruits.Add(fruit); w.State.fruitLedger.initial++; return fruit;
        }
        static void Pair(World w, IndividualState a, IndividualState b, int count, int fruit=1)
        {
            for(int n=0;n<count;n++) Tick(w,(a.individual,Deliver(b.individual,fruit)),(b.individual,Receive(a.individual)));
        }
        static void DeliveryBoundaries()
        {
            var w=Make(); var a=w.State.individuals[0]; var b=w.State.individuals[1];
            Tick(w,(a.individual,Deliver(b.individual)));
            Require(a.foodOperation==FoodAction.None && b.reservedCapacity==0,"單方邀請不啟動交付或占接收容量");
            Require(a.deliveryInviteTo==b.individual && a.deliveryInviteFruit==1,"有效交付動作公開當前邀請供接收者下一步觀察");
            Tick(w,(a.individual,new WorldAction(Vector2.up)));
            Require(a.deliveryInviteTo==0 && a.deliveryInviteFruit==0,"移動即清除交付邀請");
            a.y=0;
            var stranger=w.State.individuals.First(i=>i.species=="Frost"); stranger.x=.5f; stranger.y=0; stranger.facing=180;
            Pair(w,a,stranger,10);
            Require(w.State.fruits[0].holder==a.individual && stranger.reservedCapacity==0,"不同物種不能直接交付");

            w=Make(); a=w.State.individuals[0]; b=w.State.individuals[1];
            AddFruit(w,b.individual); AddFruit(w,b.individual); AddFruit(w,b.individual);
            Pair(w,a,b,10);
            Require(w.State.fruits[0].holder==a.individual && b.reservedCapacity==0,"接收方容量滿不得啟動交付");
            w=Make(); a=w.State.individuals[0]; b=w.State.individuals[1]; b.age=0;
            AddFruit(w,b.individual); Pair(w,a,b,10);
            Require(w.State.fruits[0].holder==a.individual && b.reservedCapacity==0,"幼體一格滿載不可接收");

            w=Make(); a=w.State.individuals[0]; b=w.State.individuals[1]; Pair(w,a,b,9);
            Tick(w,(a.individual,Deliver(b.individual)),(b.individual,new WorldAction(Vector2.right)));
            Require(w.State.fruits[0].holder==a.individual && a.foodOperation==FoodAction.None &&
                b.foodOperation==FoodAction.None && b.reservedCapacity==0,"接收者逃跑雙方即中斷，原果實仍屬送方");
            b.x=.5f; Pair(w,a,b,9); Require(w.State.fruits[0].holder==a.individual,"交付中斷後不可沿用未完進度");
            Pair(w,a,b,1); Require(w.State.fruits[0].holder==b.individual,"重新完整一秒才交付");
            for(int n=0;n<20;n++) Tick(w,(b.individual,new WorldAction(Vector2.zero,foodAction:FoodAction.Eat,fruit:1)));
            Require(b.satiety==50 && w.State.fruitLedger.eaten==1,"接收果實仍須進食兩秒才補營養");

            w=Make(); a=w.State.individuals[0]; b=w.State.individuals[1]; Pair(w,a,b,9);
            Tick(w,(a.individual,Deliver(b.individual)));
            Require(w.State.fruits[0].holder==a.individual && b.reservedCapacity==0,"接收者拒絕釋放預留");
            w=Make(); a=w.State.individuals[0]; b=w.State.individuals[1]; Pair(w,a,b,9); b.x=2;
            Pair(w,a,b,1); Require(w.State.fruits[0].holder==a.individual && b.reservedCapacity==0,"完成時超距重驗，取消交付");
            w=Make(); a=w.State.individuals[0]; b=w.State.individuals[1]; Pair(w,a,b,9);
            AddFruit(w,b.individual); AddFruit(w,b.individual); AddFruit(w,b.individual); Pair(w,a,b,1);
            Require(w.State.fruits[0].holder==a.individual && b.reservedCapacity==0,"完成時容量失效不轉移且釋放預留");
            w=Make(); a=w.State.individuals[0]; b=w.State.individuals[1]; w.State.fruits[0].expiresAt=1;
            Pair(w,a,b,10);
            Require(w.State.fruits.Count==0 && w.State.fruitLedger.rotten==1 && w.State.fruitLedger.delivered==0 &&
                b.reservedCapacity==0,"腐爛與交付完成同時先腐爛，不產生轉移");

            w=Make(); a=w.State.individuals[0]; b=w.State.individuals[1];
            var c=w.AddIndividual("Flame"); c.x=.3f; c.y=.1f; c.facing=0; var chosen=AddFruit(w,c.individual);
            for(int n=0;n<10;n++) Tick(w,(a.individual,Deliver(b.individual)),
                (c.individual,Deliver(b.individual,chosen.fruit)),(b.individual,Receive(c.individual)));
            Require(w.State.fruits[0].holder==a.individual && chosen.holder==b.individual &&
                w.State.fruitLedger.delivered==1,"多邀請僅接收者選定的一筆成功，非世界爭果抽選");

            w=Make(); a=w.State.individuals[0]; b=w.State.individuals[1];
            var ground=AddFruit(w); b.x=.5f; b.facing=0;
            for(int n=0;n<4;n++) Tick(w,(a.individual,Deliver(b.individual)),
                (b.individual,new WorldAction(Vector2.zero,foodAction:FoodAction.Pickup,fruit:ground.fruit)));
            Require(b.foodOperation==FoodAction.Pickup && ground.claimedBy==b.individual && b.reservedCapacity==0,
                "未接受的邀請不打斷有效拾取");

            w=Make(); a=w.State.individuals[0]; b=w.State.individuals[1]; Pair(w,a,b,9);
            a.health-=5; b.health-=5;
            Tick(w,(a.individual,new WorldAction(Vector2.zero,foodAction:FoodAction.Deliver,fruit:1,recipient:b.individual,turn:1)),
                (b.individual,new WorldAction(Vector2.zero,foodAction:FoodAction.Receive,recipient:a.individual,turn:-1)));
            Require(w.State.fruits[0].holder==b.individual,"普通受傷及仍在視線內的掃視不打斷雙方交付");
            w=Make(); a=w.State.individuals[0]; b=w.State.individuals[1];
            AddFruit(w,b.individual); AddFruit(w,b.individual); Pair(w,a,b,1);
            Require(b.reservedCapacity==1 && w.State.fruits.Count(f=>f.holder==b.individual)==2 &&
                w.State.fruits[0].holder==a.individual,"預留消耗最後一格但果實與重量仍在送方");
            Pair(w,a,b,9);
            Require(w.State.fruits.Count(f=>f.holder==b.individual)==3 && b.reservedCapacity==0,"完成時以原預留格接收第三顆");
            foreach(bool senderDies in new[]{true,false})
            {
                w=Make(); a=w.State.individuals[0]; b=w.State.individuals[1]; Pair(w,a,b,9);
                (senderDies?a:b).health=0; Pair(w,a,b,1);
                Require(w.State.fruits[0].holder==(senderDies?0:a.individual) && b.reservedCapacity==0 &&
                    w.State.fruitLedger.delivered==0,"交付任一方死亡先取消，送方死亡才落地");
                Require(w.State.fruitLedger.Residual(w.State.fruits.Count)==0,"死亡中斷交付仍守恆");
            }
            w=Make(); a=w.State.individuals[0]; b=w.State.individuals[1]; Pair(w,a,b,9); a.health=b.health=0;
            Pair(w,a,b,1);
            Require(w.State.fruits[0].holder==0 && w.State.fruitLedger.dropped==1 &&
                w.State.fruitLedger.delivered==0 && w.State.fruitLedger.Residual(w.State.fruits.Count)==0,
                "雙方同時死亡原果實只掉落一次");
            w=Make(); a=w.State.individuals[0]; b=w.State.individuals[1]; Pair(w,a,b,9);
            w.Config.disableAging=false; a.bornAt=-239; Pair(w,a,b,1);
            Require(w.State.fruits[0].holder==0 && w.State.fruitLedger.delivered==0 && b.reservedCapacity==0,
                "老死與交付完成同時不交付，立即掉落");
            w=Make(); a=w.State.individuals[0]; b=w.State.individuals[1]; Pair(w,a,b,1);
            w.RemoveIndividual(a.individual);
            Require(w.State.fruits[0].holder==0 && b.reservedCapacity==0 && b.foodOperation==FoodAction.None,
                "公開移除個體也釋放雙方狀態並掉落果實");
        }
        static WorldAction Pickup(int fruit=1) => new WorldAction(Vector2.zero,foodAction:FoodAction.Pickup,fruit:fruit);
        static WorldAction Drop(int fruit=1) => new WorldAction(Vector2.zero,foodAction:FoodAction.Drop,fruit:fruit);
        static void InvitationBoundaries()
        {
            var w=Make(); var a=w.State.individuals[0]; var b=w.State.individuals[1];
            var second=AddFruit(w,a.individual);
            Pair(w,a,b,1,second.fruit);
            Tick(w,(a.individual,Deliver(b.individual,fruit:0)),(b.individual,Receive(a.individual)));
            Require(a.deliveryInviteFruit==second.fruit && a.operationFruit==second.fruit,
                "有效交付續行的便捷零目標仍呈現正在交付的同一顆果實");
            for(int n=0;n<8;n++) Tick(w,(a.individual,Deliver(b.individual,fruit:0)),(b.individual,Receive(a.individual)));
            Require(second.holder==b.individual && w.State.fruits[0].holder==a.individual,"續行交付不換果");
            w=Make(); a=w.State.individuals[0]; b=w.State.individuals[1];
            Tick(w,(a.individual,Deliver(b.individual))); a.health=0; Tick(w);
            Require(a.deliveryInviteTo==0 && a.deliveryInviteFruit==0,"送方死亡清除邀請");
            w=Make(); a=w.State.individuals[0]; b=w.State.individuals[1];
            Tick(w,(a.individual,Deliver(b.individual))); b.health=0; Tick(w,(a.individual,Deliver(b.individual)));
            Require(a.deliveryInviteTo==0 && a.deliveryInviteFruit==0,"收方死亡清除邀請");
            w=Make(); a=w.State.individuals[0]; b=w.State.individuals[1]; w.State.fruits[0].expiresAt=.2f;
            Tick(w,(a.individual,Deliver(b.individual))); Tick(w,(a.individual,Deliver(b.individual)));
            Require(a.deliveryInviteTo==0 && a.deliveryInviteFruit==0,"果實腐爛清除邀請");
            w=Make(); a=w.State.individuals[0]; b=w.State.individuals[1]; b.x=3;
            Tick(w,(a.individual,Deliver(b.individual)));
            Require(a.deliveryInviteTo==0,"遠距離目標不產生可觀察交付邀請");
            b.x=.5f; a.facing=180; Tick(w,(a.individual,Deliver(b.individual)));
            Require(a.deliveryInviteTo==0,"送方看不見接收者不產生邀請");
        }
        static void GroundBoundaries()
        {
            var w=Make(); var a=w.State.individuals[0];
            Tick(w,(a.individual,Drop())); Tick(w,(a.individual,new WorldAction(Vector2.up)));
            Require(w.State.fruits[0].holder==a.individual && a.operationProgress==0,"放下中斷前果實仍持有且清進度");
            Tick(w,(a.individual,Drop())); Require(w.State.fruits[0].holder==a.individual,"重試放下須重新完整計時");
            Tick(w,(a.individual,Drop()));
            Require(w.State.fruits[0].holder==0 && w.State.fruits[0].y==a.y && w.State.foods.Count==0,
                "放下於當下位置，地面果實不建立來源");
            w=Make(); a=w.State.individuals[0]; w.State.fruits[0].expiresAt=.2f;
            Tick(w,(a.individual,Drop())); Tick(w,(a.individual,Drop()));
            Require(w.State.fruits.Count==0 && w.State.fruitLedger.rotten==1 && w.State.fruitLedger.dropped==0,
                "腐爛與放下完成同時不產生地面果實");

            w=Make(); a=w.State.individuals[0]; w.State.fruits[0].holder=0; w.State.fruits[0].x=.5f;
            for(int n=0;n<4;n++) Tick(w,(a.individual,Pickup()));
            Tick(w,(a.individual,new WorldAction(Vector2.up)));
            Require(w.State.fruits[0].holder==0 && w.State.fruits[0].claimedBy==0 &&
                w.State.fruits[0].x==.5f && w.State.fruits[0].y==0,"拾取中斷釋放單顆暫占並留原地");
            for(int n=0;n<4;n++) Tick(w,(a.individual,Pickup()));
            Require(w.State.fruits[0].holder==0,"拾取中斷後重新等待零點五秒");
            Tick(w,(a.individual,Pickup())); Require(w.State.fruits[0].holder==a.individual,"重新拾滿時間才持有");
            w=Make(); a=w.State.individuals[0]; w.State.fruits[0].holder=0; w.State.fruits[0].x=.5f;
            w.State.fruits[0].expiresAt=.5f;
            for(int n=0;n<5;n++) Tick(w,(a.individual,Pickup()));
            Require(w.State.fruits.Count==0 && w.State.fruitLedger.rotten==1,"腐爛先於拾取完成");
            w=Make(); a=w.State.individuals[0]; w.State.fruits[0].holder=0; w.State.fruits[0].x=.5f;
            AddFruit(w,a.individual); AddFruit(w,a.individual); AddFruit(w,a.individual);
            Tick(w,(a.individual,Pickup())); Require(w.State.fruits[0].claimedBy==0,"滿載不得暫占地面果實");
            w=Make(); a=w.State.individuals[0]; w.State.fruits[0].holder=0; w.State.fruits[0].x=.5f;
            for(int n=0;n<4;n++) Tick(w,(a.individual,Pickup())); a.x=3;
            Tick(w,(a.individual,Pickup())); Require(w.State.fruits[0].claimedBy==0,"拾取完成須重驗距離");

            w=Make(); a=w.State.individuals[0]; AddFruit(w,a.individual); AddFruit(w,a.individual,expiry:.1f);
            for(int n=0;n<19;n++) Tick(w,(a.individual,new WorldAction(Vector2.zero,foodAction:FoodAction.Eat,fruit:1)));
            a.health=0; Tick(w);
            Require(w.State.fruits.Count==2 && w.State.fruits.All(f=>f.holder==0 && f.source==0 && f.claimedBy==0) &&
                w.State.fruitLedger.dropped==2 && w.State.fruitLedger.rotten==1 && w.State.fruitLedger.eaten==0 &&
                w.State.fruitLedger.Residual(w.State.fruits.Count)==0,"進食中死亡只掉落未腐爛的全部持果，不重複或補部分營養");
            w=Make(); a=w.State.individuals[0]; w.State.fruits[0].expiresAt=.1f; a.health=0; Tick(w);
            Require(w.State.fruits.Count==0 && w.State.fruitLedger.dropped==0 && w.State.fruitLedger.rotten==1,
                "死亡同時腐爛先移除，不掉落腐爛果實");

            var winners=new HashSet<int>();
            for(int seed=1;seed<=40;seed++)
            {
                w=Make(seed); a=w.State.individuals[0]; var b=w.State.individuals[1];
                a.x=b.x=0; a.y=b.y=0; a.facing=b.facing=0;
                w.State.fruits[0].holder=0; w.State.fruits[0].x=.5f;
                Tick(w,(a.individual,Pickup()),(b.individual,Pickup()));
                int winner=w.State.fruits[0].claimedBy; winners.Add(winner);
                var twin=Make(seed); var ta=twin.State.individuals[0]; var tb=twin.State.individuals[1];
                ta.x=tb.x=ta.y=tb.y=0; ta.facing=tb.facing=0;
                twin.State.fruits[0].holder=0; twin.State.fruits[0].x=.5f; twin.State.individuals.Reverse();
                Tick(twin,(ta.individual,Pickup()),(tb.individual,Pickup()));
                Require(twin.State.fruits[0].claimedBy==winner,"拾取公平抽選可重現且不依個體列表順序");
                for(int n=1;n<5;n++) Tick(w,(a.individual,Pickup()),(b.individual,Pickup()));
                Require(w.State.fruits[0].holder==winner && w.State.fruitLedger.Residual(w.State.fruits.Count)==0,
                    "後到競爭不重抽有效暫占，拾取後守恆");
            }
            Require(winners.Count==2,"拾取競爭不同種子雙方均有勝出機會");
        }
        public static void Run()
        {
            var w=Make(); var a=w.State.individuals[0]; var b=w.State.individuals[1];
            Tick(w,(a.individual,Deliver(b.individual)),(b.individual,Receive(a.individual)));
            Require(b.reservedCapacity==1 && w.State.fruits[0].holder==a.individual,"交付期間預留接收一格，果實仍屬送方");
            for(int n=1;n<10;n++) Tick(w,(a.individual,Deliver(b.individual)),(b.individual,Receive(a.individual)));
            Require(w.State.fruits[0].holder==b.individual && b.reservedCapacity==0,"雙方配合一秒後才交付原果實");
            Require(a.satiety==70 && b.satiety==30 && w.State.fruitLedger.delivered==1,"交付不直接補營養且只記一次");
            w=Make(); a=w.State.individuals[0];
            Tick(w,(a.individual,new WorldAction(Vector2.zero,foodAction:FoodAction.Drop,fruit:1)));
            Require(w.State.fruits[0].holder==a.individual,"主動放下未滿零點二秒仍持有");
            Tick(w,(a.individual,new WorldAction(Vector2.zero,foodAction:FoodAction.Drop,fruit:1)));
            Require(w.State.fruits[0].holder==0 && w.State.fruits[0].source==0 && w.State.fruitLedger.dropped==1,
                "主動放下零點二秒後原果實才落地");
            Require(w.State.fruits[0].expiresAt==40 && w.State.fruits[0].x==a.x && w.State.fruits[0].y==a.y,
                "放下保留到期時刻並使用當下位置");
            var stranger=w.State.individuals.First(i=>i.species=="Frost"); stranger.x=.5f; stranger.y=0; stranger.facing=180;
            for(int n=0;n<4;n++) Tick(w,(stranger.individual,new WorldAction(Vector2.zero,foodAction:FoodAction.Pickup,fruit:1)));
            Require(w.State.fruits[0].holder==0 && w.State.fruits[0].claimedBy==stranger.individual,
                "地面果實拾取未滿零點五秒仍留原地暫占");
            Tick(w,(stranger.individual,new WorldAction(Vector2.zero,foodAction:FoodAction.Pickup,fruit:1)));
            Require(w.State.fruits[0].holder==stranger.individual && w.State.fruits[0].expiresAt==40,
                "異種花零點五秒可拾取原地果實且不刷新期限");
            w=Make(); a=w.State.individuals[0]; a.x=2; a.y=3; a.health=0;
            Tick(w);
            Require(w.State.fruits[0].holder==0 && w.State.fruits[0].x==2 && w.State.fruits[0].y==3 &&
                w.State.fruitLedger.dropped==1 && w.State.fruits[0].expiresAt==40,
                "死亡立即掉落原果實於死亡位置，不刷新保存");
            DeliveryBoundaries();
            GroundBoundaries();
            InvitationBoundaries();
            ValidateFruitOperations.Run();
            ValidateSourcePlacement.Run();
            Debug.Log("果實交付驗收通過");
        }
    }
}
