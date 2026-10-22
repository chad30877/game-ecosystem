using System.Linq;
using UnityEngine;

namespace Ecosystem
{
    public static class ForagingController
    {
        public static WorldAction Decide(World world, IndividualState individual)
        {
            var baseAction = Forage(world, individual);
            var enemy = individual.perception.Where(value => value.visible && value.species != individual.species)
                .OrderBy(value => Vector2.Distance(CombatRules.Position(individual), new Vector2(value.x, value.y))).FirstOrDefault();
            Vector2 aim = enemy == null ? CombatRules.Position(individual) : new Vector2(enemy.x, enemy.y);
            int skillSlot = ChooseSkill(world, individual, enemy, ref aim);
            if (enemy != null && individual.energy >= 20 && individual.species != "Moss")
            {
                var delta = new Vector2(enemy.x, enemy.y) - CombatRules.Position(individual);
                float range = delta.magnitude;
                var movement = individual.species == "Frost" ? (range < 3.5f ? -delta.normalized : Vector2.zero)
                    : range < world.Config.combat.meleeRange ? Vector2.zero : delta.normalized;
                return new WorldAction(movement, true, aim, true, turn: Turn(individual, delta), skillSlot: skillSlot);
            }
            return new WorldAction(baseAction.movement, baseAction.forage, aim, turn: Turn(individual, baseAction.movement), skillSlot: skillSlot);
        }

        private static int ChooseSkill(World world, IndividualState individual, EntityObservation enemy, ref Vector2 aim)
        {
            var c = world.Config.combat;
            var position = CombatRules.Position(individual);
            float distance = enemy == null ? float.PositiveInfinity : Vector2.Distance(position, new Vector2(enemy.x, enemy.y));
            for (int offset = 0; offset < 2; offset++)
            {
                int slot = (world.State.step / 60 + individual.individual + offset) % 2;
                if (individual.skillCooldowns[slot] > 0) continue;
                switch (individual.loadout[slot])
                {
                    case WorldSkill.Charge: if (distance > 1.5f && distance <= c.chargeDistance) return slot; break;
                    case WorldSkill.HotStrike: if (distance <= c.hotStrikeRange) return slot; break;
                    case WorldSkill.Shield: if (distance <= c.roarRange) return slot; break;
                    case WorldSkill.IntimidatingRoar: if (distance <= c.roarRange) return slot; break;
                    case WorldSkill.FrostbindArrow: if (distance <= c.iceRange) return slot; break;
                    case WorldSkill.PiercingIceArrow: if (distance <= c.piercingRange) return slot; break;
                    case WorldSkill.ColdMistDash:
                        if (distance < 2.5f)
                        {
                            aim = position + (position - aim).normalized * c.dashDistance;
                            return slot;
                        }
                        break;
                    case WorldSkill.FrostTrap: if (distance <= c.trapRange) return slot; break;
                    case WorldSkill.EntanglingRoots: if (distance <= c.rootsRange) return slot; break;
                    case WorldSkill.SporeSmoke: if (distance <= c.smokeRadius) return slot; break;
                    case WorldSkill.Cultivate:
                        if (world.State.foods.Exists(food => food.mature && food.phase != FoodPhase.Resting && food.amount < food.capacity &&
                            Vector2.Distance(position, new Vector2(food.x, food.y)) <= Mathf.Min(c.cultivateRange, world.Config.observationRadius))) return slot;
                        break;
                    case WorldSkill.Sow:
                        if (individual.energy >= 30)
                        {
                            aim = position + new Vector2(Mathf.Cos(individual.facing * Mathf.Deg2Rad), Mathf.Sin(individual.facing * Mathf.Deg2Rad)) * (c.sowRange * 0.7f);
                            return slot;
                        }
                        break;
                }
            }
            return -1;
        }

        private static int Turn(IndividualState individual, Vector2 direction)
        {
            if (direction.sqrMagnitude == 0) return 0;
            float angle = Mathf.DeltaAngle(individual.facing, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            return Mathf.Abs(angle) < 4 ? 0 : angle > 0 ? 1 : -1;
        }

        private static WorldAction Forage(World world, IndividualState individual)
        {
            var position = new Vector2(individual.x, individual.y);
            FoodState target = null;
            float nearest = world.Config.observationRadius;
            foreach (var food in world.State.foods)
            {
                float distance = Vector2.Distance(position, new Vector2(food.x, food.y));
                if (food.mature && food.phase == FoodPhase.Available && food.amount > 0.01f && distance <= nearest)
                {
                    nearest = distance;
                    target = food;
                }
            }
            if (target != null)
                return new WorldAction(nearest <= world.Config.forageRadius * 0.9f ? Vector2.zero
                    : (new Vector2(target.x, target.y) - position).normalized, true);
            float angle = individual.individual * 2.39996f + (world.State.step / 180) * 1.7f;
            var movement = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            if (Mathf.Abs(individual.x) > world.Config.halfExtent - 1) movement.x = -Mathf.Sign(individual.x);
            if (Mathf.Abs(individual.y) > world.Config.halfExtent - 1) movement.y = -Mathf.Sign(individual.y);
            return new WorldAction(movement);
        }
    }
}
