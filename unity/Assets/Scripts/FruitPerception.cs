using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Ecosystem
{
    public enum FruitFreshness { Unknown=-1, Fresh, NearRot }
    [Serializable]
    public sealed class FruitObservation
    {
        public int fruit, source, holder;
        public float x, y, firstSeenAt, freshnessSince, lastSeenAt;
        public bool visible, claimed;
        public int claimedBy;
        public FruitFreshness freshness;
        // 首次看見提供寬區間；重見只收窄區間，不重新給予四十秒。
        public float EstimateRemaining(float time)
        {
            float upper = firstSeenAt + 40f;
            if (freshness == FruitFreshness.NearRot) upper = Math.Min(upper, freshnessSince + 10f);
            float lower = freshness == FruitFreshness.Fresh ? Math.Max(0, 10f - (time-lastSeenAt)) : 0;
            float remainingUpper=Math.Max(0,upper-time);
            return (remainingUpper + Math.Min(lower,remainingUpper)) * .5f;
        }
    }
    // 只保留曾見果實的新鮮度區間，不保留位置或持有者，不作追尋線索。
    [Serializable]
    public sealed class FruitFreshnessMemory
    {
        public int fruit;
        public float firstSeenAt, freshnessSince, lastSeenAt;
        public FruitFreshness freshness;
        public float UpperBoundAt => freshness==FruitFreshness.NearRot ?
            Math.Min(firstSeenAt+40f,freshnessSince+10f) : firstSeenAt+40f;
    }
    [Serializable]
    public sealed class FoodInteractionObservation
    {
        public int individual, carriedCount=-1, operationFruit, operationTarget;
        public int deliveryInviteTo, deliveryInviteFruit, cultivatingFood;
        public float x,y,lastSeenAt,cultivationStrength,cultivationObservedSince,sourceX,sourceY;
        public bool visible, known, hasSource;
        public FoodAction operation;
        public FruitObservation offeredFruit;
    }
    public static class FruitPerception
    {
        static Dictionary<int,FruitObservation> Prior(IndividualState observer)
        {
            var prior=new Dictionary<int,FruitObservation>();
            foreach(var known in observer.ownFruits.Concat(observer.groundFruitMemory).Concat(observer.foodMemory.SelectMany(f=>f.fruits))
                .Concat(observer.foodInteractions.Where(k=>k.offeredFruit!=null).Select(k=>k.offeredFruit)))
                prior[known.fruit]=known;
            return prior;
        }
        static FruitObservation Observe(World world, IndividualState observer, FruitState fruit,
            Dictionary<int,FruitObservation> prior)
        {
            if(!prior.TryGetValue(fruit.fruit,out var known))
            {
                var anchor=observer.fruitFreshnessMemory.Find(f=>f.fruit==fruit.fruit);
                prior[fruit.fruit]=known=new FruitObservation { fruit=fruit.fruit,
                    firstSeenAt=anchor?.firstSeenAt ?? world.State.time,
                    freshnessSince=anchor?.freshnessSince ?? world.State.time,
                    freshness=anchor?.freshness ?? FruitFreshness.Fresh };
            }
            var freshness=fruit.NearRot(world.State.time) ? FruitFreshness.NearRot : FruitFreshness.Fresh;
            if(freshness!=known.freshness) known.freshnessSince=world.State.time;
            known.freshness=freshness;known.holder=fruit.holder;known.source=fruit.source;
            known.x=fruit.holder==observer.individual ? observer.x : fruit.x;
            known.y=fruit.holder==observer.individual ? observer.y : fruit.y;
            known.visible=true;known.lastSeenAt=world.State.time;known.claimed=fruit.claimedBy!=0;
            known.claimedBy=fruit.claimedBy==observer.individual ||
                observer.perception.Any(e=>e.visible && e.individual==fruit.claimedBy) ? fruit.claimedBy : 0;
            return known;
        }
        public static void Refresh(World world, IndividualState observer)
        {
            observer.fruitFreshnessMemory.RemoveAll(f=>f.UpperBoundAt<=world.State.time);
            var prior=Prior(observer);
            foreach(var known in prior.Values) known.visible=false;
            observer.fruitKnowledgeAvailable=true;
            observer.ownFruits.Clear();
            foreach(var fruit in world.State.fruits.Where(f=>f.holder==observer.individual))
                observer.ownFruits.Add(Observe(world,observer,fruit,prior));
            observer.visibleFoods.Clear();
            foreach(var known in observer.foodMemory)
            {
                known.visible=false;known.possiblyAvailable=false;
                known.stale=world.State.time-known.lastSeenAt>=world.Config.foodMemorySeconds;
                known.fruits.RemoveAll(f=>observer.ownFruits.Any(own=>own.fruit==f.fruit));
            }
            foreach(var source in world.State.foods)
            {
                if(!world.CanSeePoint(observer,new Vector2(source.x,source.y))) continue;
                var known=observer.foodMemory.Find(f=>f.food==source.food);
                if(known==null) observer.foodMemory.Add(known=new FoodMemory { food=source.food });
                known.x=source.x;known.y=source.y;known.capacity=source.capacity;known.growthType=source.growthType;
                known.mature=source.mature;known.phase=source.phase;known.visible=true;known.stale=false;
                known.possiblyAvailable=false;known.lastSeenAt=world.State.time;
                known.fruits=world.State.fruits.Where(f=>f.source==source.food)
                    .OrderBy(f=>f.fruit).Select(f=>Observe(world,observer,f,prior)).ToList();
                known.amount=known.fruits.Count;
            }
            observer.foodMemory.RemoveAll(f=>!f.visible && world.CanSeePoint(observer,new Vector2(f.x,f.y)));
            observer.foodMemory.Sort((a,b)=>{
                int order=b.visible.CompareTo(a.visible);
                if(order==0) order=b.lastSeenAt.CompareTo(a.lastSeenAt);
                return order==0 ? a.food.CompareTo(b.food) : order;
            });
            int limit=Math.Min(32,world.Config.foodMemoryLimit);
            if(observer.foodMemory.Count>limit) observer.foodMemory.RemoveRange(limit,observer.foodMemory.Count-limit);
            observer.groundFruitMemory.RemoveAll(f=>observer.ownFruits.Any(own=>own.fruit==f.fruit));
            foreach(var fruit in world.State.fruits.Where(f=>f.holder==0 && f.source==0 &&
                world.CanSeePoint(observer,new Vector2(f.x,f.y))))
            {
                var known=Observe(world,observer,fruit,prior);
                if(!observer.groundFruitMemory.Any(f=>f.fruit==fruit.fruit)) observer.groundFruitMemory.Add(known);
            }
            observer.groundFruitMemory.RemoveAll(f=>!f.visible && (world.State.time-f.lastSeenAt>=10f ||
                world.CanSeePoint(observer,new Vector2(f.x,f.y))));
            observer.groundFruitMemory.Sort((a,b)=>{
                int order=b.visible.CompareTo(a.visible);
                if(order==0) order=b.lastSeenAt.CompareTo(a.lastSeenAt);
                return order==0 ? a.fruit.CompareTo(b.fruit) : order;
            });
            if(observer.groundFruitMemory.Count>16) observer.groundFruitMemory.RemoveRange(16,observer.groundFruitMemory.Count-16);
            foreach(var source in observer.foodMemory)
            {
                source.fruits.RemoveAll(f=>f.visible && f.source!=source.food);
                source.amount=source.fruits.Count;
            }
            observer.visibleFoods.AddRange(observer.foodMemory.Where(f=>f.visible));
            RefreshIndividuals(world,observer,prior);
            foreach(var observed in prior.Values.Where(f=>f.visible))
            {
                var anchor=observer.fruitFreshnessMemory.Find(f=>f.fruit==observed.fruit);
                if(anchor==null) observer.fruitFreshnessMemory.Add(anchor=new FruitFreshnessMemory { fruit=observed.fruit });
                anchor.firstSeenAt=observed.firstSeenAt;anchor.freshnessSince=observed.freshnessSince;
                anchor.lastSeenAt=observed.lastSeenAt;anchor.freshness=observed.freshness;
            }
            observer.fruitFreshnessMemory.Sort((a,b)=>{
                int order=b.lastSeenAt.CompareTo(a.lastSeenAt);
                return order==0 ? a.fruit.CompareTo(b.fruit) : order;
            });
            if(observer.fruitFreshnessMemory.Count>1024) observer.fruitFreshnessMemory.RemoveRange(1024,observer.fruitFreshnessMemory.Count-1024);
        }
        static void RefreshIndividuals(World world, IndividualState observer, Dictionary<int,FruitObservation> prior)
        {
            bool VisiblePartner(int id) => id==observer.individual || observer.perception.Any(e=>e.visible && e.individual==id);
            foreach(var known in observer.foodInteractions) known.visible=false;
            foreach(var entity in observer.perception.Where(e=>e.visible))
            {
                var target=world.State.individuals.Find(a=>a.individual==entity.individual);
                var known=observer.foodInteractions.Find(k=>k.individual==entity.individual);
                if(known==null) observer.foodInteractions.Add(known=new FoodInteractionObservation { individual=entity.individual });
                known.x=entity.x;known.y=entity.y;known.visible=known.known=true;known.lastSeenAt=world.State.time;
                known.carriedCount=world.State.fruits.Count(f=>f.holder==target.individual);
                known.operation=target.foodOperation;known.operationFruit=target.operationFruit;
                known.operationTarget=VisiblePartner(target.operationTarget) ? target.operationTarget : 0;
                known.deliveryInviteTo=VisiblePartner(target.deliveryInviteTo) ? target.deliveryInviteTo : 0;
                known.deliveryInviteFruit=known.deliveryInviteTo!=0 ? target.deliveryInviteFruit : 0;
                var offered=world.State.fruits.Find(f=>f.fruit==known.deliveryInviteFruit && f.holder==target.individual);
                known.offeredFruit=offered==null ? null : Observe(world,observer,offered,prior);
                if(known.offeredFruit!=null) { known.offeredFruit.x=target.x;known.offeredFruit.y=target.y; }
                var source=world.State.foods.Find(f=>f.food==target.cultivatingFood &&
                    world.CanSeePoint(observer,new Vector2(f.x,f.y)));
                int previousSource=known.cultivatingFood;
                known.cultivatingFood=source==null ? 0 : source.food;
                known.cultivationStrength=source==null ? 0 : target.cultivateGrowth;
                if(previousSource!=known.cultivatingFood) known.cultivationObservedSince=world.State.time;
                known.hasSource=false;known.sourceX=known.sourceY=0;
                var operated=world.State.fruits.Find(f=>f.fruit==target.operationFruit);
                bool seesFruit=operated!=null && (operated.holder==target.individual || operated.holder==observer.individual ||
                    operated.holder!=0 && VisiblePartner(operated.holder) || operated.holder==0 &&
                    world.CanSeePoint(observer,new Vector2(operated.x,operated.y)));
                if(!seesFruit) known.operationFruit=0;
                if(operated!=null && (known.operation==FoodAction.Gather || known.operation==FoodAction.Pickup) &&
                    world.CanSeePoint(observer,new Vector2(operated.x,operated.y)))
                { known.hasSource=true;known.sourceX=operated.x;known.sourceY=operated.y; }
                else if(target.feeding)
                { known.hasSource=true;known.sourceX=target.x;known.sourceY=target.y; }
                entity.food=known;
            }
            foreach(var known in observer.foodInteractions)
                if(!known.visible && world.State.time-known.lastSeenAt>=5f)
                { known.known=false;known.carriedCount=-1;known.operation=FoodAction.None;known.offeredFruit=null; }
            observer.foodInteractions.RemoveAll(k=>!k.known);
            observer.foodInteractions.Sort((a,b)=>{
                int order=b.visible.CompareTo(a.visible);
                if(order==0) order=b.lastSeenAt.CompareTo(a.lastSeenAt);
                return order==0 ? a.individual.CompareTo(b.individual) : order;
            });
            if(observer.foodInteractions.Count>256) observer.foodInteractions.RemoveRange(256,observer.foodInteractions.Count-256);
            foreach(var child in observer.children)
                child.food=child.confirmedDead ? null : observer.foodInteractions.Find(k=>k.individual==child.child && k.known);
        }
    }
}
