using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEngine;

namespace Ecosystem
{
    public sealed class WorldRunner : MonoBehaviour
    {
        private World world;
        private readonly Dictionary<int, WorldAgent> agents = new Dictionary<int, WorldAgent>();
        private StreamWriter output;
        private long totalSteps;
        private int stepLimit;
        private string runId;
        private string control;

        private static string Argument(string name, string fallback)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartWorld() => new GameObject("World").AddComponent<WorldRunner>();

        private void Awake()
        {
            Application.runInBackground = true;
            string configPath = Argument("--world-config", "");
            var config = configPath == "" ? new WorldConfig()
                : JsonUtility.FromJson<WorldConfig>(File.ReadAllText(configPath));
            int seed = int.Parse(Argument("--world-seed", "1"));
            world = new World(config, seed);
            control = Argument("--control", "manual");
            if (control != "manual" && control != "python")
                throw new ArgumentException("未知控制模式。");
            var academy = Academy.Instance;
            academy.AutomaticSteppingEnabled = false;
            if (control == "python" && !academy.IsCommunicatorOn)
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
                device = SystemInfo.graphicsDeviceName, config = config, control = control
            }));
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = config.halfExtent + 1f;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.backgroundColor = new Color(0.07f, 0.1f, 0.14f);
            camera.clearFlags = CameraClearFlags.SolidColor;
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
            ResetWorld();
        }

        private void ResetWorld()
        {
            foreach (var agent in agents.Values)
            {
                // 停用會送出最後觀察與 terminal；不重新啟用舊個體識別。
                agent.gameObject.SetActive(false);
                Destroy(agent.gameObject);
            }
            agents.Clear();
            world.Reset();
            foreach (var individual in world.State.individuals)
                CreateAgent(individual);
            Record("reset");
        }

        private void CreateAgent(IndividualState individual)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.SetActive(false);
            body.name = individual.species + "-" + individual.individual;
            Destroy(body.GetComponent<Collider>());
            body.transform.localScale = Vector3.one * 0.4f;
            var material = new Material(Resources.Load<Material>("WorldMaterial"));
            material.color = individual.species == "Flame" ? new Color(1f, 0.35f, 0.2f)
                : individual.species == "Frost" ? new Color(0.35f, 0.7f, 1f) : new Color(0.35f, 0.9f, 0.4f);
            body.GetComponent<Renderer>().sharedMaterial = material;
            var behavior = body.AddComponent<BehaviorParameters>();
            behavior.BehaviorName = individual.species;
            behavior.BrainParameters.VectorObservationSize = 4;
            behavior.BrainParameters.ActionSpec = ActionSpec.MakeContinuous(2);
            behavior.BehaviorType = control == "manual" ? BehaviorType.HeuristicOnly : BehaviorType.Default;
            var agent = body.AddComponent<WorldAgent>();
            agent.World = world;
            agent.Individual = individual;
            agents.Add(individual.individual, agent);
            agent.UpdateView();
            body.SetActive(true);
            Record("agent_joined", individual, agent.GetInstanceID());
        }

        private void FixedUpdate()
        {
            // 最後一次 Python 通訊仍須送出觀察，等待關閉時不再推進世界。
            foreach (var agent in agents.Values)
                agent.RequestDecision();
            Academy.Instance.EnvironmentStep();
            if (stepLimit > 0 && totalSteps >= stepLimit)
                return;
            world.Step(agents.ToDictionary(pair => pair.Key, pair => pair.Value.Action));
            totalSteps++;
            foreach (var agent in agents.Values)
            {
                // 只驗證更新機制，尚非正式生態獎勵。
                agent.AddReward(agent.Individual.boundary ? -0.01f : 0.001f);
                agent.UpdateView();
            }
            Record("step");
            if (world.State.step == world.Config.removeStep)
                foreach (string species in World.Species)
                {
                    var individual = world.State.individuals.First(value => value.species == species);
                    var agent = agents[individual.individual];
                    Record("agent_removed", individual, agent.GetInstanceID());
                    agent.gameObject.SetActive(false);
                    Destroy(agent.gameObject);
                    agents.Remove(individual.individual);
                    world.RemoveIndividual(individual.individual);
                }
            if (world.State.step == world.Config.addStep)
                foreach (string species in World.Species)
                    CreateAgent(world.AddIndividual(species));
            if (world.State.endReason != "")
            {
                Record("episode_end");
                ResetWorld();
            }
        }

        private void Record(string kind, IndividualState individual = null, int unityObject = 0) => output.WriteLine(JsonUtility.ToJson(new StepRecord
        { run = runId, kind = kind, totalSteps = totalSteps, state = world.State, individual = individual, unityObject = unityObject }));

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
            public string rules = "shared-policies-v1";
            public WorldConfig config;
        }

        [Serializable]
        private sealed class StepRecord
        {
            public string run;
            public string kind;
            public long totalSteps;
            public WorldState state;
            public IndividualState individual;
            public int unityObject;
        }
    }
}
