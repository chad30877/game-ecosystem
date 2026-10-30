using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Ecosystem
{
    // 對外輸入只用已提供給個體的觀察選目標；世界仍負責最後合法性檢查。
    public static class WorldInput
    {
        public static WorldAction Decode(World world, IndividualState actor, Vector2 movement, Vector2 aim,
            int turn, int skillSlot, bool attack, bool forage, bool run, FoodAction operation, int selector)
        {
            int fruit = 0, recipient = 0, productionSource = 0;
            var candidates = new List<FruitObservation>();
            if (operation == FoodAction.Gather)
            {
                var source = actor.visibleFoods.OrderBy(f => Vector2.Distance(aim, new Vector2(f.x, f.y))).ThenBy(f => f.food).FirstOrDefault();
                if (source != null) candidates.AddRange(source.fruits.Where(f => !f.claimed || f.claimedBy == actor.individual));
            }
            else if (operation == FoodAction.Pickup)
                candidates.AddRange(actor.groundFruitMemory.Where(f => f.visible && (!f.claimed || f.claimedBy == actor.individual))
                    .OrderBy(f => Vector2.Distance(aim, new Vector2(f.x, f.y))).ThenBy(f => f.fruit));
            else if (operation == FoodAction.Eat || operation == FoodAction.Drop || operation == FoodAction.Deliver)
                candidates.AddRange(actor.ownFruits);
            if (operation == FoodAction.Deliver)
                recipient = actor.perception.Where(e => e.visible && e.species == actor.species)
                    .OrderBy(e => Vector2.Distance(aim, new Vector2(e.x, e.y))).ThenBy(e => e.individual)
                    .Select(e => e.individual).FirstOrDefault();
            if (operation == FoodAction.Receive)
            {
                var offer = actor.foodInteractions.Where(o => o.visible && o.known && o.deliveryInviteTo == actor.individual &&
                    o.offeredFruit != null && actor.perception.Any(e => e.visible && e.individual == o.individual && e.species == actor.species))
                    .OrderBy(o => Vector2.Distance(aim, new Vector2(o.x, o.y))).ThenBy(o => o.individual).FirstOrDefault();
                if (offer != null) { recipient = offer.individual; fruit = offer.offeredFruit.fruit; }
            }
            else if (operation != FoodAction.None)
            {
                FruitObservation selected = null;
                if (selector > 0 && selector <= candidates.Count) selected = candidates[selector - 1];
                if (selector == 0)
                {
                    if (actor.foodOperation == operation) selected = candidates.Find(f => f.fruit == actor.operationFruit);
                    selected ??= operation == FoodAction.Deliver
                        ? candidates.OrderByDescending(f => f.EstimateRemaining(world.State.time)).ThenBy(f => f.fruit).FirstOrDefault()
                        : candidates.OrderBy(f => f.EstimateRemaining(world.State.time)).ThenBy(f => f.fruit).FirstOrDefault();
                }
                if (selected != null) fruit = selected.fruit;
            }
            bool explicitFood = operation != FoodAction.None;
            if (explicitFood && fruit == 0) operation = FoodAction.None;
            if (skillSlot >= 0 && actor.loadout[skillSlot] == WorldSkill.Cultivate)
                productionSource = actor.visibleFoods.Where(f => f.phase != FoodPhase.Waiting)
                    .OrderBy(f => Vector2.Distance(aim, new Vector2(f.x, f.y))).ThenBy(f => f.food).Select(f => f.food).FirstOrDefault();
            return new WorldAction(movement, forage && !explicitFood, aim, attack, turn: turn, skillSlot: skillSlot, run: run,
                foodAction: operation, fruit: fruit, recipient: recipient, productionSource: productionSource);
        }
    }
}
