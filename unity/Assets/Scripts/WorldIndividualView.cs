using UnityEngine;

namespace Ecosystem
{
    public sealed class WorldIndividualView : MonoBehaviour
    {
        public World World { private get; set; }
        public IndividualState Individual { get; set; }
        private TrailRenderer trail;
        private LineRenderer aimView;
        private LineRenderer aura;
        private LineRenderer foodGesture;

        public void UpdateView(bool clearTrail = false)
        {
            transform.position = new Vector3(Individual.x, Individual.y, 0);
            float size = World.Config.foraging ? (Individual.age < World.Config.maturityAge ? 0.4f : 0.65f) : 0.4f;
            transform.localScale = Vector3.one * size;
            var renderer = GetComponent<Renderer>();
            var color = Individual.species == "Flame" ? new Color(1f, 0.35f, 0.2f)
                : Individual.species == "Frost" ? new Color(0.35f, 0.7f, 1f) : new Color(0.35f, 0.9f, 0.4f);
            renderer.sharedMaterial.color = World.State.time < Individual.hitUntil ? Color.white
                : Individual.rootRemaining > 0 ? new Color(0.8f, 0.4f, 1f)
                : Individual.poisonStacks > 0 ? Color.Lerp(color, new Color(0.6f, 0.8f, 0.1f), 0.5f)
                : Individual.windupRemaining > 0 ? Color.cyan : Individual.slowRemaining > 0 ? Color.Lerp(color, Color.blue, 0.5f) : color;
            if (trail == null)
            {
                trail = gameObject.AddComponent<TrailRenderer>();
                trail.sharedMaterial = renderer.sharedMaterial;
                trail.time = 0.25f;
                trail.startWidth = 0.3f;
                trail.endWidth = 0;
            }
            if (clearTrail) trail.Clear();
            trail.emitting = Individual.chargeRemaining > 0 || Individual.dashRemaining > 0;
            if (aimView == null)
            {
                aimView = new GameObject("Aim").AddComponent<LineRenderer>();
                aimView.transform.SetParent(transform);
                aimView.sharedMaterial = renderer.sharedMaterial;
                aimView.startWidth = aimView.endWidth = 0.08f;
                aimView.positionCount = 2;
            }
            aimView.enabled = true;
            aimView.SetPosition(0, transform.position);
            aimView.SetPosition(1, transform.position + new Vector3(Mathf.Cos(Individual.facing * Mathf.Deg2Rad), Mathf.Sin(Individual.facing * Mathf.Deg2Rad), 0) * 1.3f);
            if (aura == null)
            {
                aura = new GameObject("Aura").AddComponent<LineRenderer>();
                aura.transform.SetParent(transform);
                aura.sharedMaterial = new Material(Resources.Load<Material>("WorldMaterial"));
                aura.startWidth = aura.endWidth = 0.08f;
                aura.positionCount = 33;
            }
            aura.enabled = Individual.shieldRemaining > 0 || Individual.cultivateRemaining > 0 || World.State.time < Individual.skillFlashUntil;
            aura.sharedMaterial.color = Individual.shieldRemaining > 0 ? Color.cyan : Individual.cultivateRemaining > 0 ? Color.yellow
                : Individual.species == "Flame" ? new Color(1, 0.5f, 0.1f) : Individual.species == "Moss" ? Color.green : Color.cyan;
            float radius = Individual.shieldRemaining > 0 ? 0.65f : Individual.lastSkill == WorldSkill.IntimidatingRoar ? World.Config.combat.roarRange
                : Individual.lastSkill == WorldSkill.EntanglingRoots ? World.Config.combat.rootsRange : 0.9f;
            for (int i = 0; i <= 32; i++)
            {
                float angle = i * Mathf.PI / 16;
                aura.SetPosition(i, transform.position + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, -0.1f));
            }
            if (!Application.isBatchMode) UpdateFoodGesture();
        }

        private void UpdateFoodGesture()
        {
            bool cultivating = Individual.cultivatingFood != 0 && Individual.cultivateRemaining > 0;
            bool active = World.State.fruitInformationAvailable && (Individual.foodOperation != FoodAction.None || cultivating || Individual.deliveryInviteTo != 0);
            if (foodGesture == null && !active) return;
            if (foodGesture == null)
            {
                foodGesture = new GameObject("FoodGesture").AddComponent<LineRenderer>();
                foodGesture.transform.SetParent(transform);
                foodGesture.sharedMaterial = new Material(Resources.Load<Material>("WorldMaterial"));
                foodGesture.startWidth = foodGesture.endWidth = .065f;
                foodGesture.positionCount = 4;
            }
            foodGesture.enabled = active;
            if (!active) return;
            Vector3 p = new Vector3(Individual.x, Individual.y, -.45f);
            Vector3 forward = new Vector3(Mathf.Cos(Individual.facing * Mathf.Deg2Rad), Mathf.Sin(Individual.facing * Mathf.Deg2Rad), 0);
            Vector3 side = new Vector3(-forward.y, forward.x, 0);
            float pulse = Mathf.Sin(World.State.time * 9) * .08f;
            Vector3[] points;
            Color tint = new Color(1, .8f, .25f);
            switch (Individual.foodOperation)
            {
                case FoodAction.Pickup:
                    points = new[] { p + side * .2f, p + forward * .25f - Vector3.up * (.5f + pulse), p - Vector3.up * .65f, p }; break;
                case FoodAction.Eat:
                    points = new[] { p + forward * .15f - side * .2f, p + forward * (.4f + pulse), p + forward * .15f + side * .2f, p + forward * .15f - side * .2f };
                    tint = new Color(1, .95f, .75f); break;
                case FoodAction.Drop:
                    points = new[] { p + forward * .3f, p + forward * .5f - Vector3.up * .3f, p + forward * .5f - Vector3.up * .65f, p + forward * .25f - Vector3.up * .65f };
                    tint = new Color(.8f, .55f, .3f); break;
                case FoodAction.Receive:
                    points = new[] { p + forward * .8f + side * .25f, p + forward * .3f, p + forward * .8f - side * .25f, p + forward * .3f };
                    tint = Color.cyan; break;
                case FoodAction.Deliver:
                    points = new[] { p + side * .2f, p + forward * (.9f + pulse), p + forward * .7f - side * .2f, p + forward * .3f };
                    tint = Color.cyan; break;
                default:
                    if (cultivating)
                    {
                        points = new[] { p + Vector3.up * .55f, p + Vector3.up * (1 + pulse) - side * .2f, p + Vector3.up * .8f, p + Vector3.up * (1 + pulse) + side * .2f };
                        tint = new Color(.65f, 1, .45f);
                    }
                    else if (Individual.deliveryInviteTo != 0)
                    {
                        points = new[] { p + side * .2f, p + forward * (.9f + pulse), p + forward * .7f - side * .2f, p + forward * .3f };
                        tint = Color.cyan;
                    }
                    else points = new[] { p + side * .2f, p + forward * (.65f + pulse) + side * .25f, p + forward * .9f, p + forward * .5f };
                    break;
            }
            foodGesture.sharedMaterial.color = tint;
            foodGesture.SetPositions(points);
        }

        private void OnDestroy()
        {
            Destroy(GetComponent<Renderer>().sharedMaterial);
            if (aura != null) Destroy(aura.sharedMaterial);
            if (foodGesture != null) Destroy(foodGesture.sharedMaterial);
        }
    }
}
