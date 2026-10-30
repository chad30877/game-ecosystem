using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ecosystem
{
    // 培育在本步實際位移與傷害之後檢查；只有本步付得起的有效時間能加速。
    public static class ProductionRules
    {
        sealed class Session
        {
            public IndividualState actor;
            public FoodState source;
            public float growth, started;
        }

        static void Stop(IndividualState actor)
        {
            actor.cultivatingFood = 0;
            actor.cultivateUntil = actor.cultivateRemaining = 0;
        }

        static bool Eligible(World world, IndividualState actor, WorldAction action, WorldSkill skill, float time) =>
            (action.skillSlot >= 0 ? actor.loadout[action.skillSlot] : action.skill) == skill &&
            Array.IndexOf(actor.loadout, skill) >= 0 && actor.health > 0 &&
            (world.Config.disableAging || time - actor.bornAt < world.Config.lifespan) &&
            !action.attack && !action.forage && action.feedChild == 0 && action.foodAction == FoodAction.None &&
            !actor.chargeActiveThisStep && !actor.dashActiveThisStep && !actor.windupActiveThisStep;

        public static bool CanCultivate(World world, IndividualState actor, FoodState source) =>
            source != null && source.phase != FoodPhase.Waiting &&
            source.bornAt + FruitRules.Growth(world.Config, source).lifetimeSeconds > world.State.time &&
            Vector2.Distance(CombatRules.Position(actor), new Vector2(source.x, source.y)) <= world.Config.combat.cultivateRange &&
            world.CanSeePoint(actor, new Vector2(source.x, source.y));

        public static void Advance(World world, IReadOnlyDictionary<int, WorldAction> actions,
            System.Random sourceRandom, System.Random productionRandom, float time)
        {
            var state = world.State;
            foreach (var source in state.foods)
            {
                if (source.cultivator != 0) source.growthMultiplier = 1;
                source.cultivator = 0; source.growthBoostUntil = 0;
            }
            var candidates = new List<Session>();
            foreach (var actor in state.individuals)
            {
                int prior = actor.cultivatingFood;
                float started = actor.cultivateStartedAt;
                Stop(actor);
                var action = actions[actor.individual];
                if (!Eligible(world, actor, action, WorldSkill.Cultivate, time) || action.movement.sqrMagnitude > 0 || actor.mana <= 0)
                    continue;
                FoodState source;
                if (action.productionSource != 0)
                    source = state.foods.Find(f => f.food == action.productionSource && CanCultivate(world, actor, f));
                else
                    source = state.foods.Find(f => f.food == prior && CanCultivate(world, actor, f)) ??
                        state.foods.Where(f => CanCultivate(world, actor, f))
                            .OrderBy(f => Vector2.Distance(CombatRules.Position(actor), new Vector2(f.x, f.y)))
                            .ThenBy(f => f.food).FirstOrDefault();
                if (source == null) continue;
                candidates.Add(new Session { actor = actor, source = source, growth = CombatRules.Growth(world.Config, actor),
                    started = prior == source.food ? started : state.time });
            }
            var active = new List<Session>();
            foreach (var group in candidates.GroupBy(c => c.source.food).OrderBy(g => g.Key))
            {
                float strongest = group.Max(c => c.growth);
                var equal = group.Where(c => Mathf.Abs(c.growth - strongest) < .00001f).ToArray();
                float first = equal.Min(c => c.started);
                var simultaneous = equal.Where(c => c.started == first).OrderBy(c => c.actor.individual).ToArray();
                var session = simultaneous[productionRandom.Next(simultaneous.Length)];
                active.Add(session);
                var actor = session.actor; var source = session.source;
                source.growthMultiplier = Mathf.Min(2, 1 + session.growth);
                source.cultivator = actor.individual;
                source.growthBoostUntil = state.time + Mathf.Min(world.Config.stepSeconds, actor.mana / world.Config.combat.cultivateMana);
                actor.cultivatingFood = source.food; actor.cultivateStartedAt = session.started;
                actor.cultivateUntil = time + world.Config.stepSeconds; actor.cultivateGrowth = session.growth;
                actor.skillUsedThisStep = true; actor.lastSkill = WorldSkill.Cultivate;
            }
            FruitRules.Advance(world.Config, state, sourceRandom, time);
            foreach (var session in active)
            {
                float cost = Mathf.Min(session.actor.mana, world.Config.combat.cultivateMana * session.source.cultivatedSeconds);
                session.actor.mana -= cost; session.actor.skillManaSpent += cost;
                if (cost > 0)
                    state.events.Add(new WorldEvent { kind = "cultivated", individual = session.actor.individual,
                        food = session.source.food, skill = WorldSkill.Cultivate, amount = cost });
                if (session.actor.mana <= 0 || !state.foods.Contains(session.source) || session.source.phase == FoodPhase.Waiting)
                {
                    Stop(session.actor); session.source.cultivator = 0; session.source.growthMultiplier = 1;
                }
            }
            foreach (var actor in state.individuals.OrderBy(a => a.individual))
            {
                var action = actions[actor.individual];
                if (!Eligible(world, actor, action, WorldSkill.Sow, time)) continue;
                int slot = Array.IndexOf(actor.loadout, WorldSkill.Sow);
                var config = world.Config.combat;
                if (time < actor.skillReadyTimes[slot] || actor.mana < config.sowMana ||
                    !SkillRules.LegalPoint(world.Config, actor, action.aim, config.sowRange) ||
                    !world.CanSeePoint(actor, action.aim) ||
                    !SourcePlacementRules.TryCreate(world.Config, state, productionRandom, action.aim,
                        false, .2f, time, out var seed)) continue;
                actor.mana -= config.sowMana; actor.skillManaSpent += config.sowMana;
                actor.skillReadyAt = actor.skillReadyTimes[slot] = time + config.sowCooldown;
                actor.skillUsedThisStep = true; actor.lastSkill = WorldSkill.Sow; actor.skillFlashUntil = time + .35f;
                state.events.Add(new WorldEvent { kind = "sow", individual = actor.individual, food = seed.food,
                    amount = config.sowMana, skill = WorldSkill.Sow, x = seed.x, y = seed.y });
            }
        }
    }
}
