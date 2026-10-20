using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ecosystem
{
    public enum WorldSkill { None, Charge, Cultivate, Shield }

    [Serializable]
    public sealed class CombatConfig
    {
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

        public void Validate()
        {
            foreach (float value in new[] { meleeRange, meleeDamage, meleeEnergy, meleeCooldown,
                chargeDistance, chargeSpeed, chargeDamage, chargeEnergy, chargeCooldown,
                hitRadius, recoveryEnergy, recoveryDelay, recoveryRate, iceRange, iceSpeed, iceDamage,
                iceEnergy, iceWindup, iceCooldown, slowDuration, slowMultiplier })
                if (!float.IsFinite(value) || value <= 0) throw new ArgumentException("戰鬥參數須為有限正數。");
            if (meleeEnergy > 100 || chargeEnergy > 100 || recoveryEnergy > 100 || iceEnergy > 100)
                throw new ArgumentException("戰鬥能量門檻不得大於 100。");
            if (slowDuration >= iceCooldown || slowMultiplier >= 1 || iceWindup >= iceCooldown)
                throw new ArgumentException("減速時間及前搖須短於冰箭冷卻，減速倍率須介於 0 與 1。");
        }
    }

    public static class CombatRules
    {
        public static float MaxHealth(string species) => species == "Flame" ? 120f : species == "Frost" ? 70f : 90f;
        public static Vector2 Position(IndividualState individual) => new Vector2(individual.x, individual.y);

        public static void BeginStep(WorldConfig config, WorldState state, IReadOnlyDictionary<int, WorldAction> actions)
        {
            foreach (var individual in state.individuals)
            {
                individual.chargeActiveThisStep = individual.chargeRemaining > 0;
                individual.windupActiveThisStep = individual.windupUntil > 0;
                var action = actions[individual.individual];
                if (individual.health <= 0) continue;
                if (individual.windupUntil > 0 && state.time >= individual.windupUntil)
                {
                    state.projectiles.Add(new ProjectileState { projectile = ++state.nextProjectile, owner = individual.individual,
                        species = individual.species, x = individual.x, y = individual.y,
                        directionX = individual.aimX, directionY = individual.aimY, remaining = config.combat.iceRange });
                    individual.windupUntil = 0;
                    state.events.Add(new WorldEvent { kind = "ice_fired", individual = individual.individual, x = individual.x, y = individual.y });
                }
                if (individual.species == "Frost" && action.attack && !individual.windupActiveThisStep && action.aim.sqrMagnitude > 0 &&
                    state.time >= individual.attackReadyAt && individual.energy >= config.combat.iceEnergy)
                {
                    individual.energy -= config.combat.iceEnergy;
                    individual.attackReadyAt = state.time + config.combat.iceCooldown;
                    individual.windupUntil = state.time + config.combat.iceWindup;
                    individual.windupActiveThisStep = true;
                    individual.aimX = action.aim.normalized.x;
                    individual.aimY = action.aim.normalized.y;
                    individual.lastCombatAt = state.time;
                    state.events.Add(new WorldEvent { kind = "ice_windup", individual = individual.individual, amount = config.combat.iceEnergy });
                }
                if (individual.species != "Flame" || individual.chargeRemaining > 0 ||
                    action.skill != WorldSkill.Charge || action.aim.sqrMagnitude == 0 ||
                    state.time < individual.skillReadyAt || individual.energy < config.combat.chargeEnergy) continue;
                individual.energy -= config.combat.chargeEnergy;
                individual.skillReadyAt = state.time + config.combat.chargeCooldown;
                individual.chargeRemaining = config.combat.chargeDistance;
                individual.aimX = action.aim.normalized.x;
                individual.aimY = action.aim.normalized.y;
                individual.chargeActiveThisStep = true;
                individual.lastCombatAt = state.time;
                state.events.Add(new WorldEvent { kind = "charge", individual = individual.individual,
                    amount = config.combat.chargeEnergy });
            }
        }

        public static Vector2 Move(WorldConfig config, WorldState state, IndividualState individual, WorldAction action)
        {
            float speed = individual.species == "Frost" ? 3.2f : individual.species == "Moss" ? 2.7f : 2.5f;
            bool charging = individual.chargeRemaining > 0;
            if (!charging && state.time < individual.slowUntil) speed *= config.combat.slowMultiplier;
            Vector2 delta = charging ? new Vector2(individual.aimX, individual.aimY) *
                Mathf.Min(individual.chargeRemaining, config.combat.chargeSpeed * config.stepSeconds)
                : individual.windupActiveThisStep ? Vector2.zero : Vector2.ClampMagnitude(action.movement, 1) * (speed * config.stepSeconds);
            Vector2 start = Position(individual);
            Vector2 end = start + delta;
            end = new Vector2(Mathf.Clamp(end.x, -config.halfExtent, config.halfExtent), Mathf.Clamp(end.y, -config.halfExtent, config.halfExtent));
            individual.boundary = end != start + delta;
            if (charging)
            {
                var target = FirstHit(state, individual.species, start, end, config.combat.hitRadius, out float fraction);
                if (target != null)
                {
                    end = Vector2.Lerp(start, end, fraction);
                    Damage(state, individual, target, config.combat.chargeDamage, "charge_hit", state.time + config.stepSeconds);
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
                if (individual.health <= 0 || individual.species != "Flame" || individual.chargeActiveThisStep || !action.attack ||
                    action.aim.sqrMagnitude == 0 || state.time < individual.attackReadyAt || individual.energy < config.combat.meleeEnergy) continue;
                individual.energy -= config.combat.meleeEnergy;
                individual.attackReadyAt = state.time + config.combat.meleeCooldown;
                individual.lastCombatAt = state.time;
                state.events.Add(new WorldEvent { kind = "melee", individual = individual.individual, amount = config.combat.meleeEnergy });
                var target = FirstHit(state, individual.species, Position(individual),
                    Position(individual) + action.aim.normalized * config.combat.meleeRange, config.combat.hitRadius, out _);
                if (target != null) Damage(state, individual, target, config.combat.meleeDamage, "melee_hit", state.time + config.stepSeconds);
            }
            foreach (var projectile in state.projectiles)
            {
                Vector2 start = new Vector2(projectile.x, projectile.y);
                float distance = Mathf.Min(projectile.remaining, config.combat.iceSpeed * config.stepSeconds);
                Vector2 end = start + new Vector2(projectile.directionX, projectile.directionY) * distance;
                var target = FirstHit(state, projectile.species, start, end, config.combat.hitRadius, out _);
                if (target != null)
                {
                    // 射手死亡不抹去已發射的冰箭，事件仍保留原始個體識別。
                    var source = state.individuals.Find(value => value.individual == projectile.owner)
                        ?? new IndividualState { individual = projectile.owner, species = projectile.species };
                    Damage(state, source, target, config.combat.iceDamage, "ice_hit", state.time + config.stepSeconds);
                    if (!target.chargeActiveThisStep && target.health > 0)
                    {
                        target.slowUntil = state.time + config.stepSeconds + config.combat.slowDuration;
                        state.events.Add(new WorldEvent { kind = "slowed", individual = target.individual, amount = config.combat.slowDuration });
                    }
                    else if (target.chargeActiveThisStep)
                        state.events.Add(new WorldEvent { kind = "slow_immune", individual = target.individual });
                    projectile.remaining = 0;
                }
                else
                {
                    projectile.remaining = Mathf.Max(0, projectile.remaining - distance);
                    if (Mathf.Abs(end.x) > config.halfExtent || Mathf.Abs(end.y) > config.halfExtent) projectile.remaining = 0;
                    projectile.x = end.x;
                    projectile.y = end.y;
                }
            }
            state.projectiles.RemoveAll(value => value.remaining <= 0);
        }

        // 線段掃掠回傳第一個異種碰撞，避免快速衝鋒穿透；同種不受傷也不攔截。
        public static IndividualState FirstHit(WorldState state, string species, Vector2 start, Vector2 end, float radius, out float fraction)
        {
            IndividualState result = null;
            fraction = float.PositiveInfinity;
            Vector2 delta = end - start;
            foreach (var candidate in state.individuals)
            {
                if (candidate.species == species || candidate.health <= 0) continue;
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

        public static void Damage(WorldState state, IndividualState source, IndividualState target, float damage, string kind, float time)
        {
            if (source.species == target.species || target.health <= 0) return;
            float actual = Mathf.Min(target.health, damage);
            target.health -= actual;
            source.lastCombatAt = target.lastCombatAt = time;
            target.lastDamageCause = "combat";
            target.hitUntil = time + 0.2f;
            state.events.Add(new WorldEvent { kind = kind, individual = source.individual,
                target = target.individual, amount = actual, rawAmount = damage, x = target.x, y = target.y });
        }

        public static void Recover(WorldConfig config, WorldState state, IndividualState individual, float time)
        {
            individual.attackCooldown = Mathf.Max(0, individual.attackReadyAt - time);
            individual.skillCooldown = Mathf.Max(0, individual.skillReadyAt - time);
            individual.windupRemaining = Mathf.Max(0, individual.windupUntil - time);
            individual.slowRemaining = Mathf.Max(0, individual.slowUntil - time);
            if (individual.health <= 0 || individual.energy < config.combat.recoveryEnergy ||
                time - individual.lastCombatAt < config.combat.recoveryDelay) return;
            float recovered = Mathf.Min(config.combat.recoveryRate * config.stepSeconds, MaxHealth(individual.species) - individual.health);
            if (recovered <= 0) return;
            individual.health += recovered;
            state.events.Add(new WorldEvent { kind = "healed", individual = individual.individual, amount = recovered });
        }
    }
}
