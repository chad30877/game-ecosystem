using System;
using UnityEngine;

namespace Ecosystem
{
    public static class SkillRules
    {
        public static float ManaCost(CombatConfig config, WorldSkill skill) => skill switch
        {
            WorldSkill.Charge => config.chargeMana, WorldSkill.HotStrike => config.hotStrikeMana,
            WorldSkill.Shield => config.shieldMana, WorldSkill.IntimidatingRoar => config.roarMana,
            WorldSkill.FrostbindArrow => config.frostbindMana, WorldSkill.PiercingIceArrow => config.piercingMana,
            WorldSkill.ColdMistDash => config.dashMana, WorldSkill.FrostTrap => config.trapMana,
            WorldSkill.Cultivate => config.cultivateMana, WorldSkill.Sow => config.sowMana,
            WorldSkill.EntanglingRoots => config.rootsMana, WorldSkill.SporeSmoke => config.smokeMana,
            _ => float.PositiveInfinity
        };

        public static bool TryCast(WorldConfig config, WorldState state, IndividualState individual, WorldAction action)
        {
            WorldSkill skill = action.skillSlot >= 0 ? individual.loadout[action.skillSlot] : action.skill;
            int slot = Array.IndexOf(individual.loadout, skill);
            if (slot < 0 || state.time < individual.skillReadyTimes[slot] || individual.chargeRemaining > 0 || individual.dashRemaining > 0 ||
                individual.windupActiveThisStep || state.time < individual.cultivateUntil) return false;
            var c = config.combat;
            float mana = ManaCost(c, skill);
            Vector2 direction = CombatRules.AimDirection(individual, action);
            switch (skill)
            {
                case WorldSkill.Charge:
                    if (direction.sqrMagnitude == 0 || !Spend(state, individual, slot, mana, c.chargeCooldown, "charge")) return false;
                    individual.chargeRemaining = c.chargeDistance;
                    individual.aimX = direction.x;
                    individual.aimY = direction.y;
                    individual.chargeActiveThisStep = true;
                    individual.lastCombatAt = state.time;
                    return true;
                case WorldSkill.HotStrike:
                    if (direction.sqrMagnitude == 0) return false;
                    var target = CombatRules.FirstHit(state, individual.species, CombatRules.Position(individual),
                        CombatRules.Position(individual) + direction * c.hotStrikeRange, c.hitRadius, out _);
                    if (target == null || !Spend(state, individual, slot, mana, c.hotStrikeCooldown, "hot_strike")) return false;
                    CombatRules.Damage(config, state, individual, target, c.hotStrikeDamage, "hot_strike_hit", state.time);
                    return true;
                case WorldSkill.Shield:
                    if (!Spend(state, individual, slot, mana, c.shieldCooldown, "shield")) return false;
                    individual.shieldUntil = state.time + c.shieldDuration;
                    return true;
                case WorldSkill.IntimidatingRoar:
                    var enemies = state.individuals.FindAll(value => value.species != individual.species && value.health > 0 &&
                        Vector2.Distance(CombatRules.Position(individual), CombatRules.Position(value)) <= c.roarRange);
                    if (enemies.Count == 0 || !Spend(state, individual, slot, mana, c.roarCooldown, "roar")) return false;
                    foreach (var enemy in enemies)
                        ControlEffects.AddBias(state, enemy, skill, CombatRules.Position(enemy) - CombatRules.Position(individual), c.roarDuration);
                    return true;
                case WorldSkill.FrostbindArrow:
                case WorldSkill.PiercingIceArrow:
                    if (direction.sqrMagnitude == 0 || !Spend(state, individual, slot,
                        mana,
                        skill == WorldSkill.FrostbindArrow ? c.frostbindCooldown : c.piercingCooldown,
                        skill == WorldSkill.FrostbindArrow ? "frostbind_windup" : "piercing_windup")) return false;
                    individual.windupSkill = skill;
                    individual.windupUntil = state.time + c.iceWindup;
                    individual.windupActiveThisStep = true;
                    individual.aimX = direction.x;
                    individual.aimY = direction.y;
                    individual.lastCombatAt = state.time;
                    return true;
                case WorldSkill.ColdMistDash:
                    float dashDistance = Mathf.Min(c.dashDistance, Vector2.Distance(CombatRules.Position(individual), action.aim));
                    Vector2 landing = CombatRules.Position(individual) + direction * dashDistance;
                    if (state.time < individual.rootUntil || direction.sqrMagnitude == 0 || !LegalPoint(config, individual, landing, c.dashDistance + 0.0001f) ||
                        !Spend(state, individual, slot, mana, c.dashCooldown, "mist_dash")) return false;
                    individual.dashRemaining = dashDistance;
                    individual.dashActiveThisStep = true;
                    individual.aimX = direction.x;
                    individual.aimY = direction.y;
                    return true;
                case WorldSkill.FrostTrap:
                    if (!LegalPoint(config, individual, action.aim, c.trapRange) ||
                        state.areas.Exists(value => value.skill == skill && Vector2.Distance(new Vector2(value.x, value.y), action.aim) < c.trapRadius * 2) ||
                        !Spend(state, individual, slot, mana, c.trapCooldown, "trap_placed")) return false;
                    AddArea(state, individual, skill, action.aim, c.trapRadius, c.trapDuration);
                    return true;
                case WorldSkill.Sow:
                    if (!LegalPoint(config, individual, action.aim, c.sowRange) || state.foods.Count >= config.foodSourceLimit ||
                        state.foods.Exists(value => Vector2.Distance(new Vector2(value.x, value.y), action.aim) < c.sowSpacing) ||
                        state.individuals.Exists(value => value.health > 0 && Vector2.Distance(CombatRules.Position(value), action.aim) <= c.hitRadius) ||
                        state.areas.Exists(value => value.skill == WorldSkill.FrostTrap && value.until > state.time &&
                            Vector2.Distance(new Vector2(value.x, value.y), action.aim) <= value.radius) ||
                        !Spend(state, individual, slot, mana, c.sowCooldown, "sow")) return false;
                    var seed = new FoodState { food = ++state.nextFood, x = action.aim.x, y = action.aim.y,
                        growthType = FoodGrowthType.Small, capacity = config.smallFood.capacity,
                        phase = FoodPhase.Recovering, mature = false, matureAt = state.time + c.sowMaturity,
                        bornAt = state.time };
                    state.foods.Add(seed);
                    state.events[state.events.Count - 1].food = seed.food;
                    state.events[state.events.Count - 1].x = seed.x;
                    state.events[state.events.Count - 1].y = seed.y;
                    return true;
                case WorldSkill.EntanglingRoots:
                    var rootedEnemies = state.individuals.FindAll(value => value.health > 0 && value.species != individual.species &&
                        Vector2.Distance(CombatRules.Position(value), CombatRules.Position(individual)) <= c.rootsRange);
                    if (rootedEnemies.Count == 0 || !Spend(state, individual, slot, mana, c.rootsCooldown, "roots")) return false;
                    foreach (var enemy in rootedEnemies) ControlEffects.Root(state, enemy, state.time, c.rootsDuration);
                    return true;
                case WorldSkill.SporeSmoke:
                    if (!Spend(state, individual, slot, mana, c.smokeCooldown, "spore_smoke")) return false;
                    AddArea(state, individual, skill, CombatRules.Position(individual), c.smokeRadius, c.smokeDuration);
                    return true;
                case WorldSkill.Cultivate:
                    FoodState foodTarget = null;
                    float nearest = c.cultivateRange;
                    foreach (var food in state.foods)
                    {
                        float distance = Vector2.Distance(CombatRules.Position(individual), new Vector2(food.x, food.y));
                        if (distance > nearest || !World.InView(config, individual, new Vector2(food.x, food.y)) ||
                            !food.mature || food.phase == FoodPhase.Resting || food.amount >= food.capacity) continue;
                        nearest = distance;
                        foodTarget = food;
                    }
                    if (foodTarget == null || !Spend(state, individual, slot, mana, c.cultivateCooldown, "cultivate")) return false;
                    individual.cultivatingFood = foodTarget.food;
                    individual.cultivateUntil = state.time + c.cultivateDuration;
                    state.events[state.events.Count - 1].food = foodTarget.food;
                    return true;
                default: return false;
            }
        }

