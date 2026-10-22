using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ecosystem
{
    public enum WorldSkill { None, Charge, Cultivate, Shield, HotStrike, IntimidatingRoar, FrostbindArrow, PiercingIceArrow, ColdMistDash, FrostTrap, Sow, EntanglingRoots, SporeSmoke }

    [Serializable]
    public sealed class CombatConfig
    {
        public float flameSpeed = 2.5f;
        public float frostSpeed = 3.2f;
        public float mossSpeed = 2.7f;
        public float meleeRange = 1.1f;
        public float meleeDamage = 12f;
        public float meleeEnergy = 2f;
        public float meleeCooldown = 1f;
        public float chargeDistance = 4f;
        public float chargeSpeed = 10f;
        public float chargeDamage = 24f;
        public float chargeEnergy = 12f;
        public float chargeCooldown = 6f;
        public float hitRadius = 0.55f;
        public float recoveryEnergy = 50f;
        public float recoveryDelay = 5f;
        public float recoveryRate = 2f;
        public float iceRange = 6f;
        public float iceSpeed = 8f;
        public float iceDamage = 10f;
        public float iceEnergy = 6f;
        public float iceWindup = 0.45f;
        public float iceCooldown = 2.5f;
        public float slowDuration = 1f;
        public float slowMultiplier = 0.55f;
        public float cultivateRange = 1.5f;
        public float cultivateEnergy = 8f;
        public float cultivateCooldown = 6f;
        public float cultivateDuration = 3f;
        public float cultivateRate = 4f;
        public float shieldEnergy = 10f;
        public float shieldCooldown = 8f;
        public float shieldDuration = 2f;
        public float shieldMultiplier = 0.35f;

        public float hotStrikeEnergy = 10f;
        public float hotStrikeCooldown = 5f;
        public float hotStrikeDamage = 26f;
        public float hotStrikeRange = 1.5f;
        public float roarEnergy = 9f;
        public float roarCooldown = 7f;
        public float roarRange = 3f;
        public float roarDuration = 2f;
        public float roarBias = 0.2f;
        public float maxMovementBias = 0.35f;

        public float frostbindEnergy = 10f;
        public float frostbindCooldown = 5f;
        public float frostbindDamage = 8f;
        public float piercingEnergy = 14f;
        public float piercingCooldown = 7f;
        public float piercingDamage = 18f;
        public float piercingRange = 8f;
        public float dashEnergy = 12f;
        public float dashCooldown = 7f;
        public float dashDistance = 3f;
        public float dashSpeed = 8f;
        public float trapEnergy = 10f;
        public float trapCooldown = 8f;
        public float trapRange = 4f;
        public float trapRadius = 0.6f;
        public float trapDuration = 10f;
        public float trapRootDuration = 1.2f;
        public float trapSlowDuration = 2f;

        public float sowEnergy = 12f;
        public float sowCooldown = 8f;
        public float sowRange = 3f;
        public float sowSpacing = 0.8f;
        public float sowMaturity = 5f;
        public float rootsEnergy = 10f;
        public float rootsCooldown = 7f;
        public float rootsRange = 2.5f;
        public float rootsDuration = 2f;
        public float smokeEnergy = 14f;
        public float smokeCooldown = 10f;
        public float smokeRadius = 2.5f;
        public float smokeDuration = 8f;
        public float smokeBias = 0.15f;
        public float smokeBiasDuration = 0.6f;
        public float smokeDirectionInterval = 1.5f;
        public float smokeTurnSpeed = 90f;
        public float poisonInterval = 1f;
        public float poisonDuration = 3f;
        public float poisonDamage = 1f;

        public void Validate()
        {
            foreach (float value in new[] { flameSpeed, frostSpeed, mossSpeed, meleeRange, meleeDamage, meleeEnergy, meleeCooldown,
                chargeDistance, chargeSpeed, chargeDamage, chargeEnergy, chargeCooldown,
                hitRadius, recoveryEnergy, recoveryDelay, recoveryRate, iceRange, iceSpeed, iceDamage,
                iceEnergy, iceWindup, iceCooldown, slowDuration, slowMultiplier, cultivateRange, cultivateEnergy,
                cultivateCooldown, cultivateDuration, cultivateRate, shieldEnergy, shieldCooldown, shieldDuration, shieldMultiplier, hotStrikeEnergy, hotStrikeCooldown, hotStrikeDamage, hotStrikeRange,
                roarEnergy, roarCooldown, roarRange, roarDuration, roarBias, maxMovementBias,
                frostbindEnergy, frostbindCooldown, frostbindDamage, piercingEnergy, piercingCooldown, piercingDamage, piercingRange,
                dashEnergy, dashCooldown, dashDistance, dashSpeed, trapEnergy, trapCooldown, trapRange, trapRadius, trapDuration, trapRootDuration, trapSlowDuration,
                sowEnergy, sowCooldown, sowRange, sowSpacing, sowMaturity, rootsEnergy, rootsCooldown, rootsRange, rootsDuration,
                smokeEnergy, smokeCooldown, smokeRadius, smokeDuration, smokeBias, smokeBiasDuration, smokeDirectionInterval, smokeTurnSpeed,
                poisonInterval, poisonDuration, poisonDamage })
                if (!float.IsFinite(value) || value <= 0) throw new ArgumentException("戰鬥參數須為有限正數。");
            if (meleeEnergy > 100 || chargeEnergy > 100 || recoveryEnergy > 100 || iceEnergy > 100 || cultivateEnergy > 100 || shieldEnergy > 100 || hotStrikeEnergy > 100 || roarEnergy > 100 || frostbindEnergy > 100 || piercingEnergy > 100 || dashEnergy > 100 || trapEnergy > 100 || sowEnergy > 100 || rootsEnergy > 100 || smokeEnergy > 100)
                throw new ArgumentException("戰鬥能量門檻不得大於 100。");
            if (maxMovementBias >= 1 || roarBias > maxMovementBias || smokeBias > maxMovementBias) throw new ArgumentException("移動偏向須保留個體操作能力。");
            if (slowDuration >= iceCooldown || slowMultiplier >= 1 || iceWindup >= iceCooldown)
                throw new ArgumentException("減速時間及前搖須短於冰箭冷卻，減速倍率須介於 0 與 1。");
            if (shieldMultiplier >= 1 || shieldDuration >= shieldCooldown || cultivateDuration >= cultivateCooldown)
                throw new ArgumentException("技能持續時間須短於冷卻，護盾倍率須介於 0 與 1。");
        }
    }

    public static class CombatRules
    {
        public static float MaxHealth(string species) => species == "Flame" ? 120f : species == "Frost" ? 70f : 90f;
        public static Vector2 Position(IndividualState individual) => new Vector2(individual.x, individual.y);

        public static Vector2 AimDirection(IndividualState individual, WorldAction action) => (action.aim - Position(individual)).normalized;

        public static void BeginStep(WorldConfig config, WorldState state, IReadOnlyDictionary<int, WorldAction> actions)
        {
            foreach (var individual in state.individuals)
            {
                individual.dashActiveThisStep = individual.dashRemaining > 0;
                individual.chargeActiveThisStep = individual.chargeRemaining > 0;
                individual.windupActiveThisStep = individual.windupUntil > 0;
                individual.skillUsedThisStep = false;
                if (individual.health <= 0) continue;
                var action = actions[individual.individual];
                if (individual.windupUntil > 0 && state.time >= individual.windupUntil)
                {
                    state.projectiles.Add(new ProjectileState { projectile = ++state.nextProjectile, owner = individual.individual,
                        species = individual.species, skill = individual.windupSkill, x = individual.x, y = individual.y,
                        damage = individual.windupSkill == WorldSkill.FrostbindArrow ? config.combat.frostbindDamage
                            : individual.windupSkill == WorldSkill.PiercingIceArrow ? config.combat.piercingDamage : config.combat.iceDamage,
                        directionX = individual.aimX, directionY = individual.aimY,
                        remaining = individual.windupSkill == WorldSkill.PiercingIceArrow ? config.combat.piercingRange : config.combat.iceRange });
                    individual.windupUntil = 0;
                    state.events.Add(new WorldEvent { kind = "ice_fired", skill = individual.windupSkill, individual = individual.individual, x = individual.x, y = individual.y });
                }
                SkillRules.TryCast(config, state, individual, action);
                Vector2 direction = AimDirection(individual, action);
                if (individual.species == "Frost" && action.attack && !individual.windupActiveThisStep && !individual.dashActiveThisStep && !individual.skillUsedThisStep && direction.sqrMagnitude > 0 &&
                    state.time >= individual.attackReadyAt && individual.energy >= config.combat.iceEnergy)
                {
                    individual.energy -= config.combat.iceEnergy;
                    individual.attackReadyAt = state.time + config.combat.iceCooldown;
                    individual.windupSkill = WorldSkill.None;
                    individual.windupUntil = state.time + config.combat.iceWindup;
                    individual.windupActiveThisStep = true;
                    individual.aimX = direction.x;
                    individual.aimY = direction.y;
                    individual.lastCombatAt = state.time;
                    state.events.Add(new WorldEvent { kind = "ice_windup", individual = individual.individual, amount = config.combat.iceEnergy });
                }
            }
        }

        public static Vector2 Move(WorldConfig config, WorldState state, IndividualState individual, WorldAction action)
        {
            if (state.time < individual.rootUntil) { individual.boundary = false; return Position(individual); }
            float speed = individual.species == "Frost" ? config.combat.frostSpeed : individual.species == "Moss" ? config.combat.mossSpeed : config.combat.flameSpeed;
            bool charging = individual.chargeRemaining > 0;
            bool dashing = individual.dashRemaining > 0;
            if (!charging && state.time < individual.slowUntil) speed *= config.combat.slowMultiplier;
            Vector2 delta = dashing ? new Vector2(individual.aimX, individual.aimY) * Mathf.Min(individual.dashRemaining, config.combat.dashSpeed * config.stepSeconds)
                : charging ? new Vector2(individual.aimX, individual.aimY) *
                Mathf.Min(individual.chargeRemaining, config.combat.chargeSpeed * config.stepSeconds)
                : individual.windupActiveThisStep ? Vector2.zero : Vector2.ClampMagnitude(action.movement + ControlEffects.MovementBias(config, state, individual), 1) * (speed * config.stepSeconds);
            Vector2 start = Position(individual);
            Vector2 end = start + delta;
            end = new Vector2(Mathf.Clamp(end.x, -config.halfExtent, config.halfExtent), Mathf.Clamp(end.y, -config.halfExtent, config.halfExtent));
            individual.boundary = end != start + delta;
            if (dashing) individual.dashRemaining = individual.boundary ? 0 : Mathf.Max(0, individual.dashRemaining - delta.magnitude);
            if (charging)
            {
                var target = FirstHit(state, individual.species, start, end, config.combat.hitRadius, out float fraction);
                if (target != null)
                {
                    end = Vector2.Lerp(start, end, fraction);
                    Damage(config, state, individual, target, config.combat.chargeDamage, "charge_hit", state.time + config.stepSeconds);
                    individual.chargeRemaining = 0;
                }
                else individual.chargeRemaining = individual.boundary ? 0 : Mathf.Max(0, individual.chargeRemaining - delta.magnitude);
            }
            return end;
        }

        public static void Resolve(WorldConfig config, WorldState state, IReadOnlyDictionary<int, WorldAction> actions)
        {
            foreach (var individual in state.individuals)
            {
                var action = actions[individual.individual];
                if (individual.health <= 0 || individual.species != "Flame" || individual.chargeActiveThisStep || individual.skillUsedThisStep || !action.attack ||
                    AimDirection(individual, action).sqrMagnitude == 0 || state.time < individual.attackReadyAt || individual.energy < config.combat.meleeEnergy) continue;
                individual.energy -= config.combat.meleeEnergy;
                individual.attackReadyAt = state.time + config.combat.meleeCooldown;
                individual.lastCombatAt = state.time;
                state.events.Add(new WorldEvent { kind = "melee", individual = individual.individual, amount = config.combat.meleeEnergy });
                var target = FirstHit(state, individual.species, Position(individual),
                    Position(individual) + AimDirection(individual, action) * config.combat.meleeRange, config.combat.hitRadius, out _);
                if (target != null) Damage(config, state, individual, target, config.combat.meleeDamage, "melee_hit", state.time + config.stepSeconds);
            }
            foreach (var projectile in state.projectiles)
            {
                Vector2 start = new Vector2(projectile.x, projectile.y);
                float distance = Mathf.Min(projectile.remaining, config.combat.iceSpeed * config.stepSeconds);
                Vector2 end = start + new Vector2(projectile.directionX, projectile.directionY) * distance;
                while (true)
                {
                    var target = FirstHit(state, projectile.species, start, end, config.combat.hitRadius, out _, projectile.hitIndividuals);
                    if (target == null) break;
                    // 射手死亡後，已發射投射物仍保留來源及效果。
                    var source = state.individuals.Find(value => value.individual == projectile.owner)
                        ?? new IndividualState { individual = projectile.owner, species = projectile.species };
                    string kind = projectile.skill == WorldSkill.PiercingIceArrow ? "piercing_hit"
                        : projectile.skill == WorldSkill.FrostbindArrow ? "frostbind_hit" : "ice_hit";
                    Damage(config, state, source, target, projectile.damage, kind, state.time + config.stepSeconds);
                    projectile.hitIndividuals.Add(target.individual);
                    if (projectile.skill == WorldSkill.FrostbindArrow && target.health > 0)
                        ControlEffects.Slow(state, target, state.time + config.stepSeconds, config.combat.slowDuration);
                    if (projectile.skill != WorldSkill.PiercingIceArrow) { projectile.remaining = 0; break; }
                }
                projectile.remaining = Mathf.Max(0, projectile.remaining - distance);
                projectile.x = end.x;
                projectile.y = end.y;
                if (Mathf.Abs(end.x) > config.halfExtent || Mathf.Abs(end.y) > config.halfExtent) projectile.remaining = 0;
            }
            state.projectiles.RemoveAll(value => value.remaining <= 0);
            var cultivatedFoods = new HashSet<int>();
            foreach (var individual in state.individuals)
            {
                if (individual.health <= 0 || state.time >= individual.cultivateUntil) continue;
                var food = state.foods.Find(value => value.food == individual.cultivatingFood);
                if (food == null || !food.mature || food.phase == FoodPhase.Resting || cultivatedFoods.Contains(food.food) || Vector2.Distance(Position(individual), new Vector2(food.x, food.y)) > config.combat.cultivateRange) continue;
                float grown = Mathf.Min(food.capacity - food.amount, config.combat.cultivateRate *
                    Mathf.Min(config.stepSeconds, individual.cultivateUntil - state.time));
                if (grown <= 0) continue;
                cultivatedFoods.Add(food.food);
                food.amount += grown;
                state.events.Add(new WorldEvent { kind = "cultivated", individual = individual.individual, food = food.food, amount = grown });
                float threshold = food.capacity * (food.growthType == FoodGrowthType.Large
                    ? config.largeFood.recoveryFraction : config.smallFood.recoveryFraction);
                if (food.phase == FoodPhase.Recovering && food.amount >= threshold)
                {
                    food.phase = FoodPhase.Available;
                    state.events.Add(new WorldEvent { kind = "food_available", food = food.food, x = food.x, y = food.y });
                }
            }
        }

        // 線段掃掠回傳第一個異種碰撞，避免快速衝鋒穿透；同種不受傷也不攔截。
        public static IndividualState FirstHit(WorldState state, string species, Vector2 start, Vector2 end, float radius, out float fraction, System.Collections.Generic.ICollection<int> excluded = null)
        {
            IndividualState result = null;
            fraction = float.PositiveInfinity;
            Vector2 delta = end - start;
            foreach (var candidate in state.individuals)
            {
                if (candidate.species == species || candidate.health <= 0 || (excluded != null && excluded.Contains(candidate.individual))) continue;
                Vector2 offset = start - Position(candidate);
                float hit;
                if (offset.sqrMagnitude <= radius * radius) hit = 0;
                else
                {
                    float a = delta.sqrMagnitude;
                    float b = Vector2.Dot(offset, delta);
                    float discriminant = b * b - a * (offset.sqrMagnitude - radius * radius);
                    if (a == 0 || discriminant < 0) continue;
                    hit = (-b - Mathf.Sqrt(discriminant)) / a;
                    if (hit < 0 || hit > 1) continue;
                }
                if (hit >= fraction) continue;
                fraction = hit;
                result = candidate;
            }
            return result;
        }

        public static void Damage(WorldConfig config, WorldState state, IndividualState source, IndividualState target, float damage, string kind, float time)
        {
            if (source.species == target.species || target.health <= 0) return;
            float effective = time < target.shieldUntil ? damage * config.combat.shieldMultiplier : damage;
            if (effective < damage)
                state.events.Add(new WorldEvent { kind = "shield_blocked", individual = source.individual, target = target.individual,
                    amount = damage - effective, rawAmount = damage, x = target.x, y = target.y });
            float actual = Mathf.Min(target.health, effective);
            target.health -= actual;
            source.lastCombatAt = target.lastCombatAt = time;
            target.lastDamageCause = "combat";
            target.hitUntil = time + 0.2f;
            state.events.Add(new WorldEvent { kind = kind, individual = source.individual,
                target = target.individual, amount = actual, rawAmount = damage, x = target.x, y = target.y });
        }

        public static void Recover(WorldConfig config, WorldState state, IndividualState individual, float time)
        {
            individual.rootRemaining = Mathf.Max(0, individual.rootUntil - time);
            individual.attackCooldown = Mathf.Max(0, individual.attackReadyAt - time);
            for (int slot = 0; slot < individual.skillCooldowns.Length; slot++)
                individual.skillCooldowns[slot] = Mathf.Max(0, individual.skillReadyTimes[slot] - time);
            individual.skillCooldown = Mathf.Max(individual.skillCooldowns[0], individual.skillCooldowns[1]);
            individual.windupRemaining = Mathf.Max(0, individual.windupUntil - time);
            individual.slowRemaining = Mathf.Max(0, individual.slowUntil - time);
            individual.shieldRemaining = Mathf.Max(0, individual.shieldUntil - time);
            individual.cultivateRemaining = Mathf.Max(0, individual.cultivateUntil - time);
            if (individual.health <= 0 || individual.energy < config.combat.recoveryEnergy ||
                time - individual.lastCombatAt < config.combat.recoveryDelay) return;
            float recovered = Mathf.Min(config.combat.recoveryRate * config.stepSeconds, MaxHealth(individual.species) - individual.health);
            if (recovered <= 0) return;
            individual.health += recovered;
            state.events.Add(new WorldEvent { kind = "healed", individual = individual.individual, amount = recovered });
        }
    }
}
