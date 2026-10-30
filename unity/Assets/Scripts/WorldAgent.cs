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
        private bool transferKeyHeld;
        private FoodAction manualTransferRole;

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
            if (continuous.Length != 4 || discrete.Length != 7)
                throw new System.ArgumentException("動作格式須為版本 2：四個連續值、七個離散分支，舊模型不能直接使用。");
            Vector2 aim = new Vector2(Individual.x, Individual.y) + new Vector2(continuous[2], continuous[3]) * (World.Config.halfExtent * 2f);
            int[] sizes = { 3, 3, 2, 2, 2, 7, 8 };
            for (int i = 0; i < sizes.Length; i++)
                if (discrete[i] < 0 || discrete[i] >= sizes[i]) throw new System.ArgumentException("離散動作超出版本 2 分支範圍。");
            Action = WorldInput.Decode(World, Individual, new Vector2(continuous[0], continuous[1]), aim,
                discrete[0] - 1, discrete[1] - 1, discrete[2] == 1, discrete[3] == 1, discrete[4] == 1,
                (FoodAction)discrete[5], discrete[6]);
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
            bool transfer = Input.GetKey(KeyCode.F);
            if (transfer && !transferKeyHeld) manualTransferRole = Individual.ownFruits.Count > 0 ? FoodAction.Deliver : FoodAction.Receive;
            transferKeyHeld = transfer;
            discrete[5] = (int)(Input.GetKey(KeyCode.X) ? FoodAction.Drop : Input.GetKey(KeyCode.G) ? FoodAction.Pickup :
                Input.GetKey(KeyCode.C) ? FoodAction.Eat : transfer ? manualTransferRole : FoodAction.None);
            discrete[6] = 0;
        }

    }
}
