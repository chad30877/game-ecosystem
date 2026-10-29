using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ecosystem
{
    public sealed class WorldObserver : MonoBehaviour
    {
        private World world;
        private WorldReplay replay;
        private Action<int> seek;
        private Camera worldCamera;
        private string mode;
        private Action rebuild;
        private Action quit;
        private Action<IndividualState> focus;
        private Font font;
        private int selected;
        private int episode;
        private float speed = 1f;
        private bool paused;
        private string feedback = "";
        private float feedbackUntil;
        private string notice = "";
        private float noticeUntil;
        private LineRenderer selectionRing;
        private LineRenderer fieldOfView;
        private Material overlayMaterial;
        private Rect toolbar;
        private Rect inspector;
        private Vector2 scroll;
        private Rect viewport;
        private bool panelExpanded = true;
        private bool overviewFit = true;
        private bool diagnostics;
        private string lastLayout = "";

        private void UpdateLayout()
        {
            toolbar = new Rect(8, 8, Screen.width - 16, replay == null ? 108 : 148);
            bool details = selected != 0 && panelExpanded;
            float width = Mathf.Min(360, Screen.width * 0.32f);
            inspector = details ? new Rect(Screen.width - width - 8, toolbar.yMax + 8, width, Screen.height - toolbar.yMax - 16) : new Rect();
            viewport = new Rect(8, toolbar.yMax + 8, Screen.width - width - 24, Screen.height - toolbar.yMax - 16);
            worldCamera.pixelRect = new Rect(viewport.x, Screen.height - viewport.yMax, viewport.width, viewport.height);
            if (Selection == null && overviewFit)
            {
                worldCamera.transform.position = new Vector3(0, 0, -10);
                worldCamera.orthographicSize = (world.Config.halfExtent + 1) * Mathf.Max(1, viewport.height / viewport.width);
            }
            if (diagnostics)
            {
                string layout = JsonUtility.ToJson(new LayoutRecord { width = Screen.width, height = Screen.height,
                    selected = selected, expanded = panelExpanded, viewport = viewport, toolbar = toolbar, inspector = inspector,
                    center = worldCamera.transform.position, scale = worldCamera.orthographicSize });
                if (layout != lastLayout) { Debug.Log("observer_layout " + layout); lastLayout = layout; }
            }
        }

        [Serializable]
        private sealed class LayoutRecord
        {
            public int width, height, selected;
            public bool expanded;
            public Rect viewport, toolbar, inspector;
            public Vector3 center;
            public float scale;
        }

        public void Initialize(World source, Camera camera, string controlMode, Action onRebuild, Action onQuit, Action<IndividualState> onFocus, WorldReplay history = null, Action<int> onSeek = null, bool logLayout = false)
        {
            world = source;
            replay = history;
            seek = onSeek;
            worldCamera = camera;
            mode = controlMode;
            diagnostics = logLayout;
            rebuild = onRebuild;
            quit = onQuit;
            focus = onFocus;
            font = Font.CreateDynamicFontFromOSFont("Microsoft JhengHei", 16);
            overlayMaterial = new Material(Resources.Load<Material>("WorldMaterial"));
            overlayMaterial.color = new Color(1f, 0.9f, 0.4f);
            selectionRing = CreateLine("Selection", 33);
            fieldOfView = CreateLine("FieldOfView", 27);
        }

        private LineRenderer CreateLine(string name, int points)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(transform);
            line.sharedMaterial = overlayMaterial;
            line.startWidth = line.endWidth = 0.045f;
            line.positionCount = points;
            return line;
        }

        private IndividualState Selection => world.State.individuals.Find(value => value.individual == selected);

        public void Pause() => SetPaused(true);
        public void ClearFeedback() { feedback = notice = ""; feedbackUntil = noticeUntil = 0; }

        private void SetPaused(bool value)
        {
            paused = value;
            Time.timeScale = paused ? 0 : speed;
        }

        private void Update()
        {
            if (world == null) return;
            UpdateLayout();
            if (Input.GetKeyDown(KeyCode.P)) SetPaused(!paused);
            var pointer = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            if (!viewport.Contains(pointer)) return;
            float wheel = Input.mouseScrollDelta.y;
            if (wheel != 0)
            {
                overviewFit = false;
                var before = worldCamera.ScreenToWorldPoint(Input.mousePosition);
                worldCamera.orthographicSize = Mathf.Clamp(worldCamera.orthographicSize * Mathf.Pow(0.85f, wheel), 1f, (world.Config.halfExtent + 1) * 2);
                worldCamera.transform.position += before - worldCamera.ScreenToWorldPoint(Input.mousePosition);
            }
            if (!Input.GetMouseButtonDown(0)) return;
            var point = worldCamera.ScreenToWorldPoint(Input.mousePosition);
            var current = Selection;
            var nearest = world.State.individuals.Where(value => current == null || value == current || world.CanSee(current, value))
                .OrderBy(value => Vector2.Distance(CombatRules.Position(value), point)).FirstOrDefault();
            selected = nearest != null && Vector2.Distance(CombatRules.Position(nearest), point) <= 0.8f ? nearest.individual : 0;
            if (selected == 0) overviewFit = true;
            feedback = "";
            scroll = Vector2.zero;
        }

        private void LateUpdate()
        {
            if (world == null) return;
            UpdateLayout();
            if (episode != world.State.episode)
            {
                episode = world.State.episode;
                selected = 0;
                feedback = notice = "";
            }
            var individual = Selection;
            if (selected != 0 && individual == null)
            {
                var death = world.State.deaths.Find(value => value.individual == selected);
                notice = $"#{selected} {(death == null ? "已離開" : "已死亡・" + DeathName(death.cause))}";
                noticeUntil = Time.unscaledTime + 4;
                if (death == null) selected = 0;
                feedback = "";
            }
            focus(individual);
            selectionRing.enabled = fieldOfView.enabled = individual != null;
            if (individual == null) return;
            Vector3 center = new Vector3(individual.x, individual.y, -0.5f);
            for (int i = 0; i <= 32; i++)
                selectionRing.SetPosition(i, center + new Vector3(Mathf.Cos(i * Mathf.PI / 16), Mathf.Sin(i * Mathf.PI / 16), 0) * 0.65f);
            fieldOfView.SetPosition(0, center);
            for (int i = 0; i <= 24; i++)
            {
                float angle = (individual.facing - 60 + i * 5) * Mathf.Deg2Rad;
                fieldOfView.SetPosition(i + 1, center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * world.Config.observationRadius);
            }
            fieldOfView.SetPosition(26, center);
        }

        public void CaptureEvents()
        {
            var individual = Selection;
            if (individual == null) return;
            foreach (var item in world.State.events)
            {
                if (item.target != 0 && item.individual == selected)
                {
                    var target = world.State.individuals.Find(value => value.individual == item.target);
                    if (target == null || !world.CanSee(individual, target)) continue;
                    feedback = $"{EffectName(item.kind)} → {SpeciesName(target.species)} ({target.x:F1}, {target.y:F1})";
                    feedbackUntil = world.State.time + 3;
                }
                else if (item.target == selected && item.rawAmount > 0)
                {
                    feedback = $"受擊 {item.amount:F1}";
                    feedbackUntil = world.State.time + 3;
                }
            }
        }

        private void OnGUI()
        {
            if (world == null) return;
            GUI.skin.font = font;
            GUI.skin.label.fontSize = 16;
            GUI.skin.label.wordWrap = true;
            UpdateLayout();
            GUI.color = worldCamera.backgroundColor;
            GUI.DrawTexture(new Rect(viewport.xMax, toolbar.yMax, Screen.width - viewport.xMax, Screen.height - toolbar.yMax), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUILayout.BeginArea(toolbar, GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label(ModeName(), GUILayout.Width(180));
            GUILayout.Label($"第 {world.State.episode} 輪　{world.State.time:F1} 秒");
            if (GUILayout.Button(paused ? "恢復 (P)" : "暫停 (P)", GUILayout.Width(88))) SetPaused(!paused);
            foreach (float value in new[] { 0.5f, 1f, 2f, 4f, 8f })
            {
                GUI.color = Mathf.Approximately(speed, value) ? Color.yellow : Color.white;
                if (GUILayout.Button($"{value:g}×", GUILayout.Width(44))) { speed = value; SetPaused(paused); }
            }
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            var current = Selection;
            GUILayout.Label(current == null
                ? string.Join("　", world.State.statistics.Select(value => $"{SpeciesName(value.species)} {value.incompleteLives}（+{value.births} / −{value.combat + value.starved + value.oldAge}）"))
                : "可見　" + string.Join("　", World.Species.Select(species => $"{SpeciesName(species)} {current.perception.Count(value => value.visible && value.species == species)}")));
            if (world.State.deaths.Count > 0 && GUILayout.Button($"死亡 {world.State.deaths.Count}", GUILayout.Width(72)))
                selected = world.State.deaths[world.State.deaths.Count - 1].individual;
            if (GUILayout.Button("全景", GUILayout.Width(60)))
            {
                worldCamera.transform.position = new Vector3(0, 0, -10);
                worldCamera.orthographicSize = world.Config.halfExtent + 1;
                selected = 0;
                overviewFit = true;
            }
            if (selected != 0 && GUILayout.Button(panelExpanded ? "收合資料" : "展開資料", GUILayout.Width(80))) panelExpanded = !panelExpanded;
            if (GUILayout.Button(replay == null ? "重建世界" : "回到開頭", GUILayout.Width(88))) rebuild();
            if (GUILayout.Button(mode == "training" ? "保存並結束" : "結束", GUILayout.Width(100))) quit();
            GUILayout.EndHorizontal();
            string end = world.State.endReason == "all_dead" ? "全體死亡" : world.State.endReason == "time_limit" ? "時間上限" : "";
            GUILayout.Label(end != "" ? $"本輪結束・{end}" : Time.unscaledTime < noticeUntil ? notice : selected == 0 ? "點選個體　滾輪縮放" : "個體視野・點選空白或全景返回");
            if (replay != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{replay.Index + 1} / {replay.Count}　{replay.Bytes / 1048576f:F1} MB", GUILayout.Width(230));
                int index = Mathf.RoundToInt(GUILayout.HorizontalSlider(replay.Index, 0, replay.Count - 1));
                if (index != replay.Index) seek(index);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
            var individual = Selection;
            if (individual == null)
            {
                var death = world.State.deaths.Find(value => value.individual == selected);
                if (death != null && panelExpanded) DrawDeath(death);
                return;
            }
            if (panelExpanded)
            {
            GUILayout.BeginArea(inspector, GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label($"#{individual.individual}　{SpeciesName(individual.species)}");
            GUILayout.Label($"健康 {individual.health:F1} / {CombatRules.CurrentMaxHealth(world.Config, individual):F1}　飽食度 {individual.satiety:F1} / 100\n{(individual.running ? "奔跑" : "走路")}　體力 {individual.stamina:F1}　魔力 {individual.mana:F1}");
            GUILayout.Label($"營養恢復 {string.Join(" / ", individual.recoveryStatus)}\n基礎 {individual.basalSpent:F1}　進食 {individual.foodReceived:F1}　營養支出 {individual.nutritionSpent:F1}\n活動體力 {individual.activityStaminaSpent:F1}　技能魔力 {individual.skillManaSpent:F1}　繁殖 {individual.reproductionSpent:F1}");
            GUILayout.Label($"決策 {DecisionName(individual.decisionReason)}　{(individual.hasFoodTarget ? $"食物 {individual.foodTargetX:F1},{individual.foodTargetY:F1}" : "")}");
            GUILayout.Label($"觀測 {individual.observedSeconds:F1}s　{(individual.initialAdult ? "起始成年" : "本輪出生")}");
            if (individual.protectionChild != 0) GUILayout.Label($"保護 #{individual.protectionChild}　威脅 #{individual.protectionThreat}");
            GUILayout.Label($"餵養 {(individual.careTarget != 0 ? $"#{individual.careTarget}" : individual.careReceivedFrom != 0 ? $"來自 #{individual.careReceivedFrom}" : "無")}　付出 {individual.careGiven:F1}　取得 {individual.careReceived:F1}");
            DrawLedger(individual.ledger);
            DrawRecoveries(individual.automaticRecovered, individual.nutritionRecovered);
            GUILayout.Label($"年齡 {individual.age:F1}　成長 {CombatRules.Growth(world.Config, individual):P0}　{(individual.age < world.Config.maturityAge ? "成長期" : "成年")}　繁殖 {individual.reproductionCooldown:F1}s");
            GUILayout.Label($"普通攻擊　{(individual.species == "Moss" ? "無" : individual.attackCooldown > 0 ? $"冷卻 {individual.attackCooldown:F1}s" : "就緒")}");
            for (int slot = 0; slot < individual.loadout.Length; slot++)
                GUILayout.Label($"{slot + 1}　{SkillName(individual.loadout[slot])}　{Availability(individual, slot)}");
            foreach (string status in Statuses(individual)) GUILayout.Label(status);
            if (individual.cultivateRemaining > 0)
            {
                var food = world.State.foods.Find(value => value.food == individual.cultivatingFood);
                if (food != null && world.CanSeePoint(individual, new Vector2(food.x, food.y)))
                    GUILayout.Label($"培育 → 食物 ({food.x:F1}, {food.y:F1})");
            }
            foreach (var area in world.State.areas.Where(value => value.owner == selected && world.CanSeePoint(individual, new Vector2(value.x, value.y))))
                GUILayout.Label($"{SkillName(area.skill)} → 地面 ({area.x:F1}, {area.y:F1})　{Mathf.Max(0, area.until - world.State.time):F1}s");
            if (world.State.time < feedbackUntil) GUILayout.Label(feedback);
            GUILayout.Space(8);
            GUILayout.Label($"朝向 {individual.facing:F0}°　視野 120° / {world.Config.observationRadius:g}");
            if (individual.scanIntervened) GUILayout.Label($"掃視　{individual.rawTurn} → {individual.actualTurn}");
            else if (individual.scanDeferred) GUILayout.Label($"掃視暫緩　{ScanReason(individual.scanReason)}　{individual.scanDeferredSeconds:F1}s");
            else if (individual.rawTurn != 0) GUILayout.Label($"主動轉向　{individual.rawTurn}");
            GUILayout.Label($"可見 {individual.perception.Count(value => value.visible)}　警戒記憶 {individual.perception.Count(value => !value.visible)}　區域 {individual.areaMemory.Count}");
            if (!individual.smokeKnowledgeAvailable) GUILayout.Label("煙幕情報未記錄");
            foreach (var smoke in individual.smokeKnowledge)
                GUILayout.Label($"煙幕 {(smoke.visible ? "可見" : "記憶")}・{(smoke.harmful ? "有害" : "無毒")} ({smoke.x:F1}, {smoke.y:F1}) r{smoke.radius:F1}　{world.State.time - smoke.lastSeenAt:F1}s");
            var reward = world.State.rewards.Find(value => value.individual == selected);
            if (reward != null) GUILayout.Label($"探索 {individual.rewardedCells.Count} 格　累計 {individual.explorationRewardTotal:F4}　此步 {reward.exploration:F4}");
            if (individual.parentId != 0) GUILayout.Label($"親代 #{individual.parentId}");
            foreach (var child in individual.children)
                GUILayout.Label($"子女 #{child.child}　{(child.confirmedDead ? "確認死亡" : child.visible ? "可見" : "最後觀察")} ({child.x:F1}, {child.y:F1})　{child.lastSeenAt:F1}s　推測年齡 {child.predictedAge:F1}、飽食 {child.predictedSatiety:F1}{(child.searched ? "　已搜尋" : "")}");
            foreach (var food in individual.visibleFoods.Concat(individual.foodMemory.Where(value => !value.visible)))
                GUILayout.Label($"食物 ({food.x:F1}, {food.y:F1})　{(food.visible ? "可見" : world.State.time - food.lastSeenAt >= world.Config.foodMemorySeconds ? "舊情報" : "記憶")}　{(food.mature && food.phase == FoodPhase.Available ? "可採" : food.phase == FoodPhase.Resting ? "休養" : "成長中")}　{food.amount:F1}{(food.possiblyAvailable ? "　推測可能恢復" : "")}");
            foreach (var entity in individual.perception.Where(value => !value.visible))
                GUILayout.Label($"{SpeciesName(entity.species)} 最後位置 ({entity.x:F1}, {entity.y:F1})　{entity.age:F1}s 前");
            foreach (var target in world.State.individuals.Where(value => world.CanSee(individual, value)))
            {
                string status = string.Join("、", Statuses(target));
                if (status != "") GUILayout.Label($"{SpeciesName(target.species)} ({target.x:F1}, {target.y:F1})　{status}");
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            }
            foreach (var memory in individual.perception.Where(value => !value.visible))
            {
                var point = worldCamera.WorldToScreenPoint(new Vector3(memory.x, memory.y, 0));
                var rect = new Rect(point.x - 40, Screen.height - point.y - 12, 100, 24);
                if (point.z > 0 && viewport.Contains(rect.min) && viewport.Contains(rect.max)) GUI.Label(rect, $"◇ {SpeciesName(memory.species)}");
            }
            foreach (var food in individual.foodMemory.Where(value => !value.visible))
            {
                var point = worldCamera.WorldToScreenPoint(new Vector3(food.x, food.y, 0));
                var rect = new Rect(point.x - 40, Screen.height - point.y - 12, 100, 24);
                if (point.z > 0 && viewport.Contains(rect.min) && viewport.Contains(rect.max)) GUI.Label(rect, "◇ 食物");
            }
        }

        private string Availability(IndividualState individual, int slot)
        {
            if (individual.skillCooldowns[slot] > 0) return $"冷卻 {individual.skillCooldowns[slot]:F1}s";
            if (individual.mana < SkillRules.ManaCost(world.Config.combat, individual.loadout[slot])) return "魔力不足";
            if (individual.chargeRemaining > 0 || individual.dashRemaining > 0 || individual.windupActiveThisStep || individual.cultivateRemaining > 0) return "施放中";
            if (individual.loadout[slot] == WorldSkill.ColdMistDash && individual.rootRemaining > 0) return "定身中";
            return "就緒";
        }

        private IEnumerable<string> Statuses(IndividualState individual)
        {
            if (individual.rootRemaining > 0) yield return $"定身 {individual.rootRemaining:F1}s";
            if (individual.slowRemaining > 0) yield return $"減速 {individual.slowRemaining:F1}s";
            if (individual.poisonStacks > 0) yield return $"中毒 {individual.poisonStacks} 層 / {Mathf.Max(0, individual.poisonUntil - world.State.time):F1}s";
            if (individual.dashRemaining > 0) yield return $"寒霧遁行 {individual.dashRemaining:F1}m";
            if (individual.chargeRemaining > 0) yield return $"烈焰衝鋒 {individual.chargeRemaining:F1}m";
            if (individual.shieldRemaining > 0) yield return $"防禦 → 自身 {individual.shieldRemaining:F1}s";
            if (individual.cultivateRemaining > 0) yield return $"培育 {individual.cultivateRemaining:F1}s";
            if (individual.windupRemaining > 0) yield return $"{(individual.windupSkill == WorldSkill.None ? "普通冰箭" : SkillName(individual.windupSkill))} 前搖 {individual.windupRemaining:F1}s";
            foreach (var bias in individual.movementBiases.Where(value => value.until > world.State.time))
                yield return $"{SkillName(bias.skill)} 偏向 {bias.until - world.State.time:F1}s{(individual.rootRemaining > 0 ? "（定身暫緩）" : "")}";
        }

        private string ModeName() => mode switch
        {
            "replay" => "歷史回放・唯讀", "training" => "學習更新", "fixed" => "固定模型・不更新", "rules" => "規則控制", "idle" => "靜止控制",
            "scripted" => "指定動作", _ => "手動控制"
        };

        private static string DeathName(string cause) => cause == "old_age" ? "老死" : cause == "starved" ? "飢餓" : "戰鬥";
        private static string ScanReason(string reason) => reason switch
        {
            ScanReasons.Energy => "營養不足", ScanReasons.Escape => "撤退中", ScanReasons.Threat => "近迫威脅", _ => reason
        };
        private static string SpeciesName(string species) => species == "Flame" ? "炎角獸" : species == "Frost" ? "霜羽獸" : "苔靈";
        private static string EffectName(string kind) => kind switch
        {
            "trap_triggered" => "陷阱定身", "poison_applied" => "中毒", "poison_damage" => "毒傷",
            "charge_hit" => "衝鋒命中", "hot_strike_hit" => "猛擊命中", "frostbind_hit" => "霜縛箭命中",
            "piercing_hit" => "穿透冰矢命中", _ => "命中"
        };
        private static void DrawLedger(ResourceLedger ledger)
        {
            if (ledger == null) return;
            string[] labels = { "健康", "體力", "魔力", "飽食" };
            for (int i = 0; i < 4; i++)
                GUILayout.Label($"{labels[i]} {ledger.opening[i]:F1} +{ledger.income[i]:F1} −{ledger.expense[i]:F1} = {ledger.closing[i]:F1}");
        }

        private static void DrawRecoveries(float[] automatic, float[] nutrition)
        {
            GUILayout.Label($"自動恢復 健{automatic[0]:F1}／體{automatic[1]:F1}／魔{automatic[2]:F1}\n營養恢復 健{nutrition[0]:F1}／體{nutrition[1]:F1}／魔{nutrition[2]:F1}");
        }

        private void DrawDeath(DeathRecord death)
        {
            GUILayout.BeginArea(inspector, GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            int index = world.State.deaths.IndexOf(death);
            GUILayout.BeginHorizontal();
            if (index > 0 && GUILayout.Button("上一筆")) selected = world.State.deaths[index - 1].individual;
            if (index + 1 < world.State.deaths.Count && GUILayout.Button("下一筆")) selected = world.State.deaths[index + 1].individual;
            GUILayout.EndHorizontal();
            GUILayout.Label($"#{death.individual}　{SpeciesName(death.species)}・{DeathName(death.cause)}");
            GUILayout.Label($"{(death.initialAdult ? "起始成年" : "本輪出生")}　年齡 {death.age:F1}s\n觀測存活 {death.observedSeconds:F1}s　死亡 {death.time:F1}s");
            GUILayout.Label($"戰鬥傷害 {death.combatDamage:F1}　飢餓傷害 {death.starvationDamage:F1}\n最近戰鬥 {death.lastCombatDamageKind}　來源 #{death.lastCombatDamageSource}");
            GUILayout.Label($"最近進食 {death.lastAteAt:F1}s　繁殖 {death.lastReproducedAt:F1}s\n營養恢復 {death.lastNutritionAt:F1}s　{DecisionName(death.decisionReason)}");
            DrawLedger(death.ledger);
            DrawRecoveries(death.automaticRecovered, death.nutritionRecovered);
            GUILayout.Label($"基礎 {death.basalSpent:F1}　進食 {death.foodReceived:F1}　營養支出 {death.nutritionSpent:F1}\n活動體力 {death.activityStaminaSpent:F1}　技能魔力 {death.skillManaSpent:F1}　繁殖 {death.reproductionSpent:F1}");
            if (death.history != null)
                foreach (var sample in death.history)
                {
                    string text = $"{sample.time:F1}s　{sample.resources[0]:F1}/{sample.resources[1]:F1}/{sample.resources[2]:F1}/{sample.resources[3]:F1}　{DecisionName(sample.decision)}\n{(sample.running ? "跑" : "走")}　戰鬥 {sample.combatDamage:F1}　飢餓 {sample.starvationDamage:F1}　{string.Join("/", sample.recoveryStatus)}";
                    if (replay != null)
                    {
                        if (GUILayout.Button(text)) seek(Mathf.Clamp(Mathf.RoundToInt(sample.time / world.Config.stepSeconds), 0, replay.Count - 1));
                    }
                    else GUILayout.Label(text);
                }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static string DecisionName(string reason) => reason switch
        {
            "smoke_exit" => "離開煙幕", "smoke_avoid" => "繞避煙幕", "smoke_cross" => "緊急穿越",
            "protect_child" => "保護子女", "protection_retreat" => "保護撤退",
            "search_child" => "尋找子女",
            "approach_parent" => "靠近親代",
            "care_feed" => "餵養", "explore" => "探索", "food_prediction" => "回訪推測", "food_lost" => "來源耗盡", "forage" => "覓食", "feed" => "進食",
            "contest" => "爭食", "contest_approach" => "接近補給", "switch_food" => "換點",
            "retreat" => "撤退", "rest" => "休息", "walk_recover" => "步行恢復", _ => reason
        };

        private static string SkillName(WorldSkill skill) => skill switch
        {
            WorldSkill.Charge => "烈焰衝鋒", WorldSkill.HotStrike => "熾熱猛擊", WorldSkill.Shield => "防禦姿態",
            WorldSkill.IntimidatingRoar => "威嚇咆哮", WorldSkill.FrostbindArrow => "霜縛箭", WorldSkill.PiercingIceArrow => "穿透冰矢",
            WorldSkill.ColdMistDash => "寒霧遁行", WorldSkill.FrostTrap => "冰霜陷阱", WorldSkill.Cultivate => "培育",
            WorldSkill.Sow => "播種", WorldSkill.EntanglingRoots => "根鬚纏繞", WorldSkill.SporeSmoke => "孢子煙幕", _ => ""
        };

        private void OnDestroy()
        {
            if (overlayMaterial != null) Destroy(overlayMaterial);
            if (font != null) Destroy(font);
            Time.timeScale = 1;
        }
    }
}
