using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Ecosystem
{
    public sealed class WorldRunner : MonoBehaviour
    {
        private World world;
        private WorldReplay replay;
        private float replayClock;
        private string replaySeek;
        private readonly Dictionary<int, WorldIndividualView> views = new Dictionary<int, WorldIndividualView>();
        private StreamWriter recording;
        private HashSet<int> recordEpisodes;
        private bool summaryLog;
        private bool metricsLog;
        private HashSet<string> rulesSpecies;
        private bool singleEpisode;
        private readonly System.Diagnostics.Stopwatch elapsed = System.Diagnostics.Stopwatch.StartNew();
        private readonly Dictionary<int, WorldAgent> agents = new Dictionary<int, WorldAgent>();
        private StreamWriter output;
        private long totalSteps;
        private int stepLimit;
        private string runId;
        private string control;
        private bool resetRequested;
        private readonly List<GameObject> foodViews = new List<GameObject>();
        private WorldObserver observer;
        private string policyMode;
        private WorldScenario scenario;
        private readonly List<GameObject> projectileViews = new List<GameObject>();
        private readonly List<GameObject> areaViews = new List<GameObject>();

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
            try { InitializeWorld(); }
            catch (Exception error) { Debug.LogException(error); Application.Quit(1); }
        }

        private void InitializeWorld()
        {
            Application.runInBackground = true;
            if (!Application.isBatchMode && Argument("-screen-width", "") == "" && Argument("-screen-height", "") == "")
                Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
            string replayPath = Argument("--replay", "");
            if (replayPath != "") replay = new WorldReplay(replayPath);
            string configPath = Argument("--world-config", "");
            var config = replay != null ? replay.Header.config : configPath == "" ? new WorldConfig()
                : JsonUtility.FromJson<WorldConfig>(File.ReadAllText(configPath));
            int seed = replay != null ? replay.Header.seed : int.Parse(Argument("--world-seed", "1"));
            world = new World(config, seed);
            control = replay != null ? "replay" : Argument("--control", "manual");
            replaySeek = Argument("--replay-seek", "");
            summaryLog = Argument("--summary-log", "false") == "true";
            metricsLog = Argument("--metrics-log", "false") == "true";
            singleEpisode = Argument("--single-episode", "false") == "true";
            rulesSpecies = new HashSet<string>(Argument("--rules-species", "").Split(','));
            if (control != "manual" && control != "python" && control != "rules" && control != "idle" && control != "scripted" && control != "replay")
                throw new ArgumentException("未知控制模式。");
            if ((control == "rules" || control == "idle") && !config.foraging)
                throw new ArgumentException("規則控制須使用覓食配置。");
            string scenarioPath = Argument("--world-scenario", "");
            if (scenarioPath != "")
            {
                if (!config.foraging) throw new ArgumentException("指定動作須使用覓食配置。");
                scenario = JsonUtility.FromJson<WorldScenario>(File.ReadAllText(scenarioPath));
                if (scenario == null) throw new ArgumentException("缺少指定動作場景。");
            }
            if (control == "scripted" && scenario == null) throw new ArgumentException("指定動作須提供場景。");
            if (Application.isBatchMode && control != "python") Time.timeScale = 100f;
            policyMode = control == "python" ? Argument("--policy-mode", "training") : control;
            if (control == "python" && policyMode != "training" && policyMode != "fixed")
                throw new ArgumentException("未知策略模式。");
            if (replay == null)
            {
                var academy = Academy.Instance;
                academy.AutomaticSteppingEnabled = false;
                if (control == "python" && !academy.IsCommunicatorOn)
                    throw new InvalidOperationException("Python 控制模式未連接訓練器。");
            }
            Time.fixedDeltaTime = config.stepSeconds;
            stepLimit = int.Parse(Argument("--world-steps", "0"));
            runId = Guid.NewGuid().ToString("N");
            string logPath = Argument("--world-log", Path.Combine(Application.persistentDataPath, runId + ".jsonl"));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(logPath)));
            output = new StreamWriter(new FileStream(logPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
            output.AutoFlush = true;
            string recordingPath = Argument("--record-log", "");
            if (recordingPath != "")
            {
                recordEpisodes = new HashSet<int>(Argument("--record-episodes", "1").Split(',').Select(int.Parse));
                if (recordEpisodes.Any(value => value <= 0)) throw new ArgumentException("記錄輪次須為正數。");
                recording = new StreamWriter(new FileStream(recordingPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
                recording.AutoFlush = true;
            }
            string runRecord = JsonUtility.ToJson(new RunRecord
            {
                run = runId, seed = seed, unity = Application.unityVersion,
                device = SystemInfo.graphicsDeviceName, config = config, control = control, policyMode = policyMode, scenario = scenario, rulesSpecies = rulesSpecies.ToArray()
            });
            output.WriteLine(runRecord);
            recording?.WriteLine(runRecord);
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
            if (replay == null) ResetWorld();
            else ApplySnapshot(0, true);
            if (config.foraging && !Application.isBatchMode)
            {
                observer = gameObject.AddComponent<WorldObserver>();
                observer.Initialize(world, camera, policyMode,
                    () => { if (replay == null) resetRequested = true; else SeekReplay(0); },
                    () => Application.Quit(), ApplyFocus, replay, SeekReplay, Argument("--observer-diagnostics", "false") == "true");
            }
        }

        private void ResetWorld()
        {
            foreach (var agent in agents.Values)
            {
                // 停用送出最後觀察與 terminal；策略由 trainer 保留。
                agent.gameObject.SetActive(false);
                Destroy(agent.gameObject);
            }
            agents.Clear();
            views.Clear();
            ClearProjectileViews();
            foreach (var view in areaViews)
            {
                Destroy(view.GetComponent<Renderer>().sharedMaterial);
                Destroy(view);
            }
            areaViews.Clear();
            foreach (var view in foodViews)
            {
                Destroy(view.GetComponent<Renderer>().sharedMaterial);
                Destroy(view);
            }
            foodViews.Clear();
            world.Reset();
            scenario?.Apply(world);
            UpdateFoodViews();
            foreach (var individual in world.State.individuals)
                CreateAgent(individual);
            Record("reset");
        }

        private WorldIndividualView CreateView(IndividualState individual)
        {
            var body = GameObject.CreatePrimitive(individual.species == "Flame" ? PrimitiveType.Cube : individual.species == "Frost" ? PrimitiveType.Sphere : PrimitiveType.Cylinder);
            body.SetActive(false);
            body.name = individual.species + "-" + individual.individual;
            Destroy(body.GetComponent<Collider>());
            body.transform.localScale = Vector3.one * (world.Config.foraging ? 0.65f : 0.4f);
            var material = new Material(Resources.Load<Material>("WorldMaterial"));
            body.GetComponent<Renderer>().sharedMaterial = material;
            var view = body.AddComponent<WorldIndividualView>();
            view.World = world;
            view.Individual = individual;
            views.Add(individual.individual, view);
            body.SetActive(true);
            view.UpdateView(true);
            return view;
        }

        private void CreateAgent(IndividualState individual)
        {
            var body = CreateView(individual).gameObject;
            body.SetActive(false);
            var behavior = body.AddComponent<BehaviorParameters>();
            behavior.BehaviorName = individual.species;
            behavior.BrainParameters.VectorObservationSize = 19;
            behavior.BrainParameters.ActionSpec = new ActionSpec(4, new[] { 3, 3, 2, 2, 2, 2 });
            var entities = body.AddComponent<BufferSensorComponent>();
            entities.SensorName = "Entities";
            entities.ObservableSize = 10;
            // 張量另預留 64 列煙幕情報；與規則使用相同截斷結果。
            entities.MaxNumObservables = world.Config.foraging
                ? checked(Mathf.Max(world.Config.populationLimit, world.Config.individualsPerSpecies * 3) *
                    (Mathf.CeilToInt(5f / (world.Config.maturityAge >= 5f ? Mathf.Max(world.Config.stepSeconds, world.Config.reproductionCooldown) : world.Config.stepSeconds)) + 1) +
                    world.Config.foodSourceLimit + world.Config.foodMemoryLimit + world.Config.areaMemoryLimit + Mathf.CeilToInt(world.Config.episodeSteps * world.Config.stepSeconds / Mathf.Max(world.Config.stepSeconds, world.Config.reproductionCooldown)) + 66)
                : world.Config.individualsPerSpecies * 6 + 64;
            behavior.BehaviorType = control != "python" ? BehaviorType.HeuristicOnly : BehaviorType.Default;
            var agent = body.AddComponent<WorldAgent>();
            agent.World = world;
            agent.Individual = individual;
            agents.Add(individual.individual, agent);
            views[individual.individual].UpdateView();
            body.SetActive(true);
            Record("agent_joined", individual, agent.GetInstanceID());
        }

        private void FixedUpdate()
        {
            if (replay != null) return;
            // 最後一次 Python 通訊仍須送出觀察，等待關閉時不再推進世界。
            foreach (var agent in agents.Values)
                agent.RequestDecision();
            Academy.Instance.EnvironmentStep();
            if (stepLimit > 0 && totalSteps >= stepLimit)
                return;
            if (world.State.endReason != "") return;
            world.Step(agents.ToDictionary(pair => pair.Key, pair =>
                control == "rules" || rulesSpecies.Contains(pair.Value.Individual.species) ? ForagingController.Decide(world, pair.Value.Individual)
                : control == "scripted" ? scenario.Decide(pair.Key, world.State.step + 1)
                : control == "idle" ? new WorldAction(Vector2.zero) : pair.Value.Action));
            totalSteps++;
            foreach (var agent in agents.Values)
            {
                if (agent.Individual.health > 0)
                    world.State.rewards.Find(value => value.individual == agent.Individual.individual).survival = world.Config.trainingSurvivalReward;
                views[agent.Individual.individual].UpdateView();
            }
            UpdateFoodViews();
            foreach (var birth in world.State.events.Where(value => value.kind == "born"))
                world.State.rewards.Find(value => value.individual == birth.parent).birth += world.Config.trainingBirthReward;
            foreach (var death in world.State.events.Where(value => value.kind == "starved" || value.kind == "old_age" || value.kind == "combat"))
                world.State.rewards.Add(new RewardRecord { individual = death.individual, death = world.Config.trainingDeathPenalty });
            foreach (var reward in world.State.rewards)
                if (agents.TryGetValue(reward.individual, out var rewarded)) rewarded.AddReward(reward.Total);
            foreach (var individual in world.State.individuals.Where(value => agents.ContainsKey(value.individual)))
                individual.explorationRewardPending = 0;
            Record("step");
            UpdateProjectileViews();
            UpdateAreaViews();
            foreach (int id in agents.Keys.Where(id => !world.State.individuals.Any(value => value.individual == id)).ToArray())
            {
                var agent = agents[id];
                Record("agent_died", agent.Individual, agent.GetInstanceID());
                agent.gameObject.SetActive(false);
                Destroy(agent.gameObject);
                agents.Remove(id);
                views.Remove(id);
            }
            foreach (var individual in world.State.individuals.Where(value => !agents.ContainsKey(value.individual)))
            {
                CreateAgent(individual);
                agents[individual.individual].AddReward(individual.explorationRewardPending);
                individual.explorationRewardPending = 0;
            }
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
            world.RefreshPerception();
            observer?.CaptureEvents();
            if (world.State.endReason != "")
            {
                Record("episode_end");
                if (singleEpisode)
                {
                    output.WriteLine("{\"kind\":\"episode_complete\"}");
                    Application.Quit();
                    return;
                }
                if ((control == "python" || !world.Config.foraging || Application.isBatchMode || stepLimit > 0) && (stepLimit <= 0 || totalSteps < stepLimit)) ResetWorld();
            }
            if ((control != "python" || policyMode == "fixed") && stepLimit > 0 && totalSteps >= stepLimit)
                Application.Quit();
        }

        private void UpdateFoodViews()
        {
            while (foodViews.Count < world.State.foods.Count)
            {
                var food = world.State.foods[foodViews.Count];
                var view = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(view.GetComponent<Collider>());
                view.transform.rotation = Quaternion.Euler(90, 0, 0);
                view.transform.position = new Vector3(food.x, food.y, 0.3f);
                view.GetComponent<Renderer>().sharedMaterial = new Material(Resources.Load<Material>("WorldMaterial"));
                foodViews.Add(view);
            }
            for (int i = 0; i < foodViews.Count; i++)
            {
                foodViews[i].SetActive(i < world.State.foods.Count);
                if (i >= world.State.foods.Count) continue;
                var food = world.State.foods[i];
                float ratio = food.amount / food.capacity;
                float size = !food.mature ? 0.4f : food.patch ? 2.6f : food.natural ? 0.9f : 0.7f;
                foodViews[i].transform.position = new Vector3(food.x, food.y, 0.3f);
                foodViews[i].transform.localScale = new Vector3(size * (0.3f + ratio * 0.7f), 0.03f, size * (0.3f + ratio * 0.7f));
                foodViews[i].GetComponent<Renderer>().sharedMaterial.color = !food.mature ? new Color(0.45f, 0.3f, 0.15f)
                    : food.phase == FoodPhase.Resting ? new Color(0.22f, 0.18f, 0.16f)
                    : food.phase == FoodPhase.Recovering ? (food.natural ? new Color(0.38f, 0.55f, 0.24f) : new Color(0.22f, 0.52f, 0.52f))
                    : Color.Lerp(new Color(0.15f, 0.18f, 0.12f), food.natural ? new Color(0.95f, 0.8f, 0.25f) : new Color(0.25f, 0.9f, 0.8f), ratio);
            }
        }

        private void UpdateAreaViews()
        {
            while (areaViews.Count < world.State.areas.Count)
            {
                var view = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(view.GetComponent<Collider>());
                view.GetComponent<Renderer>().sharedMaterial = new Material(Resources.Load<Shader>("WorldArea"));
                areaViews.Add(view);
            }
            for (int i = 0; i < areaViews.Count; i++)
            {
                bool active = i < world.State.areas.Count;
                areaViews[i].SetActive(active);
                if (!active) continue;
                var area = world.State.areas[i];
                areaViews[i].transform.position = new Vector3(area.x, area.y, 0.2f);
                areaViews[i].transform.localScale = new Vector3(area.radius * 2, area.radius * 2, 1);
                areaViews[i].GetComponent<Renderer>().sharedMaterial.color = area.skill == WorldSkill.FrostTrap
                    ? new Color(0.3f, 0.8f, 1f, 0.55f) : new Color(0.75f, 0.5f, 0.9f, 0.18f);
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
                projectileViews[i].GetComponent<Renderer>().sharedMaterial.color = arrow.skill == WorldSkill.PiercingIceArrow ? Color.white
                    : arrow.skill == WorldSkill.FrostbindArrow ? Color.blue : Color.cyan;
                projectileViews[i].transform.position = new Vector3(arrow.x, arrow.y, -0.2f);
                projectileViews[i].transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(arrow.directionY, arrow.directionX) * Mathf.Rad2Deg);
            }
        }

        private void ApplySnapshot(int index, bool clearTrail)
        {
            world.ReadSnapshot(replay.Read(index));
            foreach (int id in views.Keys.Where(id => !world.State.individuals.Any(value => value.individual == id)).ToArray())
            {
                views[id].gameObject.SetActive(false);
                Destroy(views[id].gameObject);
                views.Remove(id);
            }
            foreach (var individual in world.State.individuals)
            {
                if (!views.TryGetValue(individual.individual, out var view)) view = CreateView(individual);
                view.Individual = individual;
                view.UpdateView(clearTrail);
            }
            UpdateFoodViews();
            UpdateAreaViews();
            UpdateProjectileViews();
            totalSteps = index;
            observer?.CaptureEvents();
        }

        private void SeekReplay(int index)
        {
            replayClock = index * world.Config.stepSeconds;
            observer?.ClearFeedback();
            ApplySnapshot(index, true);
            Record("snapshot");
        }

        private void Update()
        {
            if (replay != null)
            {
                if (replaySeek != "")
                {
                    foreach (string token in replaySeek.Split(',')) SeekReplay(token == "last" ? replay.Count - 1 : int.Parse(token));
                    replaySeek = "";
                    Application.Quit();
                    return;
                }
                replayClock = Mathf.Min(replayClock + Time.deltaTime, (replay.Count - 1) * world.Config.stepSeconds);
                int index = Mathf.Min(replay.Count - 1, Mathf.FloorToInt(replayClock / world.Config.stepSeconds));
                if (index != replay.Index) ApplySnapshot(index, false);
                if (index == replay.Count - 1) observer?.Pause();
                return;
            }
            if (resetRequested)
            {
                resetRequested = false;
                if (world.State.endReason == "")
                {
                    world.State.endReason = "manual_reset";
                    Record("episode_end");
                }
                ResetWorld();
            }
        }

        private void ApplyFocus(IndividualState selected)
        {
            foreach (var view in views.Values)
                foreach (var renderer in view.GetComponentsInChildren<Renderer>())
                    renderer.forceRenderingOff = selected != null && view.Individual != selected && !world.CanSee(selected, view.Individual);
            for (int i = 0; i < world.State.foods.Count; i++)
            {
                var food = world.State.foods[i];
                foodViews[i].GetComponent<Renderer>().forceRenderingOff = selected != null &&
                    !world.CanSeePoint(selected, new Vector2(food.x, food.y));
            }
            for (int i = 0; i < areaViews.Count && i < world.State.areas.Count; i++)
            {
                var area = world.State.areas[i];
                var renderer = areaViews[i].GetComponent<Renderer>();
                bool smoke = area.skill == WorldSkill.SporeSmoke;
                renderer.forceRenderingOff = selected != null && (smoke
                    ? !world.CanSeeCircle(selected, new Vector2(area.x, area.y), area.radius)
                    : !world.CanSeePoint(selected, new Vector2(area.x, area.y)));
                var material = renderer.sharedMaterial;
                material.SetFloat("_Focused", smoke && selected != null ? 1 : 0);
                if (selected != null)
                {
                    material.SetVector("_View", new Vector4(selected.x, selected.y, world.Config.observationRadius, 0));
                    material.SetVector("_Facing", new Vector4(Mathf.Cos(selected.facing * Mathf.Deg2Rad), Mathf.Sin(selected.facing * Mathf.Deg2Rad), 0, 0));
                }
            }
            for (int i = 0; i < projectileViews.Count; i++)
                projectileViews[i].GetComponent<Renderer>().forceRenderingOff = selected != null && i < world.State.projectiles.Count &&
                    !world.CanSeePoint(selected, new Vector2(world.State.projectiles[i].x, world.State.projectiles[i].y));
        }

        private void Record(string kind, IndividualState individual = null, int unityObject = 0)
        {
            string line = null;
            if (!summaryLog || kind != "step" || (recording != null && recordEpisodes.Contains(world.State.episode)))
                line = JsonUtility.ToJson(new StepRecord
                { run = runId, kind = kind, totalSteps = totalSteps, state = world.State, individual = individual, unityObject = unityObject });
            if (summaryLog && kind == "step")
            {
                if (world.State.events.Count > 0 || world.State.step % 50 == 0 || world.State.endReason != "")
                    output.WriteLine(JsonUtility.ToJson(new SampleRecord { kind = "sample", totalSteps = totalSteps,
                        step = world.State.step, time = world.State.time, events = world.State.events,
                        rewards = world.State.rewards, statistics = world.State.statistics,
                        foodAmount = world.State.foods.Sum(value => value.amount) }));
            }
            else output.WriteLine(line);
            if (metricsLog && kind == "step")
                output.WriteLine(JsonUtility.ToJson(new MetricRecord { step = world.State.step,
                    individuals = world.State.individuals.Select(value => new IndividualMetricRecord
                    {
                        individual = value.individual, species = value.species, x = value.x, y = value.y,
                        satiety = value.satiety, exploredCells = value.rewardedCells.Count,
                        rawTurn = value.rawTurn, actualTurn = value.actualTurn, scanIntervened = value.scanIntervened
                    }).ToList() }));
            if (recording != null && recordEpisodes.Contains(world.State.episode)) recording.WriteLine(line);
        }

        private void OnApplicationQuit()
        {
            if (output != null && world != null && world.State != null) Record("observation_end");
            if (output != null)
                output.WriteLine(JsonUtility.ToJson(new ResourceRecord { elapsedSeconds = elapsed.Elapsed.TotalSeconds,
                    peakWorkingSetBytes = System.Diagnostics.Process.GetCurrentProcess().PeakWorkingSet64 }));
            output?.Dispose();
            recording?.Dispose();
            replay?.Dispose();
        }

        [Serializable]
        private sealed class ResourceRecord
        {
            public string kind = "resources";
            public double elapsedSeconds;
            public long peakWorkingSetBytes;
        }

        [Serializable]
        private sealed class SampleRecord
        {
            public string kind;
            public long totalSteps;
            public int step;
            public float time;
            public List<WorldEvent> events;
            public List<RewardRecord> rewards;
            public List<SpeciesStatistics> statistics;
            public float foodAmount;
        }

        [Serializable]
        private sealed class MetricRecord
        {
            public string kind = "metric";
            public int step;
            public List<IndividualMetricRecord> individuals;
        }

        [Serializable]
        private sealed class IndividualMetricRecord
        {
            public int individual;
            public string species;
            public float x, y, satiety;
            public int exploredCells;
            public int rawTurn, actualTurn;
            public bool scanIntervened;
        }

        [Serializable]
        private sealed class RunRecord
        {
            public string kind = "run";
            public string run;
            public int seed;
            public string unity;
            public string device;
            public string control;
            public string policyMode;
            public string rules = "survival-v14";
            public string[] rulesSpecies;
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
