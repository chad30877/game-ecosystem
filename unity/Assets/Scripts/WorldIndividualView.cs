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
        }

        private void OnDestroy()
        {
            Destroy(GetComponent<Renderer>().sharedMaterial);
            if (aura != null) Destroy(aura.sharedMaterial);
        }
    }
}
