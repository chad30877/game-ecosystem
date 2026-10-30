using System.Linq;
using UnityEngine;
namespace Ecosystem
{
    [System.Serializable]
    public sealed class FoodDiscardMemory
    {
        public int fruit;
        public float droppedAt;
    }
    public static class FoodCareController
    {
        public static float WalkSeconds(World world,IndividualState actor,float distance,int carried=-1)
        {
            var c=world.Config.combat;
            float speed=actor.species=="Flame" ? c.flameSpeed : actor.species=="Frost" ? c.frostSpeed : c.mossSpeed;
            int count=carried<0 ? actor.ownFruits.Count : carried;
            speed*=count>=3 ? .8f : count==2 ? .9f : 1;
            if(world.State.time<actor.slowUntil) speed*=c.slowMultiplier;
            return Mathf.Max(0,actor.rootUntil-world.State.time)+Mathf.Max(0,distance)/speed;
        }
        public static WorldAction? Choose(World world,IndividualState actor)
        {
            actor.foodDiscarded.RemoveAll(f=>world.State.time-f.droppedAt>=40);
            if(!Safe(world,actor)) return null;
            if(actor.foodUnloading)
            {
                var drop=Unload(world,actor);if(drop.HasValue) return drop;
            }
            // 自救先於新照顧；已開始進食保持完整一顆，合格照顧再先於一般自用。
            if(actor.foodOperation==FoodAction.Eat) return Eat(world,actor,false);
            if(actor.satiety<=45)
                return Eat(world,actor,false) ?? ContinueAcquisition(world,actor) ?? Receive(world,actor) ?? Acquire(world,actor,false) ??
                    Eat(world,actor,true) ?? Receive(world,actor,true) ?? Acquire(world,actor,true) ?? ApproachParent(world,actor);
            var care=Care(world,actor);if(care.HasValue) return care;
            var eat=Eat(world,actor,false);if(eat.HasValue) return eat;
            var swap=SwapForCare(world,actor);if(swap.HasValue) return swap;
            var continued=ContinueAcquisition(world,actor);if(continued.HasValue) return continued;
            return Receive(world,actor) ?? Acquire(world,actor,false) ?? ApproachParent(world,actor) ?? SearchChild(world,actor) ?? Revisit(world,actor);
        }
        static WorldAction? Eat(World world,IndividualState actor,bool allowRisk)
        {
            if(actor.ownFruits.Count==0 || actor.satiety>65 && actor.foodOperation!=FoodAction.Eat) return null;
            var fruit=actor.foodOperation==FoodAction.Eat ? actor.ownFruits.Find(f=>f.fruit==actor.operationFruit) : null;
            fruit ??= actor.ownFruits.Where(f=>f.EstimateRemaining(world.State.time)>=2)
                .OrderBy(f=>f.EstimateRemaining(world.State.time)).ThenBy(f=>f.fruit).FirstOrDefault();
            if(fruit==null && allowRisk && actor.satiety<=45) fruit=actor.ownFruits.OrderByDescending(f=>f.EstimateRemaining(world.State.time)).FirstOrDefault();
            return fruit==null ? null : Act(actor,"eat_fruit",FoodAction.Eat,fruit.fruit);
        }
        public static EntityObservation CloseThreat(World world,IndividualState actor) => actor.perception.Where(e=>e.visible &&
            e.species!=actor.species && Vector2.Distance(CombatRules.Position(actor),new Vector2(e.x,e.y))<=world.Config.scanThreatRadius)
            .OrderBy(e=>Vector2.Distance(CombatRules.Position(actor),new Vector2(e.x,e.y))).FirstOrDefault();
        public static WorldAction Escape(World world,IndividualState actor,Vector2 away,string reason="retreat")
        {
            actor.foodAvoidRefillUntil=world.State.time+5;
            actor.foodUnloading|=actor.ownFruits.Count>1;
            var dropped=Safe(world,actor) ? Unload(world,actor) : null;
            actor.decisionReason=reason;
            return dropped ?? new WorldAction(away,turn:Turn(actor,away),run:actor.runPreferred);
        }
        static WorldAction? Unload(World world,IndividualState actor)
        {
            if(actor.ownFruits.Count<=1) { actor.foodUnloading=false;return null; }
            if(actor.smokeKnowledge.Any(s=>s.harmful && Vector2.Distance(CombatRules.Position(actor),new Vector2(s.x,s.y))<=s.radius)) return null;
            var fruit=actor.foodOperation==FoodAction.Drop ? actor.ownFruits.Find(f=>f.fruit==actor.operationFruit) : null;
            fruit ??= actor.ownFruits.OrderBy(f=>f.EstimateRemaining(world.State.time)).ThenBy(f=>f.fruit).First();
            if(!actor.foodDiscarded.Any(f=>f.fruit==fruit.fruit)) actor.foodDiscarded.Add(new FoodDiscardMemory { fruit=fruit.fruit,droppedAt=world.State.time });
            if(actor.foodDiscarded.Count>16) actor.foodDiscarded.RemoveAt(0);
            return Act(actor,"escape_drop",FoodAction.Drop,fruit.fruit);
        }
        static bool Safe(World world,IndividualState actor) => !actor.perception.Any(e=>e.visible && e.species!=actor.species &&
            Vector2.Distance(CombatRules.Position(actor),new Vector2(e.x,e.y))<=world.Config.scanThreatRadius);
        static WorldAction? Care(World world,IndividualState actor)
        {
            if(world.Config.disableCare || actor.satiety<=45 || !Safe(world,actor)) return null;
            actor.foodCareChildren.RemoveAll(id=>!actor.children.Any(c=>c.child==id && !c.confirmedDead &&
                c.predictedAge<world.Config.maturityAge && c.predictedSatiety<60));
            foreach(var c in actor.children.Where(c=>!c.confirmedDead && c.predictedAge<world.Config.maturityAge &&
                c.predictedSatiety<=40 && c.food!=null && c.food.known && c.food.carriedCount==0))
                if(!actor.foodCareChildren.Contains(c.child)) actor.foodCareChildren.Add(c.child);
            var children=actor.children.Where(c=>actor.foodCareChildren.Contains(c.child) && c.food!=null && c.food.known &&
                c.food.carriedCount==0 && (c.visible || !c.searched) && SafePoint(world,actor,new Vector2(c.x,c.y))).ToList();
            if(actor.foodOperation==FoodAction.Deliver)
            {
                var ongoing=children.Find(c=>c.child==actor.operationTarget);
                var held=actor.ownFruits.Find(f=>f.fruit==actor.operationFruit);
                if(ongoing!=null && held!=null) return Act(actor,"care_deliver",FoodAction.Deliver,held.fruit,ongoing.child,
                    new Vector2(ongoing.x,ongoing.y)-CombatRules.Position(actor));
            }
            if(actor.satiety<50) return null;
            var viable=children.Where(c=>actor.ownFruits.Any(f=>f.EstimateRemaining(world.State.time)>=
                WalkSeconds(world,actor,Vector2.Distance(CombatRules.Position(actor),new Vector2(c.x,c.y))-world.Config.forageRadius)+3)).ToList();
            if(viable.Count==0) return null;
            float lowest=viable.Min(c=>c.predictedSatiety);
            var child=viable.Where(c=>c.predictedSatiety<=lowest+10).OrderBy(c=>
                Vector2.Distance(CombatRules.Position(actor),new Vector2(c.x,c.y))).ThenBy(c=>c.child).First();
            var delta=new Vector2(child.x,child.y)-CombatRules.Position(actor);
            var fruit=actor.ownFruits.Where(f=>f.EstimateRemaining(world.State.time)>=WalkSeconds(world,actor,delta.magnitude-world.Config.forageRadius)+3)
                .OrderByDescending(f=>f.EstimateRemaining(world.State.time)).ThenBy(f=>f.fruit).First();
            if(delta.magnitude>world.Config.forageRadius*.9f)
            {
                actor.decisionReason="care_approach";
                return new WorldAction(delta.normalized,turn:Turn(actor,delta));
            }
            return Act(actor,"care_deliver",FoodAction.Deliver,fruit.fruit,child.child,delta);
        }
        static WorldAction? SwapForCare(World world,IndividualState actor)
        {
            if(world.Config.disableCare || actor.satiety<50 || world.State.time<actor.foodAvoidRefillUntil ||
                actor.ownFruits.Count<FoodOperations.Capacity(world.Config,actor)) return null;
            var position=CombatRules.Position(actor);float now=world.State.time;
            foreach(var child in actor.children.Where(c=>actor.foodCareChildren.Contains(c.child) && c.food!=null && c.food.known &&
                c.food.carriedCount==0 && !c.confirmedDead && c.predictedAge<world.Config.maturityAge && c.predictedSatiety<60 &&
                (c.visible || !c.searched) && SafePoint(world,actor,new Vector2(c.x,c.y))))
            {
                float delivery=WalkSeconds(world,actor,Vector2.Distance(position,new Vector2(child.x,child.y))-world.Config.forageRadius)+3;
                if(actor.ownFruits.Any(f=>f.EstimateRemaining(now)>=delivery)) continue;
                bool useful=actor.visibleFoods.SelectMany(source=>source.fruits).Concat(actor.groundFruitMemory.Where(f=>f.visible))
                    .Any(f=>(!f.claimed || f.claimedBy==actor.individual) && !actor.foodDiscarded.Any(d=>d.fruit==f.fruit) &&
                        SafePoint(world,actor,new Vector2(f.x,f.y)) && f.EstimateRemaining(now)>=.2f+
                        WalkSeconds(world,actor,Vector2.Distance(position,new Vector2(f.x,f.y))-world.Config.forageRadius,actor.ownFruits.Count-1)+
                        (f.source==0 ? .5f : 1)+WalkSeconds(world,actor,Vector2.Distance(new Vector2(f.x,f.y),new Vector2(child.x,child.y))-
                            world.Config.forageRadius,actor.ownFruits.Count)+3);
                if(!useful) continue;
                var drop=Unload(world,actor);actor.decisionReason="care_swap";return drop;
            }
            return null;
        }
        static WorldAction? ContinueAcquisition(World world,IndividualState actor)
        {
            if(actor.foodOperation!=FoodAction.Gather && actor.foodOperation!=FoodAction.Pickup) return null;
            if(actor.ownFruits.Count+actor.reservedCapacity>=FoodOperations.Capacity(world.Config,actor)) return null;
            var fruit=actor.foodMemory.SelectMany(s=>s.fruits).Concat(actor.groundFruitMemory).FirstOrDefault(f=>
                f.visible && f.fruit==actor.operationFruit && f.claimedBy==actor.individual &&
                Vector2.Distance(CombatRules.Position(actor),new Vector2(f.x,f.y))<=world.Config.forageRadius);
            return fruit==null ? null : Act(actor,actor.foodOperation==FoodAction.Gather ? "gather_fruit" : "pickup_fruit",
                actor.foodOperation,fruit.fruit,direction:new Vector2(fruit.x,fruit.y)-CombatRules.Position(actor));
        }
        sealed class Opportunity
        {
            public FruitObservation fruit;
            public FamilyMemory child;
            public float cost,required;
        }
        static bool SafePoint(World world,IndividualState actor,Vector2 point) => !actor.perception.Any(e=>e.visible &&
            e.species!=actor.species && Vector2.Distance(point,new Vector2(e.x,e.y))<=world.Config.scanThreatRadius);
        static WorldAction? Acquire(World world,IndividualState actor,bool allowRisk)
        {
            if(world.State.time<actor.foodAvoidRefillUntil || actor.ownFruits.Count+actor.reservedCapacity>=FoodOperations.Capacity(world.Config,actor)) return null;
            var position=CombatRules.Position(actor);float now=world.State.time;
            var fruits=actor.foodMemory.SelectMany(source=>source.fruits).Concat(actor.groundFruitMemory)
                .Where(f=>(!f.visible || !f.claimed || f.claimedBy==actor.individual) && !actor.foodDiscarded.Any(d=>d.fruit==f.fruit))
                .GroupBy(f=>f.fruit).Select(g=>g.First()).Where(f=>SafePoint(world,actor,new Vector2(f.x,f.y))).ToList();
            var choices=fruits.Select(f=>new Opportunity { fruit=f,
                cost=WalkSeconds(world,actor,Vector2.Distance(position,new Vector2(f.x,f.y))-world.Config.forageRadius)+(f.source==0 ? .5f : 1f) })
                .ToList();
            var children=actor.satiety>=50 && !world.Config.disableCare ? actor.children.Where(c=>actor.foodCareChildren.Contains(c.child) &&
                !c.confirmedDead && c.predictedAge<world.Config.maturityAge && c.predictedSatiety<60 && c.food!=null && c.food.known &&
                c.food.carriedCount==0 && (c.visible || !c.searched) && SafePoint(world,actor,new Vector2(c.x,c.y))).ToList() :
                new System.Collections.Generic.List<FamilyMemory>();
            var delivery=choices.SelectMany(choice=>children.Select(child=>new Opportunity { fruit=choice.fruit,child=child,cost=choice.cost,
                required=choice.cost+WalkSeconds(world,actor,Vector2.Distance(new Vector2(choice.fruit.x,choice.fruit.y),new Vector2(child.x,child.y))-
                    world.Config.forageRadius,actor.ownFruits.Count+1)+3 }))
                .Where(choice=>choice.fruit.EstimateRemaining(now)>=choice.required).ToList();
            Opportunity selected=null;
            if(delivery.Count>0)
            {
                float lowest=delivery.Min(c=>c.child.predictedSatiety);
                selected=delivery.Where(c=>c.child.predictedSatiety<=lowest+10).OrderBy(c=>
                    Vector2.Distance(position,new Vector2(c.child.x,c.child.y))).ThenByDescending(c=>c.fruit.EstimateRemaining(now))
                    .ThenBy(c=>c.cost).ThenBy(c=>c.fruit.fruit).First();
            }
            if(selected==null)
            {
                var viable=choices.Where(choice=>choice.fruit.EstimateRemaining(now)>=choice.cost+2).ToList();
                selected=viable.Find(c=>c.fruit.fruit==actor.operationFruit && c.fruit.visible && c.fruit.claimedBy==actor.individual);
                selected ??= viable.OrderBy(c=>c.cost).ThenBy(c=>c.fruit.EstimateRemaining(now)).ThenBy(c=>c.fruit.fruit).FirstOrDefault();
            }
            if(selected==null && allowRisk) selected=choices.OrderByDescending(c=>c.fruit.EstimateRemaining(now)-c.cost).ThenBy(c=>c.fruit.fruit).FirstOrDefault();
            if(selected==null) return null;
            var delta=new Vector2(selected.fruit.x,selected.fruit.y)-position;
            if(!selected.fruit.visible || delta.magnitude>world.Config.forageRadius*.9f)
            {
                actor.decisionReason=selected.fruit.visible ? "food_approach" : "food_revisit";
                return new WorldAction(delta.magnitude>world.Config.forageRadius*.9f ? delta.normalized : Vector2.zero,turn:Turn(actor,delta),
                    run:actor.runPreferred && actor.satiety<=45 && delta.magnitude>world.Config.forageRadius*2);
            }
            return Act(actor,selected.fruit.source==0 ? "pickup_fruit" : "gather_fruit",
                selected.fruit.source==0 ? FoodAction.Pickup : FoodAction.Gather,selected.fruit.fruit, direction:delta);
        }
        static WorldAction? SearchChild(World world,IndividualState actor)
        {
            if(world.Config.disableCare || actor.satiety<=45) return null;
            var child=actor.children.Where(c=>!c.visible && !c.confirmedDead && !c.searched &&
                c.predictedAge<world.Config.maturityAge && (c.predictedSatiety<world.Config.familySearchSatiety || c.threatened))
                .OrderBy(c=>Vector2.Distance(CombatRules.Position(actor),new Vector2(c.x,c.y))).ThenBy(c=>c.child).FirstOrDefault();
            if(child==null) return null;
            var delta=new Vector2(child.x,child.y)-CombatRules.Position(actor);
            if(delta.magnitude<=world.Config.forageRadius) { child.searched=true;return null; }
            actor.decisionReason="search_child";
            return new WorldAction(delta.normalized,turn:Turn(actor,delta));
        }
        static WorldAction? Revisit(World world,IndividualState actor)
        {
            if(world.State.time<actor.foodAvoidRefillUntil) return null;
            var source=actor.foodMemory.Where(f=>!f.visible && f.stale).OrderBy(f=>
                Vector2.Distance(CombatRules.Position(actor),new Vector2(f.x,f.y))).ThenBy(f=>f.food).FirstOrDefault();
            if(source==null) return null;
            var delta=new Vector2(source.x,source.y)-CombatRules.Position(actor);actor.decisionReason="source_revisit";
            return new WorldAction(delta.magnitude>world.Config.forageRadius*.9f ? delta.normalized : Vector2.zero,turn:Turn(actor,delta));
        }
        static WorldAction? Receive(World world,IndividualState actor,bool allowRisk=false)
        {
            if(actor.ownFruits.Count!=0 || actor.satiety>65 || !Safe(world,actor) ||
                actor.foodOperation!=FoodAction.None && actor.foodOperation!=FoodAction.Receive) return null;
            var offers=actor.foodInteractions.Where(k=>k.visible && k.known && k.deliveryInviteTo==actor.individual &&
                k.offeredFruit!=null && (allowRisk || actor.foodOperation==FoodAction.Receive && actor.operationTarget==k.individual ||
                    k.offeredFruit.EstimateRemaining(world.State.time)>=3) &&
                Vector2.Distance(CombatRules.Position(actor),new Vector2(k.x,k.y))<=world.Config.forageRadius &&
                actor.perception.Any(e=>e.visible && e.individual==k.individual && e.species==actor.species)).ToList();
            var selected=actor.foodOperation==FoodAction.Receive ? offers.Find(o=>o.individual==actor.operationTarget) : null;
            if(selected==null && offers.Count>0)
            {
                bool hasParent=offers.Any(o=>o.individual==actor.parentId);
                var preferred=offers.Where(o=>!hasParent || o.individual==actor.parentId).ToList();
                float nearest=preferred.Min(o=>Vector2.Distance(CombatRules.Position(actor),new Vector2(o.x,o.y)));
                var tied=preferred.Where(o=>Mathf.Abs(Vector2.Distance(CombatRules.Position(actor),new Vector2(o.x,o.y))-nearest)<.00001f)
                    .OrderBy(o=>o.individual).ToArray();
                // 接收者自行以種子抽同條件邀請，不消耗世界生產與爭果亂數。
                uint choice=unchecked((uint)world.Seed ^ (uint)actor.individual*0x9e3779b9u ^ (uint)world.State.step*0x85ebca6bu);
                choice^=choice>>16;choice*=0x7feb352du;choice^=choice>>15;choice*=0x846ca68bu;choice^=choice>>16;
                selected=tied[choice%(uint)tied.Length];
            }
            return selected==null ? null : Act(actor,"receive_fruit",FoodAction.Receive,selected.offeredFruit.fruit,selected.individual,
                new Vector2(selected.x,selected.y)-CombatRules.Position(actor));
        }
        static WorldAction? ApproachParent(World world,IndividualState actor)
        {
            if(world.Config.disableCare || actor.age>=world.Config.maturityAge || actor.ownFruits.Count!=0 || actor.satiety>65 || !Safe(world,actor)) return null;
            var parent=actor.perception.Find(p=>p.visible && p.individual==actor.parentId && p.food!=null && p.food.carriedCount>0);
            if(parent==null) return null;
            var delta=new Vector2(parent.x,parent.y)-CombatRules.Position(actor);actor.decisionReason="approach_parent";
            return new WorldAction(delta.magnitude>world.Config.forageRadius*.8f ? delta.normalized : Vector2.zero,turn:Turn(actor,delta));
        }
        static WorldAction Act(IndividualState actor,string reason,FoodAction operation,int fruit,int recipient=0,Vector2 direction=default)
        {
            actor.decisionReason=reason;
            return new WorldAction(Vector2.zero,foodAction:operation,fruit:fruit,recipient:recipient,turn:Turn(actor,direction));
        }
        static int Turn(IndividualState actor,Vector2 direction)
        {
            if(direction.sqrMagnitude==0) return 0;
            float delta=Mathf.DeltaAngle(actor.facing,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg);
            return Mathf.Abs(delta)<4 ? 0 : delta>0 ? 1 : -1;
        }
    }
}
