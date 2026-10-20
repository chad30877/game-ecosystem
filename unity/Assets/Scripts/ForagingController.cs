using UnityEngine;

namespace Ecosystem
{
    public static class ForagingController
    {
        public static WorldAction Decide(World world, IndividualState individual)
        {
            if (individual.species == "Flame")
            {
                IndividualState enemy = null;
                float range = world.Config.observationRadius;
                foreach (var other in world.State.individuals)
                {
                    float distance = Vector2.Distance(CombatRules.Position(individual), CombatRules.Position(other));
                    if (other.species == individual.species || distance > range) continue;
                    enemy = other;
                    range = distance;
                }
                if (enemy != null && individual.energy >= 20)
                {
                    var direction = (CombatRules.Position(enemy) - CombatRules.Position(individual)).normalized;
                    return new WorldAction(range < world.Config.combat.meleeRange ? Vector2.zero : direction,
                        true, direction, true, range > 1.5f && range <= world.Config.combat.chargeDistance ? WorldSkill.Charge : WorldSkill.None);
                }
            }
            var position = new Vector2(individual.x, individual.y);
            FoodState target = null;
            float nearest = world.Config.observationRadius;
            foreach (var food in world.State.foods)
            {
                float distance = Vector2.Distance(position, new Vector2(food.x, food.y));
                if (food.amount > 0.01f && distance <= nearest)
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
