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
            return new WorldAction(baseAction.movement, baseAction.forage, aim, turn: baseAction.turn, skillSlot: skillSlot);
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
                        if (individual.visibleFoods.Exists(food => food.mature && food.phase != FoodPhase.Resting && food.amount < food.capacity &&
                            Vector2.Distance(position, new Vector2(food.x, food.y)) <= c.cultivateRange)) return slot;
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
            FoodMemory target = null;
            float best = float.PositiveInfinity;
            foreach (var food in individual.visibleFoods.Concat(individual.foodMemory.Where(value => !value.visible)))
            {
                float distance = Vector2.Distance(position, new Vector2(food.x, food.y));
                float age = world.State.time - food.lastSeenAt;
                float score = distance + (food.visible ? 0 : Mathf.Min(age / world.Config.foodMemorySeconds, 2f) * world.Config.observationRadius);
                if (food.mature && food.phase == FoodPhase.Available && food.amount > 0.01f && score < best)
                {
                    best = score;
                    target = food;
                }
            }
            if (target != null)
            {
                Vector2 delta = new Vector2(target.x, target.y) - position;
                float distance = delta.magnitude;
                Vector2 movement = distance <= world.Config.forageRadius * 0.9f ? Vector2.zero : delta.normalized;
                return new WorldAction(movement, true, turn: target.visible && movement == Vector2.zero ? 0 : Turn(individual, delta));
            }
            return Explore(world, individual, position);
        }

        private static WorldAction Explore(World world, IndividualState individual, Vector2 position)
        {
            var config = world.Config;
            int columns = Mathf.CeilToInt(config.halfExtent * 2 / config.explorationCellSize);
            bool targetKnown = individual.areaMemory.Exists(value => value.cellX == individual.explorationTargetX &&
                value.cellY == individual.explorationTargetY && world.State.time - value.lastSeenAt < config.areaMemorySeconds);
            bool targetReached = individual.explorationTargetX >= 0 &&
                Vector2.Distance(position, world.ExplorationCellCenter(individual.explorationTargetX, individual.explorationTargetY)) < config.forageRadius;
            if (individual.explorationTargetX < 0 || targetKnown || targetReached)
            {
                float best = float.PositiveInfinity;
                individual.explorationTargetX = individual.explorationTargetY = -1;
                for (int x = 0; x < columns; x++)
                    for (int y = 0; y < columns; y++)
                    {
                        Vector2 point = world.ExplorationCellCenter(x, y);
                        Vector2 delta = point - position;
                        float distance = delta.magnitude;
                        if (distance < config.forageRadius) continue;
                        var known = individual.areaMemory.Find(value => value.cellX == x && value.cellY == y);
                        if (known != null && world.State.time - known.lastSeenAt < config.areaMemorySeconds) continue;
                        float score = distance + (known == null ? 0 : config.observationRadius);
                        score += Mathf.Abs(Mathf.DeltaAngle(individual.facing, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg)) / 180f;
                        if (score >= best) continue;
                        best = score;
                        individual.explorationTargetX = x;
                        individual.explorationTargetY = y;
                    }
            }
            if (individual.explorationTargetX < 0) return new WorldAction(Vector2.zero);
            Vector2 direction = world.ExplorationCellCenter(individual.explorationTargetX, individual.explorationTargetY) - position;
            return new WorldAction(direction.normalized, turn: Turn(individual, direction));
        }
    }
}
