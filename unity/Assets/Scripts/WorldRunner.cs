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
        private readonly List<GameObject> foodViews = new List<GameObject>();
        private int selected;
        private Font font;
        private WorldScenario scenario;
        private readonly List<GameObject> projectileViews = new List<GameObject>();

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
            if (control != "manual" && control != "python" && control != "rules" && control != "idle" && control != "scripted")
                throw new ArgumentException("未知控制模式。");
            if ((control == "rules" || control == "idle") && !config.foraging)
                throw new ArgumentException("規則控制須使用覓食配置。");
            if (config.foraging && control == "python")
                throw new ArgumentException("覓食策略橋接尚未實作，請使用 rules、idle 或 manual。");
            if (control == "scripted")
            {
                if (!config.foraging) throw new ArgumentException("指定動作須使用覓食配置。");
                scenario = JsonUtility.FromJson<WorldScenario>(File.ReadAllText(Argument("--world-scenario", "")));
                if (scenario == null) throw new ArgumentException("缺少指定動作場景。");
            }
            if (Application.isBatchMode && control != "python") Time.timeScale = 100f;
            font = Font.CreateDynamicFontFromOSFont("Microsoft JhengHei", 16);
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
                device = SystemInfo.graphicsDeviceName, config = config, control = control, scenario = scenario
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
            ClearProjectileViews();
            foreach (var view in foodViews)
            {
                Destroy(view.GetComponent<Renderer>().sharedMaterial);
                Destroy(view);
            }
            foodViews.Clear();
            world.Reset();
            scenario?.Apply(world);
            selected = 0;
            foreach (var food in world.State.foods)
            {
                var view = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(view.GetComponent<Collider>());
                view.transform.rotation = Quaternion.Euler(90, 0, 0);
                view.transform.position = new Vector3(food.x, food.y, 0.3f);
                view.GetComponent<Renderer>().sharedMaterial = new Material(Resources.Load<Material>("WorldMaterial"));
                foodViews.Add(view);
            }
            UpdateFoodViews();
            foreach (var individual in world.State.individuals)
                CreateAgent(individual);
            Record("reset");
        }

        private void CreateAgent(IndividualState individual)
        {
            var body = GameObject.CreatePrimitive(individual.species == "Flame" ? PrimitiveType.Cube : individual.species == "Frost" ? PrimitiveType.Sphere : PrimitiveType.Cylinder);
            body.SetActive(false);
            body.name = individual.species + "-" + individual.individual;
            Destroy(body.GetComponent<Collider>());
            body.transform.localScale = Vector3.one * (world.Config.foraging ? 0.65f : 0.4f);
            var material = new Material(Resources.Load<Material>("WorldMaterial"));
            material.color = individual.species == "Flame" ? new Color(1f, 0.35f, 0.2f)
                : individual.species == "Frost" ? new Color(0.35f, 0.7f, 1f) : new Color(0.35f, 0.9f, 0.4f);
            body.GetComponent<Renderer>().sharedMaterial = material;
            var behavior = body.AddComponent<BehaviorParameters>();
            behavior.BehaviorName = individual.species;
            behavior.BrainParameters.VectorObservationSize = 4;
            behavior.BrainParameters.ActionSpec = ActionSpec.MakeContinuous(2);
            behavior.BehaviorType = control != "python" ? BehaviorType.HeuristicOnly : BehaviorType.Default;
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
            if (world.State.endReason != "") return;
            if (world.Config.foraging)
                world.Step(agents.ToDictionary(pair => pair.Key, pair =>
                    control == "rules" ? ForagingController.Decide(world, pair.Value.Individual)
                    : control == "scripted" ? scenario.Decide(pair.Key, world.State.step + 1)
                    : new WorldAction(control == "idle" ? Vector2.zero : pair.Value.Action,
                        control == "manual" && Input.GetKey(KeyCode.Space), pair.Value.Action,
                        control == "manual" && Input.GetKey(KeyCode.A),
                        control == "manual" && Input.GetKey(KeyCode.S) ? WorldSkill.Charge : WorldSkill.None)));
            else
                world.Step(agents.ToDictionary(pair => pair.Key, pair => pair.Value.Action));
            totalSteps++;
            foreach (var agent in agents.Values)
            {
                // 只驗證更新機制，尚非正式生態獎勵。
                agent.AddReward(agent.Individual.boundary ? -0.01f : 0.001f);
                agent.UpdateView();
            }
            UpdateFoodViews();
            Record("step");
            UpdateProjectileViews();
            foreach (int id in agents.Keys.Where(id => !world.State.individuals.Any(value => value.individual == id)).ToArray())
            {
                var agent = agents[id];
                Record("agent_died", agent.Individual, agent.GetInstanceID());
                agent.gameObject.SetActive(false);
                Destroy(agent.gameObject);
                agents.Remove(id);
            }
            foreach (var individual in world.State.individuals.Where(value => !agents.ContainsKey(value.individual)))
                CreateAgent(individual);
            if (!world.Config.foraging && world.State.step == world.Config.removeStep)
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
            if (!world.Config.foraging && world.State.step == world.Config.addStep)
                foreach (string species in World.Species)
                    CreateAgent(world.AddIndividual(species));
            if (world.State.endReason != "")
            {
                Record("episode_end");
                if ((!world.Config.foraging || Application.isBatchMode || stepLimit > 0) && (stepLimit <= 0 || totalSteps < stepLimit)) ResetWorld();
            }
            if (control != "python" && stepLimit > 0 && totalSteps >= stepLimit)
                Application.Quit();
        }

        private void UpdateFoodViews()
        {
            for (int i = 0; i < foodViews.Count; i++)
            {
                var food = world.State.foods[i];
                float ratio = food.amount / food.capacity;
                float size = food.patch ? 2.6f : 0.9f;
                foodViews[i].transform.localScale = new Vector3(size * (0.3f + ratio * 0.7f), 0.03f, size * (0.3f + ratio * 0.7f));
                foodViews[i].GetComponent<Renderer>().sharedMaterial.color = Color.Lerp(new Color(0.15f, 0.18f, 0.12f), new Color(0.95f, 0.8f, 0.25f), ratio);
            }
        }

        private void ClearProjectileViews()
        {
            foreach (var view in projectileViews)
            {
                Destroy(view.GetComponent<Renderer>().sharedMaterial);
                Destroy(view);
            }
            projectileViews.Clear();
        }

        private void UpdateProjectileViews()
        {
            while (projectileViews.Count < world.State.projectiles.Count)
            {
                var view = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(view.GetComponent<Collider>());
                view.transform.localScale = new Vector3(0.5f, 0.12f, 0.12f);
                var material = new Material(Resources.Load<Material>("WorldMaterial"));
                material.color = Color.cyan;
                view.GetComponent<Renderer>().sharedMaterial = material;
                projectileViews.Add(view);
            }
            for (int i = 0; i < projectileViews.Count; i++)
            {
                projectileViews[i].SetActive(i < world.State.projectiles.Count);
                if (i >= world.State.projectiles.Count) continue;
                var arrow = world.State.projectiles[i];
                projectileViews[i].transform.position = new Vector3(arrow.x, arrow.y, -0.2f);
                projectileViews[i].transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(arrow.directionY, arrow.directionX) * Mathf.Rad2Deg);
            }
        }

        private void Update()
        {
            if (!world.Config.foraging || !Input.GetMouseButtonDown(0)) return;
            var point = Camera.main != null ? Camera.main.ScreenToWorldPoint(Input.mousePosition)
                : FindFirstObjectByType<Camera>().ScreenToWorldPoint(Input.mousePosition);
            var nearest = world.State.individuals.OrderBy(value => Vector2.Distance(new Vector2(value.x, value.y), point)).FirstOrDefault();
            if (nearest != null && Vector2.Distance(new Vector2(nearest.x, nearest.y), point) < 1.2f) selected = nearest.individual;
        }

        private void OnGUI()
        {
            if (world == null || !world.Config.foraging) return;
            GUI.skin.font = font;
            GUI.skin.label.fontSize = 16;
            GUI.Box(new Rect(8, 8, 590, 282), "");
            GUI.Label(new Rect(18, 12, 570, 30), control == "rules" ? "規則控制・尚非學習結果" : control == "idle" ? "靜止控制・飢餓核對" : control == "scripted" ? "指定動作・尚非學習結果" : "手動・方向鍵／空白採食／A 攻擊／S 技能");
            GUI.Label(new Rect(18, 42, 350, 28), $"{world.State.time:F1} 秒　炎角 {world.State.individuals.Count(v => v.species == "Flame")}　霜羽 {world.State.individuals.Count(v => v.species == "Frost")}　苔靈 {world.State.individuals.Count(v => v.species == "Moss")}");
            var individual = world.State.individuals.FirstOrDefault(value => value.individual == selected);
            GUI.Label(new Rect(18, 72, 520, 28), individual == null ? "點選個體" : $"#{individual.individual} {individual.species}　健康 {individual.health:F1}　能量 {individual.energy:F1}");
            if (individual != null)
            {
                GUI.Label(new Rect(18, 100, 570, 28), $"年齡 {individual.age:F1}　{(individual.age < world.Config.maturityAge ? "成長期" : "成年")}　繁殖 {individual.reproductionCooldown:F1}");
                GUI.Label(new Rect(18, 128, 570, 28), $"攻擊 {individual.attackCooldown:F1}　技能 {individual.skillCooldown:F1}　前搖 {individual.windupRemaining:F1}　減速 {individual.slowRemaining:F1}　{(individual.chargeRemaining > 0 ? "衝鋒" : "")}");
            }
            int row = 0;
            foreach (var statistics in world.State.statistics)
                GUI.Label(new Rect(18, 156 + row++ * 24, 570, 26), $"{statistics.species}　出生 {statistics.births}　戰死 {statistics.combat}　飢餓 {statistics.starved}　老死 {statistics.oldAge}　存活 {statistics.incompleteLives}");
            GUI.Label(new Rect(18, 246, 350, 28), world.State.endReason == "all_dead" ? "本輪結束・全體死亡" : world.State.endReason == "time_limit" ? "本輪結束・時間上限" : $"第 {world.State.episode} 輪　上限觸發 {world.State.statistics.Sum(v => v.populationLimitHits)}");
            if (world.State.endReason != "" && GUI.Button(new Rect(458, 244, 120, 32), "重建世界")) ResetWorld();
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
            public string rules = "frost-v1";
            public WorldConfig config;
            public WorldScenario scenario;
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
