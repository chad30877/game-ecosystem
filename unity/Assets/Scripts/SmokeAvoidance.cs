using System.Linq;
using UnityEngine;

namespace Ecosystem
{
    public static class SmokeAvoidance
    {
        public static WorldAction Adjust(World world, IndividualState individual, WorldAction action)
        {
            var harmful = individual.smokeKnowledge.Where(value => value.harmful).ToArray();
            if (harmful.Length == 0) return action;
            Vector2 position = CombatRules.Position(individual);
            float horizon = Mathf.Max(0.8f, world.Config.speed * world.Config.runMultiplier * world.Config.stepSeconds);
            bool inside = harmful.Any(value => Vector2.Distance(position, new Vector2(value.x, value.y)) <= value.radius);
            bool emergency = individual.decisionReason == "retreat" || individual.decisionReason == "protection_retreat";
            bool Safe(Vector2 direction, float distance)
            {
                Vector2 end = position + direction * distance;
                if (Mathf.Abs(end.x) > world.Config.halfExtent || Mathf.Abs(end.y) > world.Config.halfExtent) return false;
                foreach (var smoke in harmful)
                {
                    Vector2 delta = new Vector2(smoke.x, smoke.y) - position;
                    if (inside && delta.sqrMagnitude <= smoke.radius * smoke.radius &&
                        Vector2.SqrMagnitude(new Vector2(smoke.x, smoke.y) - end) > delta.sqrMagnitude + 0.0001f) continue;
                    Vector2 nearest = direction * Mathf.Clamp(Vector2.Dot(delta, direction), 0, distance);
                    if ((delta - nearest).sqrMagnitude <= (smoke.radius + 0.12f) * (smoke.radius + 0.12f)) return false;
                }
                return true;
            }
            WorldSkill skill = action.skillSlot >= 0 ? individual.loadout[action.skillSlot] : action.skill;
            float mobility = skill == WorldSkill.Charge ? world.Config.combat.chargeDistance :
                skill == WorldSkill.ColdMistDash ? world.Config.combat.dashDistance : 0;
            bool unsafeSkill = mobility > 0 && !Safe((action.aim - position).normalized, mobility * CombatRules.Growth(world.Config, individual));
            if (!inside && (action.movement == Vector2.zero || Safe(action.movement.normalized, horizon)) && !unsafeSkill) return action;
            Vector2 wanted = action.movement.sqrMagnitude > 0 ? action.movement.normalized :
                new Vector2(Mathf.Cos(individual.facing * Mathf.Deg2Rad), Mathf.Sin(individual.facing * Mathf.Deg2Rad));
            Vector2 best = Vector2.zero;
            float bestScore = float.NegativeInfinity;
            for (int index = -1; index < 32; index++)
            {
                float angle = index * Mathf.PI / 16;
                Vector2 direction = index < 0 ? wanted : new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 end = position + direction * horizon;
                if (Mathf.Abs(end.x) > world.Config.halfExtent || Mathf.Abs(end.y) > world.Config.halfExtent) continue;
                float alignment = Vector2.Dot(direction, wanted);
                if (!inside || emergency)
                {
                    if (!Safe(direction, horizon) || (emergency && alignment < 0.05f)) continue;
                }
                float penetration = harmful.Sum(value => Mathf.Max(0, value.radius + 0.12f - Vector2.Distance(end, new Vector2(value.x, value.y))));
                float score = inside && !emergency ? -penetration * 100 + alignment : alignment;
                if (score <= bestScore) continue;
                bestScore = score; best = direction;
            }
            // 緊急撤退仍以脫離眼前威脅為先；煙幕不能成為絕對禁區。
            if (emergency && best == Vector2.zero)
            {
                individual.decisionReason = "smoke_cross";
                return new WorldAction(wanted, turn: action.turn, run: action.run);
            }
            individual.decisionReason = inside ? "smoke_exit" : "smoke_avoid";
            int turn = best == Vector2.zero ? action.turn : Mathf.DeltaAngle(individual.facing, Mathf.Atan2(best.y, best.x) * Mathf.Rad2Deg) >= 0 ? 1 : -1;
            return new WorldAction(best, !inside && action.forage, action.aim, !inside && action.attack,
                !inside && !unsafeSkill ? action.skill : WorldSkill.None, turn,
                !inside && !unsafeSkill ? action.skillSlot : -1, action.run, !inside ? action.feedChild : 0);
        }
    }
}
