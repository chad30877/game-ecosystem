using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor.SceneManagement;

namespace Ecosystem.Editor
{
    public static class SmokeBaselineValidation
    {
        private static double Measure(Action action)
        {
            for (int i = 0; i < 5; i++) action();
            var timer = Stopwatch.StartNew();
            for (int i = 0; i < 50; i++) action();
            return timer.Elapsed.TotalMilliseconds / 50;
        }

        public static void Run()
        {
            ValidateSmoke.RunAvoidance();
            string configText = File.ReadAllText("../artifacts/smoke-validation/current-perception/config.json");
            var config = JsonUtility.FromJson<WorldConfig>(configText);
            var oldConfig = JsonUtility.FromJson<SmokeBaseline.WorldConfig>(configText);
            var current = new World(config, 7);
            var baseline = new SmokeBaseline.World(oldConfig, 7);
            current.Reset(); baseline.Reset();
            var scenario = JsonUtility.FromJson<WorldScenario>(File.ReadAllText("../artifacts/smoke-validation/current-perception/scenario.json"));
            scenario.Apply(current);
            // 使用相同世界配置、種子及指定動作；基準沒有場景適配器，套用相同起始狀態。
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(current.State), baseline.State);
            baseline.RefreshPerception(true);
            for (int step = 1; step <= 650; step++)
            {
                var actions = current.State.individuals.ToDictionary(value => value.individual, value => scenario.Decide(value.individual, step));
                current.Step(actions);
                baseline.Step(actions.ToDictionary(pair => pair.Key, pair => new SmokeBaseline.WorldAction(pair.Value.movement,
                    pair.Value.forage, pair.Value.aim, pair.Value.attack, (SmokeBaseline.WorldSkill)pair.Value.skill,
                    pair.Value.turn, pair.Value.skillSlot, pair.Value.run, pair.Value.feedChild)));
                string projected = JsonUtility.ToJson(JsonUtility.FromJson<SmokeBaseline.WorldState>(JsonUtility.ToJson(current.State)));
                if (projected != JsonUtility.ToJson(baseline.State)) throw new Exception($"固定動作世界回歸失敗：{step}");
            }
            config.individualsPerSpecies = oldConfig.individualsPerSpecies = 40;
            config.populationLimit = oldConfig.populationLimit = 120;
            current = new World(config, 7); baseline = new SmokeBaseline.World(oldConfig, 7);
            current.Reset(); baseline.Reset();
            for (int i = 0; i < 32; i++)
            {
                current.State.areas.Add(new AreaState { area = i + 1, skill = WorldSkill.SporeSmoke, species = "Moss", x = i % 8 - 4, y = i / 8 - 2, radius = 2.5f, until = 100 });
                baseline.State.areas.Add(new SmokeBaseline.AreaState { area = i + 1, skill = SmokeBaseline.WorldSkill.SporeSmoke, species = "Moss", x = i % 8 - 4, y = i / 8 - 2, radius = 2.5f, until = 100 });
            }
            double oldPerception = Measure(() => baseline.RefreshPerception());
            double newPerception = Measure(() => current.RefreshPerception());
            double oldRules = Measure(() => { foreach (var individual in baseline.State.individuals) SmokeBaseline.ForagingController.Decide(baseline, individual); });
            double newRules = Measure(() => { foreach (var individual in current.State.individuals) ForagingController.Decide(current, individual); });
            var report = new Report { oldPerceptionMs = oldPerception, newPerceptionMs = newPerception, oldRulesMs = oldRules, newRulesMs = newRules };
            report.render960 = RenderCost(960, 540);
            report.render1280 = RenderCost(1280, 720);
            File.WriteAllText("../artifacts/smoke-validation/costs.json", JsonUtility.ToJson(report, true));
            UnityEngine.Debug.Log("650 步固定動作完全一致；感知、避險及渲染成本已量測");
        }

        private static double[] RenderCost(int width, int height)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 11;
            camera.transform.position = new Vector3(0, 0, -10);
            var target = new RenderTexture(width, height, 24);
            camera.targetTexture = target;
            var views = new List<GameObject>();
            for (int i = 0; i < 120; i++)
            {
                var body = GameObject.CreatePrimitive(PrimitiveType.Quad);
                body.transform.position = new Vector3(i % 12 - 6, i / 12 - 5, 0);
                body.transform.localScale = Vector3.one * 0.4f;
                body.GetComponent<Renderer>().sharedMaterial = Resources.Load<Material>("WorldMaterial");
                views.Add(body);
            }
            var smokeViews = new List<GameObject>();
            for (int i = 0; i < 32; i++)
            {
                var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                body.transform.position = new Vector3(i % 8 - 4, i / 8 - 2, 0.2f);
                body.transform.rotation = Quaternion.Euler(90, 0, 0);
                body.transform.localScale = new Vector3(5, 0.02f, 5);
                body.GetComponent<Renderer>().sharedMaterial = new Material(Resources.Load<Material>("WorldMaterial"));
                body.GetComponent<Renderer>().sharedMaterial.color = new Color(0.65f, 0.4f, 0.8f, 0.25f);
                smokeViews.Add(body);
            }
            double oldRender = Measure(camera.Render);
            foreach (var body in smokeViews) { UnityEngine.Object.DestroyImmediate(body.GetComponent<Renderer>().sharedMaterial); UnityEngine.Object.DestroyImmediate(body); }
            smokeViews.Clear();
            for (int i = 0; i < 32; i++)
            {
                var body = GameObject.CreatePrimitive(PrimitiveType.Quad);
                body.transform.position = new Vector3(i % 8 - 4, i / 8 - 2, 0.2f);
                body.transform.localScale = new Vector3(5, 5, 1);
                body.GetComponent<Renderer>().sharedMaterial = new Material(Resources.Load<Shader>("WorldArea"));
                smokeViews.Add(body);
            }
            double newRender = Measure(camera.Render);
            foreach (var body in smokeViews)
            {
                var material = body.GetComponent<Renderer>().sharedMaterial;
                material.SetFloat("_Focused", 1);
                material.SetVector("_View", new Vector4(0, 0, 6, 0));
                material.SetVector("_Facing", new Vector4(1, 0, 0, 0));
            }
            double clipped = Measure(camera.Render);
            foreach (var body in smokeViews) { UnityEngine.Object.DestroyImmediate(body.GetComponent<Renderer>().sharedMaterial); UnityEngine.Object.DestroyImmediate(body); }
            foreach (var body in views) UnityEngine.Object.DestroyImmediate(body);
            camera.targetTexture = null; target.Release();
            UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(camera.gameObject);
            return new[] { oldRender, newRender, clipped };
        }

        [Serializable]
        private sealed class Report
        {
            public int seed = 7, individuals = 120, smoke = 32, iterations = 50, fixedActionSteps = 650;
            public double oldPerceptionMs, newPerceptionMs, oldRulesMs, newRulesMs;
            public double[] render960, render1280;
        }
    }
}
