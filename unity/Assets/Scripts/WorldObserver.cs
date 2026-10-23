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

        public void Initialize(World source, Camera camera, string controlMode, Action onRebuild, Action onQuit, Action<IndividualState> onFocus, WorldReplay history = null, Action<int> onSeek = null)
        {
            world = source;
            replay = history;
            seek = onSeek;
            worldCamera = camera;
            mode = controlMode;
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
            if (Input.GetKeyDown(KeyCode.P)) SetPaused(!paused);
            var pointer = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            if (toolbar.Contains(pointer) || (selected != 0 && inspector.Contains(pointer))) return;
            float wheel = Input.mouseScrollDelta.y;
            if (wheel != 0)
            {
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
            feedback = "";
            scroll = Vector2.zero;
        }

        private void LateUpdate()
        {
            if (world == null) return;
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
                selected = 0;
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
            toolbar = new Rect(8, 8, Screen.width - 16, replay == null ? 100 : 140);
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
            if (GUILayout.Button("全景", GUILayout.Width(60)))
            {
                worldCamera.transform.position = new Vector3(0, 0, -10);
                worldCamera.orthographicSize = world.Config.halfExtent + 1;
                selected = 0;
            }
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
            if (individual == null) return;
            inspector = new Rect(Screen.width - Mathf.Min(360, Screen.width - 16) - 8, toolbar.yMax + 8, Mathf.Min(360, Screen.width - 16), Screen.height - toolbar.yMax - 16);
            GUILayout.BeginArea(inspector, GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label($"#{individual.individual}　{SpeciesName(individual.species)}");
            GUILayout.Label($"健康 {individual.health:F1} / {CombatRules.MaxHealth(individual.species):g}　飽食度 {individual.satiety:F1} / 100　體力 {individual.stamina:F1}　魔力 {individual.mana:F1}");
            GUILayout.Label($"營養恢復 {string.Join(" / ", individual.recoveryStatus)}　基礎 {individual.basalSpent:F1}　進食 {individual.foodReceived:F1}　營養支出 {individual.nutritionSpent:F1}");
            GUILayout.Label($"年齡 {individual.age:F1}　{(individual.age < world.Config.maturityAge ? "成長期" : "成年")}　繁殖 {individual.reproductionCooldown:F1}s");
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
            var reward = world.State.rewards.Find(value => value.individual == selected);
            if (reward != null) GUILayout.Label($"探索 {individual.rewardedCells.Count} 格　累計 {individual.explorationRewardTotal:F4}　此步 {reward.exploration:F4}");
            foreach (var food in individual.visibleFoods.Concat(individual.foodMemory.Where(value => !value.visible)))
                GUILayout.Label($"食物 ({food.x:F1}, {food.y:F1})　{(food.visible ? "可見" : world.State.time - food.lastSeenAt >= world.Config.foodMemorySeconds ? "舊情報" : "記憶")}　{(food.mature && food.phase == FoodPhase.Available ? "可採" : food.phase == FoodPhase.Resting ? "休養" : "成長中")}　{food.amount:F1}");
            foreach (var entity in individual.perception.Where(value => !value.visible))
                GUILayout.Label($"{SpeciesName(entity.species)} 最後位置 ({entity.x:F1}, {entity.y:F1})　{entity.age:F1}s 前");
            foreach (var target in world.State.individuals.Where(value => world.CanSee(individual, value)))
            {
                string status = string.Join("、", Statuses(target));
                if (status != "") GUILayout.Label($"{SpeciesName(target.species)} ({target.x:F1}, {target.y:F1})　{status}");
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            foreach (var memory in individual.perception.Where(value => !value.visible))
            {
                var point = worldCamera.WorldToScreenPoint(new Vector3(memory.x, memory.y, 0));
                var rect = new Rect(point.x - 40, Screen.height - point.y - 12, 100, 24);
                if (point.z > 0 && !toolbar.Overlaps(rect) && !inspector.Overlaps(rect)) GUI.Label(rect, $"◇ {SpeciesName(memory.species)}");
            }
            foreach (var food in individual.foodMemory.Where(value => !value.visible))
            {
                var point = worldCamera.WorldToScreenPoint(new Vector3(food.x, food.y, 0));
                var rect = new Rect(point.x - 40, Screen.height - point.y - 12, 100, 24);
                if (point.z > 0 && !toolbar.Overlaps(rect) && !inspector.Overlaps(rect)) GUI.Label(rect, "◇ 食物");
            }
        }

        private string Availability(IndividualState individual, int slot)
        {
            if (individual.skillCooldowns[slot] > 0) return $"冷卻 {individual.skillCooldowns[slot]:F1}s";
            if (individual.mana < SkillRules.EnergyCost(world.Config.combat, individual.loadout[slot])) return "魔力不足";
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
            ScanReasons.Energy => "能量不足", ScanReasons.Escape => "撤退中", ScanReasons.Threat => "近迫威脅", _ => reason
        };
        private static string SpeciesName(string species) => species == "Flame" ? "炎角獸" : species == "Frost" ? "霜羽獸" : "苔靈";
        private static string EffectName(string kind) => kind switch
        {
            "trap_triggered" => "陷阱定身", "poison_applied" => "中毒", "poison_damage" => "毒傷",
            "charge_hit" => "衝鋒命中", "hot_strike_hit" => "猛擊命中", "frostbind_hit" => "霜縛箭命中",
            "piercing_hit" => "穿透冰矢命中", _ => "命中"
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