        public static bool LegalPoint(WorldConfig config, IndividualState individual, Vector2 point, float range) =>
            Mathf.Abs(point.x) <= config.halfExtent && Mathf.Abs(point.y) <= config.halfExtent &&
            Vector2.Distance(CombatRules.Position(individual), point) <= range;

        private static void AddArea(WorldState state, IndividualState individual, WorldSkill skill, Vector2 point, float radius, float duration) =>
            state.areas.Add(new AreaState { area = ++state.nextArea, owner = individual.individual, species = individual.species,
                skill = skill, x = point.x, y = point.y, radius = radius, until = state.time + duration });

        private static bool Spend(WorldState state, IndividualState individual, int slot, float mana, float cooldown, string kind)
        {
            if (individual.mana < mana) return false;
            individual.mana -= mana; individual.skillManaSpent += mana;
            var skill = individual.loadout[slot];
            if (skill == WorldSkill.Charge || skill == WorldSkill.ColdMistDash || skill == WorldSkill.HotStrike)
                individual.lastStrenuousAt = state.time;
            if (skill == WorldSkill.Charge || skill == WorldSkill.HotStrike || skill == WorldSkill.IntimidatingRoar || skill == WorldSkill.FrostbindArrow || skill == WorldSkill.PiercingIceArrow || skill == WorldSkill.EntanglingRoots || skill == WorldSkill.SporeSmoke || skill == WorldSkill.FrostTrap)
                individual.lastCombatAt = state.time;
            individual.skillReadyAt = individual.skillReadyTimes[slot] = state.time + cooldown;
            individual.skillUsedThisStep = true;
            individual.lastSkill = individual.loadout[slot];
            individual.skillFlashUntil = state.time + 0.35f;
            state.events.Add(new WorldEvent { kind = kind, individual = individual.individual, amount = mana, skill = individual.loadout[slot], x = individual.x, y = individual.y });
            return true;
        }
    }
}
