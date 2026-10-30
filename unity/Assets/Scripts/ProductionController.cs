using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Ecosystem
{
    public static class ProductionController
    {
        public static WorldAction? Choose(World world,IndividualState actor)
        {
            actor.productionDecision="no_skill";
            if(!actor.loadout.Contains(WorldSkill.Cultivate) && !actor.loadout.Contains(WorldSkill.Sow)) return null;
            var threat=FoodCareController.CloseThreat(world,actor);
            if(threat!=null)
            {
                actor.productionDecision="danger";
                return FoodCareController.Escape(world,actor,
                    (CombatRules.Position(actor)-new Vector2(threat.x,threat.y)).normalized);
            }
            var local=actor.foodMemory.Where(f=>Vector2.Distance(CombatRules.Position(actor),new Vector2(f.x,f.y))<=8).ToList();
            float strength=CombatRules.Growth(world.Config,actor);
            var source=actor.visibleFoods.Where(f=>f.phase!=FoodPhase.Waiting &&
                !actor.foodInteractions.Any(other=>other.visible && other.known && other.cultivatingFood==f.food &&
                    (other.cultivationStrength>strength+.00001f ||
                     actor.cultivatingFood!=f.food && other.cultivationStrength>=strength-.00001f)) &&
                Vector2.Distance(CombatRules.Position(actor),new Vector2(f.x,f.y))<=world.Config.combat.cultivateRange)
                .OrderBy(f=>f.food==actor.cultivatingFood ? 0 : 1)
                .ThenBy(f=>Vector2.Distance(CombatRules.Position(actor),new Vector2(f.x,f.y))).ThenBy(f=>f.food).FirstOrDefault();
            if(source!=null && actor.loadout.Contains(WorldSkill.Cultivate) && actor.mana>0)
            {
                actor.decisionReason="cultivate_source";
                actor.productionDecision="cultivate";
                return new WorldAction(Vector2.zero,skill:WorldSkill.Cultivate,productionSource:source.food);
            }
            if(local.Any(f=>!f.visible || f.phase!=FoodPhase.Waiting))
            {
                actor.productionDecision=actor.loadout.Contains(WorldSkill.Cultivate) && actor.mana<=0 ? "no_mana" :
                    local.Any(f=>!f.visible) ? "source_revisit" : source==null && actor.foodInteractions.Any(i=>i.visible && i.cultivatingFood!=0) ?
                    "other_cultivator" : "local_supply";
                return null;
            }
            if(!actor.loadout.Contains(WorldSkill.Sow)) { actor.productionDecision="waiting";return null; }
            if(actor.mana<40) { actor.productionDecision="sow_low_mana";return null; }
            if(actor.satiety<30 || !CanWait(world,actor)) { actor.productionDecision="cannot_wait";return null; }
            if(actor.ownFruits.Count>=FoodOperations.Capacity(world.Config,actor) && !DeliveryNeeded(world,actor))
            { actor.productionDecision="full";return null; }
            int slot=Array.IndexOf(actor.loadout,WorldSkill.Sow);
            if(actor.skillCooldowns[slot]>0) { actor.productionDecision="sow_cooldown";return null; }
            if(world.State.time-actor.lastSowAttemptAt<1 && world.State.step!=actor.lastSowAttemptStep)
            { actor.productionDecision="sow_retry";return null; }
            var candidates=Candidates(world,actor);
            if(candidates.Count==0) { actor.productionDecision="sow_no_space";return null; }
            var random=new System.Random(unchecked(world.Seed*397 ^ actor.individual*7919 ^ world.State.step));
            var point=candidates[random.Next(candidates.Count)];actor.decisionReason="sow_source";
            actor.productionDecision="sow";
            actor.lastSowAttemptAt=world.State.time;actor.lastSowAttemptStep=world.State.step;
            return new WorldAction(Vector2.zero,aim:point,skill:WorldSkill.Sow);
        }
        public static void Record(IndividualState actor,WorldAction action,bool cultivated)
        {
            if(actor.decisionReason.StartsWith("smoke_") || actor.decisionReason.Contains("retreat") ||
                actor.decisionReason=="protect_child" || actor.decisionReason.StartsWith("contest")) actor.productionDecision="danger";
            else if(action.foodAction!=FoodAction.None || action.forage) actor.productionDecision="food_priority";
            else if(actor.productionDecision=="") actor.productionDecision=actor.decisionReason.StartsWith("care_") ||
                actor.decisionReason=="approach_parent" ? "care_priority" : actor.decisionReason=="source_revisit" ? "source_revisit" : "other_priority";
            if(actor.productionDecision=="cultivate") actor.productionStopReason="";
            else if(cultivated) actor.productionStopReason=actor.productionDecision;
        }
        static bool DeliveryNeeded(World world,IndividualState actor) => !world.Config.disableCare &&
            actor.children.Any(c=>actor.foodCareChildren.Contains(c.child) && !c.confirmedDead &&
                c.predictedAge<world.Config.maturityAge && c.predictedSatiety<60 &&
                c.food!=null && c.food.known && c.food.carriedCount==0);
        static bool CanWait(World world,IndividualState actor)
        {
            float wait=Math.Max(world.Config.smallFood.firstMaturitySeconds,world.Config.largeFood.firstMaturitySeconds)+
                FoodCareController.WalkSeconds(world,actor,world.Config.combat.sowRange)+3;
            float consumption=world.Config.idleCost+(actor.nutritionActive.Any(active=>active) ? world.Config.nutritionBudget : 0);
            return actor.satiety>wait*consumption && (world.Config.disableAging || actor.age+wait<world.Config.lifespan);
        }
        static List<Vector2> Candidates(World world,IndividualState actor)
        {
            var candidates=new List<Vector2>();float radius=world.Config.smallFood.growthRadius;
            for(int distance=1;distance<=3;distance++) for(int angle=-60;angle<=60;angle+=10)
            {
                float radians=(actor.facing+angle)*Mathf.Deg2Rad;
                var point=CombatRules.Position(actor)+new Vector2(Mathf.Cos(radians),Mathf.Sin(radians))*
                    (Mathf.Min(world.Config.combat.sowRange,world.Config.observationRadius)*distance/3);
                if(!World.InView(world.Config,actor,point)) continue;
                if(Mathf.Abs(point.x)+radius>world.Config.halfExtent || Mathf.Abs(point.y)+radius>world.Config.halfExtent) continue;
                if(actor.foodMemory.Any(f=>Vector2.Distance(point,new Vector2(f.x,f.y))<radius+
                    (f.growthType==FoodGrowthType.Large ? world.Config.largeFood.growthRadius : radius))) continue;
                if(actor.perception.Any(e=>e.visible && e.species!=actor.species &&
                    Vector2.Distance(point,new Vector2(e.x,e.y))<=world.Config.scanThreatRadius)) continue;
                if(actor.smokeKnowledge.Any(s=>s.harmful && Vector2.Distance(point,new Vector2(s.x,s.y))<=s.radius)) continue;
                candidates.Add(point);
            }
            return candidates;
        }
    }
}
