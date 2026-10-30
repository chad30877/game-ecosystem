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
            int partnerId = actor.operationTarget;
            bool bilateral = actor.foodOperation == FoodAction.Deliver || actor.foodOperation == FoodAction.Receive;
            actor.foodOperation = FoodAction.None; actor.operationFruit = actor.operationTarget = 0;
            actor.operationStartedAt = actor.operationProgress = 0;
            actor.reservedCapacity = 0;
            actor.deliveryInviteTo = actor.deliveryInviteFruit = 0;
            actor.careTarget = actor.careReceivedFrom = 0;
            if (bilateral)
            {
                var partner = state.individuals.Find(value => value.individual == partnerId);
                if (partner != null && partner.operationTarget == actor.individual &&
                    (partner.foodOperation == FoodAction.Deliver || partner.foodOperation == FoodAction.Receive)) Cancel(state, partner);
            }
        }

        public static void DropAll(WorldState state, IndividualState actor, float time)
        {
            Cancel(state, actor);
            foreach (var other in state.individuals)
                if (other.deliveryInviteTo == actor.individual) other.deliveryInviteTo = other.deliveryInviteFruit = 0;
            foreach (var fruit in state.fruits.Where(f=>f.holder==actor.individual).ToArray())
            {
                if (fruit.expiresAt <= time)
                {
                    state.fruits.Remove(fruit); state.fruitLedger.rotten++;
                    state.events.Add(new WorldEvent { kind="fruit_rotten", fruit=fruit.fruit });
                    continue;
                }
                fruit.holder=fruit.source=fruit.claimedBy=0; fruit.x=actor.x; fruit.y=actor.y;
                state.fruitLedger.dropped++;
                state.events.Add(new WorldEvent { kind="fruit_dropped", individual=actor.individual, fruit=fruit.fruit,
                    x=actor.x, y=actor.y });
            }
        }

        static bool CanAct(World world, IndividualState actor, WorldAction action) => actor.health > 0 &&
            (world.Config.disableAging || actor.age < world.Config.lifespan) && action.movement.sqrMagnitude == 0 &&
            !action.attack && action.skill == WorldSkill.None && action.skillSlot < 0 && action.feedChild == 0 &&
            !actor.chargeActiveThisStep && !actor.dashActiveThisStep && !actor.windupActiveThisStep &&
            actor.cultivateUntil <= world.State.time;

        static bool Acquirable(World world, IndividualState actor, FruitState fruit, FoodAction operation) => fruit != null &&
            fruit.holder == 0 && (operation == FoodAction.Gather ?
                fruit.source != 0 && world.State.foods.Exists(f=>f.food==fruit.source) :
                operation == FoodAction.Pickup && fruit.source == 0) &&
            Carried(world.State,actor)+actor.reservedCapacity < Capacity(world.Config,actor) &&
            Vector2.Distance(CombatRules.Position(actor),new Vector2(fruit.x,fruit.y)) <= world.Config.forageRadius &&
            world.CanSeePoint(actor,new Vector2(fruit.x,fruit.y));

        static bool OfferLegal(World world, IndividualState sender, IndividualState receiver, FruitState fruit,
            IReadOnlyDictionary<int, WorldAction> actions)
        {
            if (receiver == null || receiver == sender || receiver.species != sender.species || receiver.health <= 0 ||
                !world.Config.disableAging && receiver.age >= world.Config.lifespan || fruit == null ||
                fruit.holder != sender.individual || !CanAct(world, sender, actions[sender.individual])) return false;
            var action = actions[sender.individual];
            return Requested(world.State, sender, action) == FoodAction.Deliver && action.recipient == receiver.individual &&
                (action.fruit == 0 || action.fruit == fruit.fruit) &&
                Vector2.Distance(CombatRules.Position(sender), CombatRules.Position(receiver)) <= world.Config.forageRadius &&
                world.CanSee(sender, receiver);
        }

        static bool DeliveryLegal(World world, IndividualState sender, IndividualState receiver, FruitState fruit,
            IReadOnlyDictionary<int, WorldAction> actions, bool continuing)
        {
            if (sender == null || receiver == null || receiver == sender || sender.species != receiver.species || fruit == null ||
                fruit.holder != sender.individual || !CanAct(world, sender, actions[sender.individual]) ||
                !CanAct(world, receiver, actions[receiver.individual])) return false;
            var send = actions[sender.individual]; var receive = actions[receiver.individual];
            return Requested(world.State, sender, send) == FoodAction.Deliver && send.recipient == receiver.individual &&
                (send.fruit == 0 || send.fruit == fruit.fruit) &&
                Requested(world.State, receiver, receive) == FoodAction.Receive && receive.recipient == sender.individual &&
                (receive.fruit == 0 || receive.fruit == fruit.fruit) &&
                Vector2.Distance(CombatRules.Position(sender), CombatRules.Position(receiver)) <= world.Config.forageRadius &&
                world.CanSee(sender, receiver) && world.CanSee(receiver, sender) &&
                Carried(world.State, receiver) + receiver.reservedCapacity <= Capacity(world.Config, receiver) &&
                Carried(world.State, receiver) < Capacity(world.Config, receiver) &&
                (continuing ? sender.foodOperation == FoodAction.Deliver && sender.operationTarget == receiver.individual &&
                    sender.operationFruit == fruit.fruit && receiver.foodOperation == FoodAction.Receive && receiver.operationTarget == sender.individual &&
                    receiver.operationFruit == fruit.fruit && receiver.reservedCapacity == 1 :
                    receiver.foodOperation == FoodAction.None && receiver.reservedCapacity == 0);
        }

        static void Begin(WorldState state, IndividualState actor, FoodAction operation, FruitState fruit)
        {
            actor.foodOperation=operation; actor.operationFruit=fruit.fruit;
            actor.operationStartedAt=state.time; actor.operationProgress=0;
        }

        public static void Resolve(World world, IReadOnlyDictionary<int,WorldAction> actions, System.Random random, float time)
        {
            var state=world.State;
            foreach (var actor in state.individuals) actor.deliveryInviteTo=actor.deliveryInviteFruit=0;
            foreach (var actor in state.individuals)
                if (actor.health <= 0 || !world.Config.disableAging && actor.age >= world.Config.lifespan) DropAll(state, actor, time);
            foreach (var actor in state.individuals)
            {
                var action=actions[actor.individual]; var requested=Requested(state,actor,action);
                var fruit=state.fruits.Find(f=>f.fruit==actor.operationFruit);
                bool valid=CanAct(world,actor,action) && requested==actor.foodOperation &&
                    (action.fruit==0 || action.fruit==actor.operationFruit) &&
                    ((actor.foodOperation==FoodAction.Gather || actor.foodOperation==FoodAction.Pickup) ? Acquirable(world,actor,fruit,actor.foodOperation) && fruit.claimedBy==actor.individual :
                     (actor.foodOperation==FoodAction.Eat || actor.foodOperation==FoodAction.Drop) ? fruit!=null && fruit.holder==actor.individual :
                     actor.foodOperation==FoodAction.Deliver ? DeliveryLegal(world, actor,
                         state.individuals.Find(i=>i.individual==actor.operationTarget), fruit, actions, true) :
                     actor.foodOperation==FoodAction.Receive && DeliveryLegal(world,
                         state.individuals.Find(i=>i.individual==actor.operationTarget), actor, fruit, actions, true));
                if (!valid) Cancel(state,actor);
            }
            foreach(var actor in state.individuals)
            {
                var action=actions[actor.individual];
                var requested=Requested(state,actor,action);
                if(actor.foodOperation!=FoodAction.None || !CanAct(world,actor,action) ||
                    (requested!=FoodAction.Eat && requested!=FoodAction.Drop)) continue;
                var fruit=state.fruits.Where(f=>f.holder==actor.individual && (action.fruit==0 || f.fruit==action.fruit)).OrderBy(f=>f.fruit).FirstOrDefault();
                if(fruit!=null) Begin(state,actor,requested,fruit);
            }
            var contenders=new Dictionary<int,List<IndividualState>>();
            foreach(var actor in state.individuals.OrderBy(i=>i.individual))
            {
                var action=actions[actor.individual];
                var requested=Requested(state,actor,action);
                if(actor.foodOperation!=FoodAction.None || !CanAct(world,actor,action) ||
                    (requested!=FoodAction.Gather && requested!=FoodAction.Pickup)) continue;
                var fruit=state.fruits.Where(f=>f.claimedBy==0 && (action.fruit==0 || action.fruit==f.fruit) && Acquirable(world,actor,f,requested))
                    .OrderBy(f=>Vector2.Distance(CombatRules.Position(actor),new Vector2(f.x,f.y))).ThenBy(f=>f.fruit).FirstOrDefault();
                if(fruit==null) continue;
                if(!contenders.TryGetValue(fruit.fruit,out var group)) contenders[fruit.fruit]=group=new List<IndividualState>();
                group.Add(actor);
            }
            foreach(var pair in contenders.OrderBy(p=>p.Key))
            {
                var winner=pair.Value[random.Next(pair.Value.Count)];
                var fruit=state.fruits.Find(f=>f.fruit==pair.Key); fruit.claimedBy=winner.individual;
                Begin(state,winner,Requested(state,winner,actions[winner.individual]),fruit);
            }
            foreach (var sender in state.individuals.OrderBy(i=>i.individual))
            {
                var action = actions[sender.individual];
                if (Requested(state, sender, action) != FoodAction.Deliver) continue;
                var receiver = state.individuals.Find(i=>i.individual==action.recipient);
                int requestedFruit = action.fruit != 0 ? action.fruit :
                    sender.foodOperation == FoodAction.Deliver ? sender.operationFruit : 0;
                var fruit = state.fruits.Where(f=>f.holder==sender.individual && (requestedFruit==0 || f.fruit==requestedFruit))
                    .OrderBy(f=>f.fruit).FirstOrDefault();
                if (!OfferLegal(world, sender, receiver, fruit, actions)) continue;
                sender.deliveryInviteTo=receiver.individual; sender.deliveryInviteFruit=fruit.fruit;
                if (sender.foodOperation != FoodAction.None || !DeliveryLegal(world, sender, receiver, fruit, actions, false)) continue;
                Begin(state, sender, FoodAction.Deliver, fruit); sender.operationTarget=receiver.individual;
                Begin(state, receiver, FoodAction.Receive, fruit); receiver.operationTarget=sender.individual;
                receiver.reservedCapacity=1;
                sender.careTarget=receiver.individual; receiver.careReceivedFrom=sender.individual;
                state.events.Add(new WorldEvent { kind="delivery_started", individual=sender.individual, target=receiver.individual, fruit=fruit.fruit });
            }
            foreach(var actor in state.individuals)
            {
                if(actor.foodOperation==FoodAction.None) continue;
                actor.operationProgress=time-actor.operationStartedAt;
                actor.feeding=actor.foodOperation==FoodAction.Eat;
                if (actor.feeding) { actor.fedSourceX=actor.x; actor.fedSourceY=actor.y; }
                float duration=actor.foodOperation==FoodAction.Eat ? 2f : actor.foodOperation==FoodAction.Drop ? .2f : actor.foodOperation==FoodAction.Pickup ? .5f : 1f;
                if(actor.operationProgress+0.00001f<duration || actor.foodOperation==FoodAction.Receive) continue;
                var fruit=state.fruits.Find(f=>f.fruit==actor.operationFruit);
                if(actor.foodOperation==FoodAction.Eat && fruit!=null && fruit.holder==actor.individual)
                {
                    float nutrition=Mathf.Min(20f,100f-actor.satiety); actor.satiety+=nutrition;
                    actor.foodReceived+=nutrition; actor.fedAmount=nutrition; actor.lastAteAt=time;
                    actor.fedSourceX=actor.x;actor.fedSourceY=actor.y;
                    state.fruits.Remove(fruit);state.fruitLedger.eaten++;
                    state.events.Add(new WorldEvent{kind="ate",individual=actor.individual,fruit=fruit.fruit,amount=nutrition});
                }
                else if((actor.foodOperation==FoodAction.Gather || actor.foodOperation==FoodAction.Pickup) && Acquirable(world,actor,fruit,actor.foodOperation) && fruit.claimedBy==actor.individual)
                {
                    int source=fruit.source; fruit.source=0; fruit.holder=actor.individual;
                    fruit.x=actor.x;fruit.y=actor.y;
                    state.events.Add(new WorldEvent{kind=actor.foodOperation==FoodAction.Gather ? "fruit_gathered" : "fruit_picked_up",individual=actor.individual,fruit=fruit.fruit,food=source});
                }
                else if (actor.foodOperation==FoodAction.Drop && fruit!=null && fruit.holder==actor.individual)
                {
                    fruit.holder=fruit.source=fruit.claimedBy=0; fruit.x=actor.x; fruit.y=actor.y;
                    state.fruitLedger.dropped++;
                    state.events.Add(new WorldEvent { kind="fruit_dropped", individual=actor.individual, fruit=fruit.fruit, x=actor.x, y=actor.y });
                }
                else if (actor.foodOperation==FoodAction.Deliver)
                {
                    var receiver=state.individuals.Find(i=>i.individual==actor.operationTarget);
                    if (DeliveryLegal(world, actor, receiver, fruit, actions, true))
                    {
                        fruit.holder=receiver.individual; fruit.x=receiver.x; fruit.y=receiver.y;
                        state.fruitLedger.delivered++;
                        state.events.Add(new WorldEvent { kind="fruit_delivered", individual=actor.individual,
                            target=receiver.individual, fruit=fruit.fruit });
                    }
                }
                Cancel(state,actor);
            }
            foreach(var source in state.foods) source.amount=state.fruits.Count(f=>f.source==source.food);
        }
    }
}
