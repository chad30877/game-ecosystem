using System.Linq;
using UnityEngine;

namespace Ecosystem
{
    public static class ForagingController
    {
        public static WorldAction Decide(World world, IndividualState individual)
        {
            var config = world.Config;
            var position = CombatRules.Position(individual);
            var foods = individual.visibleFoods.Concat(individual.foodMemory.Where(value => !value.visible))
                .Where(value => value.mature && value.phase == FoodPhase.Available && value.amount > 0.01f).ToList();
            var target = foods.OrderBy(value => FoodScore(world, individual, value)).FirstOrDefault();
            var previous = foods.Find(value => SamePoint(value.x, value.y, individual.foodTargetX, individual.foodTargetY));
            if (individual.hasFoodTarget && previous != null && target != null &&
                FoodScore(world, individual, previous) <= FoodScore(world, individual, target) + config.aiTargetMargin) target = previous;
            var competitors = individual.perception.Where(value => value.visible && value.feeding && value.species != individual.species).ToList();
            EntityObservation enemy = target == null ? null : competitors.Where(value => SamePoint(value.sourceX, value.sourceY, target.x, target.y))
                .OrderBy(value => Vector2.Distance(position, new Vector2(value.x, value.y))).FirstOrDefault();
            float health = individual.health / CombatRules.MaxHealth(individual.species);
            bool wasContesting = individual.decisionReason == "contest" || individual.decisionReason == "contest_approach";
            bool costly = wasContesting && individual.contestHealth - health >= config.aiCombatLoss;
            bool weak = health < config.aiWeakHealth || (individual.species == "Flame" && individual.stamina < 2) ||
                (individual.species == "Frost" && individual.mana < config.combat.iceMana);
            var alternative = target == null ? null : foods.Where(value => !SamePoint(value.x, value.y, target.x, target.y) &&
                !competitors.Any(other => SamePoint(value.x, value.y, other.sourceX, other.sourceY)))
                .OrderBy(value => FoodScore(world, individual, value)).FirstOrDefault();
            string reason = target == null ? "explore" : "forage";
            if (enemy != null && (weak || costly || health < config.aiRetreatHealth) && alternative != null)
            {
                target = alternative; enemy = null; reason = "switch_food";
            }
            else if (enemy != null && (health < config.aiRetreatHealth || costly))
            {
                individual.retreatUntil = world.State.time + 2f;
                Vector2 away = (position - new Vector2(enemy.x, enemy.y)).normalized;
                individual.retreatX = away.x; individual.retreatY = away.y;
            }
            bool lostTarget = individual.hasFoodTarget && target == null;
            individual.hasFoodTarget = target != null;
            if (target != null) { individual.foodTargetX = target.x; individual.foodTargetY = target.y; }
            if (individual.stamina >= config.aiRunStart) individual.runPreferred = true;
            if (individual.stamina <= config.aiRunStop) individual.runPreferred = false;
            if (world.State.time < individual.retreatUntil)
            {
                individual.decisionReason = "retreat";
                var away = new Vector2(individual.retreatX, individual.retreatY);
                return new WorldAction(away, true, turn: Turn(individual, away), run: individual.runPreferred);
            }
            if (target == null)
            {
                individual.decisionReason = lostTarget ? "food_lost" : "explore";
                individual.contestHealth = health;
                var exploration = Explore(world, individual, position);
                Vector2 aim = position;
                int support = ChooseSkill(world, individual, null, ref aim);
                return new WorldAction(exploration.movement, true, aim, turn: exploration.turn, skillSlot: support,
                    run: individual.runPreferred && individual.satiety < config.aiUrgentSatiety);
            }
            Vector2 delta = new Vector2(target.x, target.y) - position;
            bool canEat = individual.feeding && SamePoint(individual.fedSourceX, individual.fedSourceY, target.x, target.y) &&
                individual.fedAmount >= config.forageRate * config.stepSeconds * 0.5f;
            if (canEat || individual.satiety >= 80f) enemy = null;
            Vector2 movement = delta.magnitude <= config.forageRadius * 0.9f ? Vector2.zero : delta.normalized;
            Vector2 skillAim = enemy == null ? position : new Vector2(enemy.x, enemy.y);
            int skillSlot = ChooseSkill(world, individual, enemy, ref skillAim);
            bool attack = false;
            if (enemy != null)
            {
                if (!wasContesting) individual.contestHealth = health;
                bool capable = !weak;
                attack = capable && individual.species != "Moss";
                reason = capable ? "contest" : "contest_approach";
            }
            else
            {
                individual.contestHealth = health;
                if (movement == Vector2.zero) reason = individual.stamina <= config.aiRunStop ? "rest" : "feed";
                else if (reason != "switch_food" && individual.stamina <= config.aiRunStop) reason = "walk_recover";
            }
            individual.decisionReason = reason;
            return new WorldAction(movement, true, skillAim, attack, turn: Turn(individual, delta), skillSlot: skillSlot,
                run: individual.runPreferred && delta.magnitude > config.forageRadius * 2 &&
                    (enemy != null || individual.satiety < config.aiUrgentSatiety));
        }

        private static bool SamePoint(float x, float y, float otherX, float otherY) => Mathf.Abs(x - otherX) < 0.001f && Mathf.Abs(y - otherY) < 0.001f;

        private static float FoodScore(World world, IndividualState individual, FoodMemory food) =>
            Vector2.Distance(CombatRules.Position(individual), new Vector2(food.x, food.y)) +
            (food.visible ? 0 : Mathf.Min((world.State.time - food.lastSeenAt) / world.Config.foodMemorySeconds, 2f) * world.Config.observationRadius);

        private static int ChooseSkill(World world, IndividualState individual, EntityObservation enemy, ref Vector2 aim)
        {
            var c = world.Config.combat;
            var position = CombatRules.Position(individual);
            float distance = enemy == null ? float.PositiveInfinity : Vector2.Distance(position, new Vector2(enemy.x, enemy.y));
            for (int offset = 0; offset < 2; offset++)
            {
                int slot = (world.State.step / 60 + individual.individual + offset) % 2;
                if (individual.skillCooldowns[slot] > 0 || individual.mana < SkillRules.ManaCost(c, individual.loadout[slot])) continue;
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
                        if (individual.satiety >= 30)
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
