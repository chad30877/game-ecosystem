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
        public WorldAction Action { get; private set; }
        private BufferSensorComponent entitySensor;

        public override void CollectObservations(VectorSensor sensor)
        {
            sensor.AddObservation(World.Observe(Individual));
            if (entitySensor == null) entitySensor = GetComponent<BufferSensorComponent>();
            foreach (var entity in World.ObserveEntities(Individual)) entitySensor.AppendObservation(entity);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            var continuous = actions.ContinuousActions;
            var discrete = actions.DiscreteActions;
            Action = new WorldAction(new Vector2(continuous[0], continuous[1]), discrete[3] == 1,
                new Vector2(Individual.x, Individual.y) + new Vector2(continuous[2], continuous[3]) * (World.Config.halfExtent * 2f), discrete[2] == 1,
                turn: discrete[0] - 1, skillSlot: discrete[1] - 1, run: discrete[4] == 1);
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var actions = actionsOut.ContinuousActions;
            actions[0] = (Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
            actions[1] = (Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
            var camera = FindFirstObjectByType<Camera>();
            var aim = camera != null ? camera.ScreenToWorldPoint(Input.mousePosition) : Vector3.zero;
            actions[2] = Mathf.Clamp((aim.x - Individual.x) / (World.Config.halfExtent * 2f), -1f, 1f);
            actions[3] = Mathf.Clamp((aim.y - Individual.y) / (World.Config.halfExtent * 2f), -1f, 1f);
            var discrete = actionsOut.DiscreteActions;
            discrete[0] = 1 + (Input.GetKey(KeyCode.E) ? 1 : 0) - (Input.GetKey(KeyCode.Q) ? 1 : 0);
            discrete[1] = Input.GetKey(KeyCode.S) ? 1 : Input.GetKey(KeyCode.D) ? 2 : 0;
            discrete[2] = Input.GetKey(KeyCode.A) ? 1 : 0;
            discrete[3] = Input.GetKey(KeyCode.Space) ? 1 : 0;
            discrete[4] = Input.GetKey(KeyCode.LeftShift) ? 1 : 0;
        }

    }
}
