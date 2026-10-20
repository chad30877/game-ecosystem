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
        }

        private void OnDestroy() => Destroy(GetComponent<Renderer>().sharedMaterial);
    }
}
