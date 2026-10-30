using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Ecosystem
{
    public enum FoodAction { None, Gather, Eat, Pickup, Deliver, Receive, Drop }

    public static class FoodOperations
    {
        public static int Carried(WorldState state, IndividualState actor) => state.fruits.Count(f => f.holder == actor.individual);
        public static int Capacity(WorldConfig config, IndividualState actor) => actor.age >= config.maturityAge ? 3 : 1;
        public static FoodAction Requested(WorldState state, IndividualState actor, WorldAction action) =>
            action.foodAction != FoodAction.None ? action.foodAction : action.forage ?
                (Carried(state, actor) > 0 ? FoodAction.Eat : FoodAction.Gather) : FoodAction.None;

        public static void Cancel(WorldState state, IndividualState actor)
        {
            foreach (var fruit in state.fruits)
                if (fruit.claimedBy == actor.individual) fruit.claimedBy = 0;
            actor.foodOperation = FoodAction.None; actor.operationFruit = actor.operationTarget = 0;
            actor.operationStartedAt = actor.operationProgress = 0;
        }

        static bool CanAct(World world, IndividualState actor, WorldAction action) => actor.health > 0 &&
            (world.Config.disableAging || actor.age < world.Config.lifespan) && action.movement.sqrMagnitude == 0 &&
            !action.attack && action.skill == WorldSkill.None && action.skillSlot < 0 && action.feedChild == 0 &&
            !actor.chargeActiveThisStep && !actor.dashActiveThisStep && !actor.windupActiveThisStep &&
            actor.cultivateUntil <= world.State.time;

        static bool Gatherable(World world, IndividualState actor, FruitState fruit) => fruit != null &&
            fruit.source != 0 && fruit.holder == 0 && world.State.foods.Exists(f=>f.food==fruit.source) &&
            Carried(world.State,actor)+actor.reservedCapacity < Capacity(world.Config,actor) &&
            Vector2.Distance(CombatRules.Position(actor),new Vector2(fruit.x,fruit.y)) <= world.Config.forageRadius &&
            world.CanSeePoint(actor,new Vector2(fruit.x,fruit.y));

        static void Begin(WorldState state, IndividualState actor, FoodAction operation, FruitState fruit)
        {
            actor.foodOperation=operation; actor.operationFruit=fruit.fruit;
            actor.operationStartedAt=state.time; actor.operationProgress=0;
        }

        public static void Resolve(World world, IReadOnlyDictionary<int,WorldAction> actions, System.Random random, float time)
        {
            var state=world.State;
            foreach (var actor in state.individuals)
            {
                var action=actions[actor.individual]; var requested=Requested(state,actor,action);
                var fruit=state.fruits.Find(f=>f.fruit==actor.operationFruit);
                bool valid=CanAct(world,actor,action) && requested==actor.foodOperation &&
                    (action.fruit==0 || action.fruit==actor.operationFruit) &&
                    (actor.foodOperation==FoodAction.Gather ? Gatherable(world,actor,fruit) && fruit.claimedBy==actor.individual :
                     actor.foodOperation==FoodAction.Eat && fruit!=null && fruit.holder==actor.individual);
                if (!valid) Cancel(state,actor);
            }
            foreach(var actor in state.individuals)
            {
                var action=actions[actor.individual];
                if(actor.foodOperation!=FoodAction.None || !CanAct(world,actor,action) || Requested(state,actor,action)!=FoodAction.Eat) continue;
                var fruit=state.fruits.Where(f=>f.holder==actor.individual && (action.fruit==0 || f.fruit==action.fruit)).OrderBy(f=>f.fruit).FirstOrDefault();
                if(fruit!=null) Begin(state,actor,FoodAction.Eat,fruit);
            }
            var contenders=new Dictionary<int,List<IndividualState>>();
            foreach(var actor in state.individuals.OrderBy(i=>i.individual))
            {
                var action=actions[actor.individual];
                if(actor.foodOperation!=FoodAction.None || !CanAct(world,actor,action) || Requested(state,actor,action)!=FoodAction.Gather) continue;
                var fruit=state.fruits.Where(f=>f.claimedBy==0 && (action.fruit==0 || action.fruit==f.fruit) && Gatherable(world,actor,f))
                    .OrderBy(f=>Vector2.Distance(CombatRules.Position(actor),new Vector2(f.x,f.y))).ThenBy(f=>f.fruit).FirstOrDefault();
                if(fruit==null) continue;
                if(!contenders.TryGetValue(fruit.fruit,out var group)) contenders[fruit.fruit]=group=new List<IndividualState>();
                group.Add(actor);
            }
            foreach(var pair in contenders.OrderBy(p=>p.Key))
            {
                var winner=pair.Value[random.Next(pair.Value.Count)];
                var fruit=state.fruits.Find(f=>f.fruit==pair.Key); fruit.claimedBy=winner.individual;
                Begin(state,winner,FoodAction.Gather,fruit);
            }
            foreach(var actor in state.individuals)
            {
                if(actor.foodOperation==FoodAction.None) continue;
                actor.operationProgress=time-actor.operationStartedAt;
                actor.feeding=actor.foodOperation==FoodAction.Eat;
                if (actor.feeding) { actor.fedSourceX=actor.x; actor.fedSourceY=actor.y; }
                float duration=actor.foodOperation==FoodAction.Eat ? 2f : 1f;
                if(actor.operationProgress+0.00001f<duration) continue;
                var fruit=state.fruits.Find(f=>f.fruit==actor.operationFruit);
                if(actor.foodOperation==FoodAction.Eat && fruit!=null && fruit.holder==actor.individual)
                {
                    float nutrition=Mathf.Min(20f,100f-actor.satiety); actor.satiety+=nutrition;
                    actor.foodReceived+=nutrition; actor.fedAmount=nutrition; actor.lastAteAt=time;
                    actor.fedSourceX=actor.x;actor.fedSourceY=actor.y;
                    state.fruits.Remove(fruit);state.fruitLedger.eaten++;
                    state.events.Add(new WorldEvent{kind="ate",individual=actor.individual,fruit=fruit.fruit,amount=nutrition});
                }
                else if(actor.foodOperation==FoodAction.Gather && Gatherable(world,actor,fruit) && fruit.claimedBy==actor.individual)
                {
                    int source=fruit.source; fruit.source=0; fruit.holder=actor.individual;
                    fruit.x=actor.x;fruit.y=actor.y;
                    state.events.Add(new WorldEvent{kind="fruit_gathered",individual=actor.individual,fruit=fruit.fruit,food=source});
                }
                Cancel(state,actor);
            }
            foreach(var source in state.foods) source.amount=state.fruits.Count(f=>f.source==source.food);
        }
    }
}
