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
            int skillSlot = -1;
            WorldSkill requested = individual.species == "Moss" ? WorldSkill.Cultivate
                : enemy != null ? WorldSkill.Charge : WorldSkill.None;
            for (int slot = 0; slot < individual.loadout.Length; slot++)
                if (individual.loadout[slot] == requested) skillSlot = slot;
            if (enemy != null && individual.energy >= 20 && individual.species != "Moss")
            {
                var delta = new Vector2(enemy.x, enemy.y) - CombatRules.Position(individual);
                float range = delta.magnitude;
                var movement = individual.species == "Frost" ? (range < 3.5f ? -delta.normalized : Vector2.zero)
                    : range < world.Config.combat.meleeRange ? Vector2.zero : delta.normalized;
                int turn = Turn(individual, delta);
                return new WorldAction(movement, true, delta.normalized, true,
                    turn: turn, skillSlot: individual.species == "Flame" && range > 1.5f ? skillSlot : -1);
            }
            return new WorldAction(baseAction.movement, baseAction.forage, turn: Turn(individual, baseAction.movement), skillSlot: skillSlot);
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
