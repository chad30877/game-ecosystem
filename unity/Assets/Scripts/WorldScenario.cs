using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ecosystem
{
    [Serializable]
    public sealed class WorldScenario
    {
        public Placement[] placements = Array.Empty<Placement>();
        public FoodPlacement[] foods = Array.Empty<FoodPlacement>();
        public Command[] commands = Array.Empty<Command>();
        [NonSerialized] private Dictionary<int, int> slots;

        public void Apply(World world)
        {
            if (placements == null || foods == null || commands == null) throw new ArgumentException("場景配置不可為 null。");
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
                if (placement.setEnergy)
                {
                    if (!float.IsFinite(placement.energy) || placement.energy < 0 || placement.energy > 100) throw new ArgumentException("場景能量無效。");
                    individual.energy = placement.energy;
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
                if (placement.setAmount) food.amount = placement.amount;
                if (placement.setElapsedAge) food.bornAt = -placement.elapsedAge;
            }
            for (int i = 0; i < commands.Length; i++)
            {
                var command = commands[i];
                if (command.slot < 1 || command.slot > count || command.fromStep < 1 || command.toStep < command.fromStep ||
                    (command.turn < -1 || command.turn > 1) || command.skillSlot < -1 || command.skillSlot > 1 || !float.IsFinite(command.moveX) || !float.IsFinite(command.moveY) ||
                    !float.IsFinite(command.aimX) || !float.IsFinite(command.aimY) ||
                    !Enum.TryParse<WorldSkill>(command.skill, out var skill) || !Enum.IsDefined(typeof(WorldSkill), skill))
                    throw new ArgumentException("指定動作的個體、步數、方向或技能無效。");
                for (int j = 0; j < i; j++)
                    if (commands[j].slot == command.slot && command.fromStep <= commands[j].toStep && command.toStep >= commands[j].fromStep)
                        throw new ArgumentException("同一個體的指定動作區間不可重疊。");
            }
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
                            new Vector2(command.aimX, command.aimY), command.attack, Enum.Parse<WorldSkill>(command.skill), command.turn, command.skillSlot);
            return new WorldAction(Vector2.zero);
        }

        [Serializable]
        public sealed class Placement { public int slot; public float x; public float y; public float facing; public bool setEnergy; public float energy; public string[] loadout; }
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
            public bool attack;
            public int turn;
            public int skillSlot = -1;
            public string skill = "None";
        }
    }
}
