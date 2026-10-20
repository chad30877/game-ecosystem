using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Ecosystem
{
    public sealed class WorldAgent : Agent
    {
        public World World { private get; set; }
        public IndividualState Individual { get; set; }
        public Vector2 Action { get; private set; }
        private TrailRenderer trail;
        private LineRenderer aimView;
        private LineRenderer aura;

        public override void CollectObservations(VectorSensor sensor) => sensor.AddObservation(World.Observe(Individual));

        public override void OnActionReceived(ActionBuffers actions) =>
            Action = new Vector2(actions.ContinuousActions[0], actions.ContinuousActions[1]);

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var actions = actionsOut.ContinuousActions;
            actions[0] = (Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
            actions[1] = (Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
        }

        public void UpdateView()
        {
            transform.position = new Vector3(Individual.x, Individual.y, 0);
            float size = World.Config.foraging ? (Individual.age < World.Config.maturityAge ? 0.4f : 0.65f) : 0.4f;
            transform.localScale = Vector3.one * size;
            var renderer = GetComponent<Renderer>();
            var color = Individual.species == "Flame" ? new Color(1f, 0.35f, 0.2f)
                : Individual.species == "Frost" ? new Color(0.35f, 0.7f, 1f) : new Color(0.35f, 0.9f, 0.4f);
            renderer.sharedMaterial.color = World.State.time < Individual.hitUntil ? Color.white
                : Individual.windupRemaining > 0 ? Color.cyan : Individual.slowRemaining > 0 ? Color.Lerp(color, Color.blue, 0.5f) : color;
            if (trail == null)
            {
                trail = gameObject.AddComponent<TrailRenderer>();
                trail.sharedMaterial = renderer.sharedMaterial;
                trail.time = 0.25f;
                trail.startWidth = 0.3f;
                trail.endWidth = 0;
            }
            trail.emitting = Individual.chargeRemaining > 0;
            if (aimView == null)
            {
                aimView = new GameObject("Aim").AddComponent<LineRenderer>();
                aimView.transform.SetParent(transform);
                aimView.sharedMaterial = renderer.sharedMaterial;
                aimView.startWidth = aimView.endWidth = 0.08f;
                aimView.positionCount = 2;
            }
            aimView.enabled = Individual.windupRemaining > 0;
            aimView.SetPosition(0, transform.position);
            aimView.SetPosition(1, transform.position + new Vector3(Individual.aimX, Individual.aimY, 0) * 1.3f);
            if (aura == null)
            {
                aura = new GameObject("Aura").AddComponent<LineRenderer>();
                aura.transform.SetParent(transform);
                aura.sharedMaterial = new Material(Resources.Load<Material>("WorldMaterial"));
                aura.startWidth = aura.endWidth = 0.08f;
                aura.positionCount = 33;
            }
            aura.enabled = Individual.shieldRemaining > 0 || Individual.cultivateRemaining > 0;
            aura.sharedMaterial.color = Individual.shieldRemaining > 0 ? Color.cyan : Color.yellow;
            float radius = Individual.shieldRemaining > 0 ? 0.65f : 0.9f;
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
