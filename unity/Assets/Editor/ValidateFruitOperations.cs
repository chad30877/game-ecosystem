using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Ecosystem.Editor
{
    public static class ValidateFruitOperations
    {
        static void Require(bool value, string message) { if (!value) throw new Exception(message); }
        static World Make(int seed = 101)
        {
            var world = new World(new WorldConfig { foraging=true, halfExtent=20, naturalFoodLimit=0,
                stepSeconds=0.1f, episodeSteps=2000, disableAging=true, disableReproduction=true, idleCost=0,
                healthRecoveryFraction=0, staminaRecoveryRate=0, manaRecoveryRate=0, nutritionBudget=0 }, seed);
            world.Reset();
            foreach (var i in world.State.individuals) { i.x=15; i.y=15; i.facing=0; }
            var actor=world.State.individuals[0]; actor.x=actor.y=0; actor.satiety=50;
            world.State.foods.Add(new FoodState { food=1, x=0.5f, y=0, bornAt=0, lastAdvancedAt=0,
                capacity=4, cycleInitialized=true, phase=FoodPhase.Waiting, waitUntil=50 });
            world.State.nextFood=1;
            world.State.fruits.Add(new FruitState { fruit=1,source=1,x=0.5f,y=0,maturedAt=0,expiresAt=40 });
            world.State.nextFruit=1; world.State.fruitLedger.initial=1;
            return world;
        }
        static void Step(World w, int count, WorldAction actor)
        {
            for (int n=0;n<count;n++) {
                var actions=w.State.individuals.ToDictionary(i=>i.individual,i=>new WorldAction(Vector2.zero));
                actions[w.State.individuals[0].individual]=actor; w.Step(actions);
            }
        }
        static FruitState AddFruit(World w, int holder=0, float expiry=40)
        {
            var fruit=new FruitState { fruit=++w.State.nextFruit,source=holder==0?1:0,holder=holder,x=.5f,y=0,expiresAt=expiry };
            w.State.fruits.Add(fruit);w.State.fruitLedger.initial++;return fruit;
        }
        static void Tick(World w, params (int id, WorldAction action)[] commands)
        {
            var actions=w.State.individuals.ToDictionary(i=>i.individual,i=>new WorldAction(Vector2.zero));
            foreach(var command in commands) actions[command.id]=command.action;
            w.Step(actions);
        }
        static WorldAction Gather(int fruit=1) => new WorldAction(Vector2.zero,foodAction:FoodAction.Gather,fruit:fruit);
        static WorldAction Eat(int fruit=1) => new WorldAction(Vector2.zero,foodAction:FoodAction.Eat,fruit:fruit);
        static void Boundaries()
        {
            var w=Make();var actor=w.State.individuals[0];
            w.State.fruits[0].source=0;w.State.fruits[0].holder=actor.individual;
            AddFruit(w,actor.individual);AddFruit(w,actor.individual);var fourth=AddFruit(w);
            Step(w,10,Gather(fourth.fruit));
            Require(fourth.holder==0 && fourth.claimedBy==0,"成體三格滿載不能採集或暫占");
            Step(w,10,Eat());
            Step(w,1,Gather(fourth.fruit));
            Require(fourth.claimedBy==0 && w.State.fruits.Count==4,"正在進食仍占格，切換採集取消進食且不騰空");
            actor.age=0;w.State.fruits.RemoveAll(f=>f.fruit!=1 && f.fruit!=fourth.fruit);w.State.fruitLedger.initial=2;
            Step(w,10,Gather(fourth.fruit));Require(fourth.holder==0,"幼體一格滿載不可採集");
            actor.age=30;Step(w,10,Gather(fourth.fruit));Require(fourth.holder==actor.individual,"成年擴為三格可採集");
            Require(w.State.fruitLedger.Residual(w.State.fruits.Count)==0,"容量與中斷情境守恆");

            w=Make();actor=w.State.individuals[0];Step(w,9,Gather());
            Step(w,1,new WorldAction(Vector2.up));
            Require(w.State.fruits[0].claimedBy==0 && actor.operationProgress==0,"主動移動釋放暫占並清除進度");
            Step(w,9,Gather());Require(w.State.fruits[0].holder==0,"移動中斷後需完整重新計時");
            Step(w,1,Gather());Require(w.State.fruits[0].holder==actor.individual,"重新採滿一秒才能持有");
            Step(w,19,Eat());Step(w,1,new WorldAction(Vector2.zero,attack:true));
            Require(actor.satiety==50 && w.State.fruits.Count==1,"攻擊中斷進食，不給部分營養");
            Step(w,19,Eat());Require(actor.satiety==50,"進食中斷後重新計時");
            Step(w,1,Eat());Require(actor.satiety==70,"重新吃完整顆才補營養");

            w=Make();actor=w.State.individuals[0];Step(w,10,Gather());
            actor.satiety=95;Step(w,20,Eat());Require(actor.satiety==100 && w.State.fruits.Count==0,"營養封頂且不退還剩餘果實");
            w=Make();actor=w.State.individuals[0];Step(w,10,Gather());
            w.State.fruits[0].expiresAt=3;Step(w,20,Eat());
            Require(actor.satiety==50 && w.State.fruitLedger.rotten==1 && w.State.fruitLedger.eaten==0,"腐爛與吃完同時先腐爛");
            w=Make();actor=w.State.individuals[0];w.State.foods[0].bornAt=-119;
            Step(w,10,Gather());Require(w.State.fruits.Count==0 && actor.foodOperation==FoodAction.None,"來源到期先於採集完成");
            w=Make();actor=w.State.individuals[0];Step(w,9,Gather());actor.health=0;
            Step(w,1,Gather());Require(w.State.fruits[0].claimedBy==0 && w.State.fruits[0].holder==0,"死亡不完成採集且釋放暫占");
            w=Make();actor=w.State.individuals[0];Step(w,10,Gather());
            w.Config.disableAging=false;actor.bornAt=-237;
            Step(w,20,Eat());Require(actor.satiety==50 && w.State.fruitLedger.eaten==0,"老死與吃完同時不取得營養");

            w=Make();actor=w.State.individuals[0];Step(w,9,Gather());
            actor.health-=5;Step(w,1,new WorldAction(Vector2.zero,foodAction:FoodAction.Gather,fruit:1,turn:1));
            Require(w.State.fruits[0].holder==actor.individual,"普通受傷與有效掃視不打斷採集");
            Step(w,19,Eat());actor.health-=5;Step(w,1,Eat());Require(actor.satiety==70,"普通受傷不打斷進食");
            w=Make();actor=w.State.individuals[0];actor.poisonStacks=1;actor.poisonOwner=w.State.individuals.Find(i=>i.species=="Moss").individual;
            actor.poisonUntil=20;actor.nextPoisonDamageAt=1;float health=actor.health;
            Step(w,10,Gather());Require(actor.health<health && w.State.fruits[0].holder==actor.individual,"實際中毒跳傷不打斷採集");
            Step(w,20,Eat());Require(actor.satiety==70,"持續中毒仍能吃完整顆");
            w=Make();actor=w.State.individuals[0];Step(w,9,Gather());var replacement=AddFruit(w);
            Step(w,9,Gather(replacement.fruit));
            Require(w.State.fruits[0].claimedBy==0 && replacement.holder==0,"換目標釋放舊暫占並重新計時");
            Step(w,1,Gather(replacement.fruit));Require(replacement.holder==actor.individual,"新目標需完整採集時間");
            w=Make();actor=w.State.individuals[0];Step(w,9,Gather());
            AddFruit(w,actor.individual);AddFruit(w,actor.individual);AddFruit(w,actor.individual);
            Step(w,1,Gather());Require(w.State.fruits[0].claimedBy==0 && w.State.fruits[0].holder==0,"完成時重新檢查容量並釋放暫占");
            w=Make();actor=w.State.individuals[0];w.State.fruits[0].expiresAt=1;
            Step(w,10,Gather());Require(w.State.fruits.Count==0 && w.State.fruitLedger.rotten==1,"腐爛先於採集完成");
            w=Make();actor=w.State.individuals[0];Step(w,9,Gather());actor.x=5;
            Step(w,1,Gather());Require(w.State.fruits[0].claimedBy==0,"位移超距即中斷");
            w=Make();actor=w.State.individuals[0];Step(w,9,Gather());actor.facing=180;
            Step(w,1,Gather());Require(w.State.fruits[0].claimedBy==0,"失去視線須釋放採集暫占");
        }
        static void Competition()
        {
            var winners=new HashSet<int>();
            for(int seed=1;seed<=40;seed++)
            {
                var w=Make(seed);var a=w.State.individuals[0];var b=w.State.individuals[1];b.x=b.y=0;
                Tick(w,(a.individual,Gather()),(b.individual,Gather()));int winner=w.State.fruits[0].claimedBy;winners.Add(winner);
                var copy=Make(seed);var ca=copy.State.individuals[0];var cb=copy.State.individuals[1];cb.x=cb.y=0;
                copy.State.individuals.Reverse();Tick(copy,(ca.individual,Gather()),(cb.individual,Gather()));
                Require(copy.State.fruits[0].claimedBy==winner,"相同種子公平抽選不受個體列表順序影響");
                for(int n=0;n<9;n++) Tick(w,(a.individual,Gather()),(b.individual,Gather()));
                Require(w.State.fruits[0].holder==winner && w.State.fruits[0].source==0,"後到或持續競爭不能重抽有效暫占");
                Require(w.State.fruitLedger.Residual(w.State.fruits.Count)==0,"競爭轉移守恆");
            }
            Require(winners.Count==2,"不同種子雙方都有機會勝出");
            var world=Make();var first=world.State.individuals[0];var second=world.State.individuals[1];second.x=second.y=0;
            AddFruit(world);Tick(world,(first.individual,Gather(1)),(second.individual,Gather(2)));
            Require(world.State.fruits.All(f=>f.claimedBy!=0),"同來源不同顆果實可同時暫占");
        }
        public static void Run()
        {
            var w=Make(); var actor=w.State.individuals[0];
            Step(w,9,new WorldAction(Vector2.zero,forage:true));
            Require(w.State.fruits.Single().holder==0 && actor.satiety==50,"未採滿一秒不可持有或取得營養");
            Step(w,1,new WorldAction(Vector2.zero,forage:true));
            Require(w.State.fruits.Single().holder==actor.individual && actor.satiety==50,"採集一秒後只轉為攜帶");
            Step(w,19,new WorldAction(Vector2.zero,foodAction:FoodAction.Eat,fruit:1));
            Require(actor.satiety==50 && w.State.fruits.Count==1,"未吃滿兩秒不得給部分營養");
            Step(w,1,new WorldAction(Vector2.zero,foodAction:FoodAction.Eat,fruit:1));
            Require(actor.satiety==70 && w.State.fruits.Count==0 && w.State.fruitLedger.eaten==1,"完整吃一顆增加二十且只消耗一次");
            Boundaries();Competition();ValidateFruitCycle.Run();
            Debug.Log("果實操作驗收通過");
        }
    }
}
