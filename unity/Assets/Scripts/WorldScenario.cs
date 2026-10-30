using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ecosystem
{
    [Serializable]
    public sealed class WorldScenario
    {
        public int actionVersion = WorldRules.ActionVersion;
        public FruitPlacement[] fruits = Array.Empty<FruitPlacement>();
        public Placement[] placements = Array.Empty<Placement>();
        public FoodPlacement[] foods = Array.Empty<FoodPlacement>();
        public Command[] commands = Array.Empty<Command>();
        [NonSerialized] private Dictionary<int, int> slots;

        public void Apply(World world)
        {
            if (actionVersion != WorldRules.ActionVersion) throw new ArgumentException("指定動作版本不相容，須使用 actionVersion=2。");
            if (placements == null || foods == null || commands == null || fruits == null) throw new ArgumentException("場景配置不可為 null。");
            if (foods.Any(p => p.setAmount)) throw new ArgumentException("survival-v15 不接受 setAmount 連續食物；請使用逐顆 fruits 配置。");
            if (commands.Any(c => c.feedChild != 0)) throw new ArgumentException("survival-v15 不接受 feedChild；請使用 Deliver／Receive 與 recipientSlot。");
            int count = world.State.individuals.Count;
            var placed = new HashSet<int>();
            foreach (var placement in placements)
            {
                if (placement.slot < 1 || placement.slot > count || !placed.Add(placement.slot) ||
                    !float.IsFinite(placement.facing) || !float.IsFinite(placement.x) || !float.IsFinite(placement.y) ||
                    Mathf.Abs(placement.x) > world.Config.halfExtent || Mathf.Abs(placement.y) > world.Config.halfExtent)
                    throw new ArgumentException("場景位置須在邊界內，且起始個體 slot 不可重複。");
                var individual = world.State.individuals[placement.slot - 1];
                individual.x = placement.x;
                individual.y = placement.y;
                individual.facing = Mathf.Repeat(placement.facing, 360f);
                if (placement.setSatiety)
                {
                    if (!float.IsFinite(placement.satiety) || placement.satiety < 0 || placement.satiety > 100) throw new ArgumentException("場景能量無效。");
                    individual.satiety = placement.satiety;
                }
                if (placement.setAge)
                {
                    if (!float.IsFinite(placement.age) || placement.age < 0 || placement.age >= world.Config.lifespan) throw new ArgumentException("場景年齡無效。");
                    individual.age = placement.age; individual.bornAt = world.State.time - placement.age;
                    individual.initialAdult = placement.age >= world.Config.maturityAge;
                    individual.health = CombatRules.CurrentMaxHealth(world.Config, individual) * world.Config.initialHealthFraction;
                    individual.growth = CombatRules.Growth(world.Config, individual); individual.maxHealth = CombatRules.CurrentMaxHealth(world.Config, individual);
                }
                if (placement.setVitals)
                {
                    if (!float.IsFinite(placement.health) || placement.health <= 0 || placement.health > CombatRules.CurrentMaxHealth(world.Config, individual) || !float.IsFinite(placement.stamina) || placement.stamina < 0 || placement.stamina > 100 || !float.IsFinite(placement.mana) || placement.mana < 0 || placement.mana > 100) throw new ArgumentException("場景資源無效。");
                    individual.health = placement.health; individual.stamina = placement.stamina; individual.mana = placement.mana;
                    if (placement.nutritionActive != null)
                    {
                        if (placement.nutritionActive.Length != 3) throw new ArgumentException("恢復狀態須包含健康、體力、魔力。");
                        individual.nutritionActive = (bool[])placement.nutritionActive.Clone();
                    }
                }
                if (placement.loadout != null && placement.loadout.Length > 0)
                {
                    if (placement.loadout.Length != 2) throw new ArgumentException("配置須恰有兩個技能。");
                    var skills = new WorldSkill[2];
                    for (int slot = 0; slot < 2; slot++)
                        if (!Enum.TryParse(placement.loadout[slot], out skills[slot]) || !World.IsSpeciesSkill(individual.species, skills[slot]))
                            throw new ArgumentException("配置必須來自所屬物種技能池。");
                    if (skills[0] == skills[1]) throw new ArgumentException("配置技能不可重複。");
                    individual.loadout = skills;
                }
            }
            foreach (var placement in placements)
            {
                if (placement.parentSlot == 0) continue;
                if (placement.parentSlot < 1 || placement.parentSlot > count || placement.parentSlot == placement.slot)
                    throw new ArgumentException("場景親代無效。");
                var child = world.State.individuals[placement.slot - 1];
                var parent = world.State.individuals[placement.parentSlot - 1];
                if (child.species != parent.species || child.age >= world.Config.maturityAge)
                    throw new ArgumentException("場景親子需同物種且子女為幼體。");
                child.parentId = parent.individual;
                parent.children.Add(new FamilyMemory { child = child.individual, x = parent.x, y = parent.y,
                    age = child.age, satiety = world.Config.newbornSatiety, lastSeenAt = world.State.time });
            }
            var placedFoods = new HashSet<int>();
            foreach (var placement in foods)
            {
                var food = world.State.foods.Find(value => value.food == placement.food);
                if (food == null || !placedFoods.Add(placement.food) ||
                    (placement.setAmount && (!float.IsFinite(placement.amount) || placement.amount < 0 || placement.amount > food.capacity)) ||
                    (placement.setElapsedAge && (!float.IsFinite(placement.elapsedAge) || placement.elapsedAge < 0)))
                    throw new ArgumentException("場景食物設定無效。");
                if (placement.relocate)
                {
                    if (!float.IsFinite(placement.x) || !float.IsFinite(placement.y) ||
                        Mathf.Abs(placement.x) > world.Config.halfExtent || Mathf.Abs(placement.y) > world.Config.halfExtent)
                        throw new ArgumentException("場景食物位置超出邊界。");
                    food.x = placement.x;
                    food.y = placement.y;
                }
                if (placement.setElapsedAge)
                {
                    if (placement.elapsedAge >= FruitRules.Growth(world.Config, food).lifetimeSeconds)
                        throw new ArgumentException("來源年齡須小於來源壽命。");
                    world.State.fruits.RemoveAll(f => f.source == food.food);
                    food.cycle = 0; food.lastFruitAt = -1000;
                    FruitRules.Initialize(world.Config, food, world.State.time - placement.elapsedAge);
                }
                foreach (var fruit in world.State.fruits.Where(f => f.source == food.food)) { fruit.x = food.x; fruit.y = food.y; }
            }
            if (foods.Any(p => p.setElapsedAge))
            {
                FruitRules.Advance(world.Config, world.State, new System.Random(world.Seed ^ 0x531f1), world.State.time);
                world.State.fruitLedger = new FruitLedger { initial = world.State.fruits.Count };
                world.State.events.Clear();
            }
            foreach (var source in world.State.foods)
            {
                float radius = SourcePlacementRules.Radius(world.Config, source.growthType);
                if (Mathf.Abs(source.x) + radius > world.Config.halfExtent || Mathf.Abs(source.y) + radius > world.Config.halfExtent ||
                    world.State.foods.Any(other => other.food != source.food && Vector2.Distance(new Vector2(source.x, source.y),
                        new Vector2(other.x, other.y)) < radius + SourcePlacementRules.Radius(world.Config, other.growthType)))
                    throw new ArgumentException("場景來源生長圓必須完整在界內且互不重疊。");
            }
            foreach (int sourceId in fruits.Where(f => f.source != 0).Select(f => f.source).Distinct())
                world.State.fruits.RemoveAll(f => f.source == sourceId);
            foreach (var placement in fruits)
            {
                if (placement.fruit < 0 || placement.source < 0 || placement.holderSlot < 0 || placement.holderSlot > count ||
                    !float.IsFinite(placement.ageSeconds) || placement.ageSeconds < 0 || placement.ageSeconds >= FruitRules.PreservationSeconds ||
                    !float.IsFinite(placement.x) || !float.IsFinite(placement.y) ||
                    placement.source != 0 && placement.holderSlot != 0)
                    throw new ArgumentException("逐顆果實配置的識別、位置或保存年齡無效。");
                var source = world.State.foods.Find(f => f.food == placement.source);
                var holder = placement.holderSlot == 0 ? null : world.State.individuals[placement.holderSlot - 1];
                if (placement.source != 0 && (source == null || world.State.fruits.Count(f => f.source == source.food) >= source.capacity) ||
                    holder != null && FoodOperations.Carried(world.State, holder) >= FoodOperations.Capacity(world.Config, holder))
                    throw new ArgumentException("逐顆果實配置的來源或攜帶容量無效。");
                int id = placement.fruit == 0 ? world.State.nextFruit + 1 : placement.fruit;
                if (world.State.fruits.Any(f => f.fruit == id)) throw new ArgumentException("果實識別不可重複。");
                float x = holder != null ? holder.x : source != null ? source.x : placement.x;
                float y = holder != null ? holder.y : source != null ? source.y : placement.y;
                if (Mathf.Abs(x) > world.Config.halfExtent || Mathf.Abs(y) > world.Config.halfExtent)
                    throw new ArgumentException("地面果實位置超出世界。");
                world.State.nextFruit = Math.Max(world.State.nextFruit, id);
                world.State.fruits.Add(new FruitState { fruit = id, source = placement.source,
                    holder = holder?.individual ?? 0, x = x, y = y, maturedAt = world.State.time - placement.ageSeconds,
                    expiresAt = world.State.time - placement.ageSeconds + FruitRules.PreservationSeconds });
            }
            foreach (var source in world.State.foods.Where(s => fruits.Any(f => f.source == s.food)))
            {
                var configured = world.State.fruits.Where(f => f.source == source.food).ToArray();
                source.phase = FoodPhase.Waiting; source.mature = true; source.cycle = Math.Max(1, source.cycle);
                source.nextMaturity = source.maturitySchedule.Length;
                source.lastFruitAt = configured.Max(f => f.maturedAt); source.waitUntil = configured.Max(f => f.expiresAt);
            }
            if (fruits.Length > 0) world.State.fruitLedger = new FruitLedger { initial = world.State.fruits.Count };
            foreach (var source in world.State.foods) source.amount = world.State.fruits.Count(f => f.source == source.food);
            for (int i = 0; i < commands.Length; i++)
            {
                var command = commands[i];
                if (command.slot < 1 || command.slot > count || command.fromStep < 1 || command.toStep < command.fromStep ||
                    (command.turn < -1 || command.turn > 1) || command.skillSlot < -1 || command.skillSlot > 1 || command.feedChild < 0 || command.feedChild > count || command.fruit < 0 || command.productionSource < 0 ||
                    command.recipientSlot < 0 || command.recipientSlot > count ||
                    !Enum.TryParse<FoodAction>(command.foodAction, out var foodAction) || !Enum.IsDefined(typeof(FoodAction), foodAction) || !float.IsFinite(command.moveX) || !float.IsFinite(command.moveY) ||
                    !float.IsFinite(command.aimX) || !float.IsFinite(command.aimY) ||
                    !Enum.TryParse<WorldSkill>(command.skill, out var skill) || !Enum.IsDefined(typeof(WorldSkill), skill))
                    throw new ArgumentException("指定動作的個體、步數、方向或技能無效。");
                for (int j = 0; j < i; j++)
                    if (commands[j].slot == command.slot && command.fromStep <= commands[j].toStep && command.toStep >= commands[j].fromStep)
                        throw new ArgumentException("同一個體的指定動作區間不可重疊。");
            }
            foreach (var individual in world.State.individuals) World.CaptureResourceBaseline(individual, world.State.time, true);
            world.RefreshPerception(clearMemory: true);
            slots = new Dictionary<int, int>();
            for (int i = 0; i < count; i++) slots.Add(world.State.individuals[i].individual, i + 1);
        }

        public WorldAction Decide(int individual, int step)
        {
            if (slots.TryGetValue(individual, out int slot))
                foreach (var command in commands)
                    if (command.slot == slot && step >= command.fromStep && step <= command.toStep)
                        return new WorldAction(new Vector2(command.moveX, command.moveY), command.forage,
                            new Vector2(command.aimX, command.aimY), command.attack, Enum.Parse<WorldSkill>(command.skill), command.turn, command.skillSlot, command.run, foodAction: Enum.Parse<FoodAction>(command.foodAction),
                            fruit: command.fruit, recipient: command.recipientSlot == 0 ? 0 : slots.First(value => value.Value == command.recipientSlot).Key,
                            productionSource: command.productionSource);
            return new WorldAction(Vector2.zero);
        }

        [Serializable]
        public sealed class Placement { public int slot; public int parentSlot; public float x; public float y; public float facing; public bool setAge; public float age; public bool setSatiety; public float satiety; public bool[] nutritionActive; public bool setVitals; public float health, stamina, mana; public string[] loadout; }
        [Serializable]
        public sealed class FruitPlacement
        {
            public int fruit, source, holderSlot;
            public float x, y, ageSeconds;
        }
        [Serializable]
        public sealed class FoodPlacement
        {
            public int food;
            public bool relocate;
            public float x, y;
            public bool setAmount;
            public float amount;
            public bool setElapsedAge;
            public float elapsedAge;
        }
        [Serializable]
        public sealed class Command
        {
            public int slot;
            public int fromStep;
            public int toStep;
            public float moveX;
            public float moveY;
            public float aimX;
            public float aimY;
            public bool forage;
            public int feedChild;
            public string foodAction = "None";
            public int fruit, recipientSlot, productionSource;
            public bool run;
            public bool attack;
            public int turn;
            public int skillSlot = -1;
            public string skill = "None";
        }
    }
}
