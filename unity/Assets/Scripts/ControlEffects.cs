using UnityEngine;

namespace Ecosystem
{
    public static class ControlEffects
    {
        public static void PrepareMovement(WorldConfig config, WorldState state, System.Random random)
        {
            foreach (var individual in state.individuals)
            {
                individual.movementBiases.RemoveAll(value => value.until <= state.time);
                if (state.time < individual.rootUntil) continue;
                if (individual.trapSlowPending)
                {
                    Slow(state, individual, state.time, individual.trapSlowDuration);
                    individual.trapSlowPending = false;
                }
                foreach (var bias in individual.movementBiases)
                {
                    if (bias.skill != WorldSkill.SporeSmoke) continue;
                    if (state.time >= bias.nextDirectionAt)
                    {
                        bias.targetAngle = (float)random.NextDouble() * 360f;
                        bias.nextDirectionAt = state.time + config.combat.smokeDirectionInterval;
                    }
                    bias.angle = Mathf.MoveTowardsAngle(bias.angle, bias.targetAngle, config.combat.smokeTurnSpeed * config.stepSeconds);
                }
            }
        }

        public static void Slow(WorldState state, IndividualState target, float time, float duration)
        {
            target.slowUntil = Mathf.Max(target.slowUntil, time + duration);
            state.events.Add(new WorldEvent { kind = "slowed", individual = target.individual, amount = duration });
        }

        public static void Root(WorldState state, IndividualState target, float time, float duration)
        {
            target.rootUntil = Mathf.Max(target.rootUntil, time + duration);
            state.events.Add(new WorldEvent { kind = "rooted", individual = target.individual, amount = duration });
        }

        public static void Resolve(WorldConfig config, WorldState state, System.Random random)
        {
            float time = state.time + config.stepSeconds;
            foreach (var area in state.areas)
            {
                if (area.until <= state.time) continue;
                if (area.skill == WorldSkill.FrostTrap)
                {
                    var target = state.individuals.Find(value => value.health > 0 && value.species != area.species && Inside(area, value));
                    if (target == null) continue;
                    Root(state, target, time, config.combat.trapRootDuration * area.growth);
                    target.trapSlowPending = true;
                    target.trapSlowDuration = config.combat.trapSlowDuration * area.growth;
                    area.until = state.time;
                    state.events.Add(new WorldEvent { kind = "trap_triggered", individual = area.owner, target = target.individual, x = area.x, y = area.y });
                }
            }
            foreach (var target in state.individuals)
            {
                if (target.health <= 0) continue;
                if (time >= target.poisonUntil) target.poisonStacks = 0;
                AreaState smoke = state.areas.Find(value => value.skill == WorldSkill.SporeSmoke && value.until > time &&
                    value.species != target.species && Inside(value, target));
                if (smoke != null)
                {
                    RefreshSmokeBias(config, state, target, time, random);
                    if (time + 0.00001f >= target.nextPoisonExposureAt)
                    {
                        if (target.poisonStacks == 0) target.nextPoisonDamageAt = time + config.combat.poisonInterval;
                        target.poisonStacks = Mathf.Min(5, target.poisonStacks + 1);
                        target.poisonOwner = smoke.owner;
                        target.poisonUntil = time + config.combat.poisonDuration;
                        target.nextPoisonExposureAt = time + config.combat.poisonInterval;
                        state.events.Add(new WorldEvent { kind = "poison_applied", individual = smoke.owner, target = target.individual,
                            amount = target.poisonStacks, skill = WorldSkill.SporeSmoke });
                    }
                }
                while (target.poisonStacks > 0 && target.health > 0 && target.nextPoisonDamageAt <= time + 0.00001f)
                {
                    var source = state.individuals.Find(value => value.individual == target.poisonOwner)
                        ?? new IndividualState { individual = target.poisonOwner, species = "Moss" };
                    float damage = config.combat.poisonDamage * target.poisonStacks * (target.poisonStacks == 5 ? 2 : 1);
                    CombatRules.Damage(config, state, source, target, damage, "poison_damage", target.nextPoisonDamageAt);
                    target.nextPoisonDamageAt += config.combat.poisonInterval;
                }
            }
            state.areas.RemoveAll(value => value.until <= time);
        }

        private static bool Inside(AreaState area, IndividualState target) =>
            Vector2.Distance(CombatRules.Position(target), new Vector2(area.x, area.y)) <= area.radius;

        private static void RefreshSmokeBias(WorldConfig config, WorldState state, IndividualState target, float time, System.Random random)
        {
            if (time < target.rootUntil) return;
            var bias = target.movementBiases.Find(value => value.skill == WorldSkill.SporeSmoke);
            if (bias == null)
            {
                float angle = (float)random.NextDouble() * 360f;
                bias = new MovementBiasState { skill = WorldSkill.SporeSmoke, angle = angle, targetAngle = angle,
                    nextDirectionAt = time + config.combat.smokeDirectionInterval };
                target.movementBiases.Add(bias);
            }
            bias.until = time + config.combat.smokeBiasDuration;
        }

        public static void AddBias(WorldState state, IndividualState target, WorldSkill skill, Vector2 direction, float duration, float strength)
        {
            if (state.time < target.rootUntil) return;
            var bias = target.movementBiases.Find(value => value.skill == skill);
            if (bias == null)
            {
                bias = new MovementBiasState { skill = skill };
                target.movementBiases.Add(bias);
            }
            bias.until = state.time + duration;
            bias.strength = strength;
            bias.angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        }

        public static Vector2 MovementBias(WorldConfig config, WorldState state, IndividualState individual)
        {
            if (state.time < individual.rootUntil) return Vector2.zero;
            Vector2 sum = Vector2.zero;
            foreach (var bias in individual.movementBiases)
                if (bias.until > state.time)
                    sum += new Vector2(Mathf.Cos(bias.angle * Mathf.Deg2Rad), Mathf.Sin(bias.angle * Mathf.Deg2Rad)) *
                        (bias.skill == WorldSkill.SporeSmoke ? config.combat.smokeBias : config.combat.roarBias * bias.strength);
            return Vector2.ClampMagnitude(sum, config.combat.maxMovementBias);
        }
    }
}
