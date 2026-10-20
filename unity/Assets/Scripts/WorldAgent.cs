using System;
using System.IO;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Ecosystem
{
    public sealed class WorldAgent : Agent
    {
        private World world;
        private StreamWriter output;
        private long totalSteps;
        private int stepLimit;
        private string runId;

        public static string Argument(string name, string fallback)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartWorld()
        {
            Application.runInBackground = true;
            var cameraObject = new GameObject("Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.backgroundColor = new Color(0.07f, 0.1f, 0.14f);
            camera.clearFlags = CameraClearFlags.SolidColor;

            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Individual";
            Destroy(body.GetComponent<Collider>());
            body.transform.localScale = Vector3.one * 0.4f;
            body.GetComponent<Renderer>().material = Resources.Load<Material>("WorldMaterial");
            var behavior = body.AddComponent<BehaviorParameters>();
            behavior.BehaviorName = "Toolchain";
            behavior.BrainParameters.VectorObservationSize = 4;
            behavior.BrainParameters.ActionSpec = ActionSpec.MakeContinuous(2);
            behavior.BehaviorType = Argument("--control", "manual") == "manual"
                ? BehaviorType.HeuristicOnly : BehaviorType.Default;
            body.AddComponent<WorldAgent>();
            var requester = body.AddComponent<DecisionRequester>();
            requester.DecisionPeriod = 1;
        }

        public override void Initialize()
        {
            string configPath = Argument("--world-config", "");
            var config = configPath == "" ? new WorldConfig()
                : JsonUtility.FromJson<WorldConfig>(File.ReadAllText(configPath));
            int seed = int.Parse(Argument("--world-seed", "1"));
            world = new World(config, seed);
            if (Argument("--control", "manual") == "python" && !Academy.Instance.IsCommunicatorOn)
                throw new InvalidOperationException("Python 控制模式未連接訓練器。");
            Time.fixedDeltaTime = config.stepSeconds;
            stepLimit = int.Parse(Argument("--world-steps", "0"));
            runId = Guid.NewGuid().ToString("N");
            string logPath = Argument("--world-log", Path.Combine(Application.persistentDataPath, runId + ".jsonl"));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(logPath)));
            output = new StreamWriter(new FileStream(logPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
            output.AutoFlush = true;
            output.WriteLine(JsonUtility.ToJson(new RunRecord
            {
                run = runId, seed = seed, unity = Application.unityVersion,
                device = SystemInfo.graphicsDeviceName, config = config,
                control = Argument("--control", "manual")
            }));
            for (int axis = 0; axis < 2; axis++)
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(wall.GetComponent<Collider>());
                    wall.transform.position = axis == 0 ? new Vector3(sign * config.halfExtent, 0, 0)
                        : new Vector3(0, sign * config.halfExtent, 0);
                    wall.transform.localScale = axis == 0 ? new Vector3(0.05f, config.halfExtent * 2, 0.05f)
                        : new Vector3(config.halfExtent * 2, 0.05f, 0.05f);
                    wall.GetComponent<Renderer>().material = Resources.Load<Material>("WorldMaterial");
                }
            var camera = FindFirstObjectByType<Camera>();
            camera.orthographicSize = config.halfExtent + 1f;
        }

        public override void OnEpisodeBegin()
        {
            world.Reset();
            UpdateView();
            Record("reset");
        }

        public override void CollectObservations(VectorSensor sensor) => sensor.AddObservation(world.Observe());

        public override void OnActionReceived(ActionBuffers actions)
        {
            // Python 收到最後一次觀察後才關閉程序；等待期間不再推進世界。
            if (stepLimit > 0 && totalSteps >= stepLimit)
                return;
            world.Step(new Vector2(actions.ContinuousActions[0], actions.ContinuousActions[1]));
            totalSteps++;
            // 僅驗證訓練往返；此存活獎勵不是正式生態獎勵。
            AddReward(world.State.boundary ? -0.01f : 0.001f);
            UpdateView();
            Record(world.State.boundary ? "boundary" : "step");
            if (world.State.endReason != "")
            {
                Record("episode_end");
                EndEpisode();
            }
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var actions = actionsOut.ContinuousActions;
            actions[0] = (Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
            actions[1] = (Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
        }

        private void UpdateView() => transform.position = new Vector3(world.State.x, world.State.y, 0);
        private void Record(string kind) => output.WriteLine(JsonUtility.ToJson(new StepRecord
        { run = runId, kind = kind, totalSteps = totalSteps, state = world.State }));
        private void OnApplicationQuit() => output?.Dispose();

        [Serializable]
        private sealed class RunRecord
        {
            public string kind = "run";
            public string run;
            public int seed;
            public string unity;
            public string device;
            public string control;
            public string rules = "toolchain-v1";
            public WorldConfig config;
        }

        [Serializable]
        private sealed class StepRecord
        {
            public string run;
            public string kind;
            public long totalSteps;
            public int individual = 1;
            public string species = "toolchain";
            public WorldState state;
        }
    }
}
