using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ecosystem
{
    // 一顆果實一個物件；共用材質，回放換頁與世界重建時同樣清理。
    public sealed class FruitWorldView : MonoBehaviour
    {
        sealed class Plant
        {
            public GameObject root, stem;
            public readonly List<GameObject> leaves = new List<GameObject>();
        }
        sealed class Fruit
        {
            public GameObject root, shine;
            public readonly List<GameObject> spots = new List<GameObject>();
        }
        readonly Dictionary<int, Plant> plants = new Dictionary<int, Plant>();
        readonly Dictionary<int, Fruit> fruits = new Dictionary<int, Fruit>();
        Material material;
        readonly MaterialPropertyBlock color = new MaterialPropertyBlock();
        World world;

        GameObject Part(Transform parent, string name, PrimitiveType shape, Vector3 position, Vector3 scale, Color tint)
        {
            if (material == null) material = new Material(Resources.Load<Material>("WorldMaterial"));
            var part = GameObject.CreatePrimitive(shape);
            part.name = name;
            Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            Tint(part, tint);
            return part;
        }
        void Tint(GameObject part, Color tint)
        {
            color.SetColor("_Color", tint);
            part.GetComponent<Renderer>().SetPropertyBlock(color);
        }
        Plant CreatePlant(int id)
        {
            var plant = new Plant { root = new GameObject("Source-" + id) };
            plant.root.transform.SetParent(transform, false);
            plant.stem = Part(plant.root.transform, "Stem", PrimitiveType.Cube, Vector3.zero, Vector3.one, Color.white);
            for (int i = 0; i < 4; i++)
                plant.leaves.Add(Part(plant.root.transform, "Leaf", PrimitiveType.Sphere, Vector3.zero, Vector3.one, Color.white));
            return plant;
        }
        Fruit CreateFruit(int id)
        {
            var fruit = new Fruit { root = Part(transform, "Fruit-" + id, PrimitiveType.Sphere, Vector3.zero, Vector3.one, Color.white) };
            fruit.shine = Part(fruit.root.transform, "Shine", PrimitiveType.Sphere, new Vector3(-.18f, .2f, -.5f), new Vector3(.22f, .22f, .08f), new Color(1, .96f, .72f));
            for (int i = 0; i < 3; i++)
                fruit.spots.Add(Part(fruit.root.transform, "RotSpot", PrimitiveType.Sphere,
                    new Vector3((i - 1) * .22f, i % 2 == 0 ? -.12f : .2f, -.5f), new Vector3(.2f, .16f, .08f), new Color(.23f, .16f, .08f)));
            return fruit;
        }
        public void Refresh(World source)
        {
            world = source;
            foreach (int id in plants.Keys.Where(id => !world.State.foods.Any(f => f.food == id)).ToArray())
            { Destroy(plants[id].root); plants.Remove(id); }
            foreach (int id in fruits.Keys.Where(id => !world.State.fruits.Any(f => f.fruit == id)).ToArray())
            { Destroy(fruits[id].root); fruits.Remove(id); }
            foreach (var food in world.State.foods)
            {
                if (!plants.TryGetValue(food.food, out var plant)) plants.Add(food.food, plant = CreatePlant(food.food));
                float size = food.growthType == FoodGrowthType.Large ? 1.35f : 1;
                plant.root.transform.position = new Vector3(food.x, food.y, .2f);
                plant.root.transform.localScale = Vector3.one * size;
                bool growing = food.phase == FoodPhase.Recovering, resting = food.phase == FoodPhase.Resting;
                bool waiting = food.phase == FoodPhase.Waiting;
                float height = resting ? .28f : growing ? .55f : waiting ? 1.05f : 1.25f;
                plant.stem.transform.localPosition = new Vector3(0, height * .3f - .2f, 0);
                plant.stem.transform.localScale = new Vector3(resting ? .22f : .12f, height, .08f);
                Tint(plant.stem, resting ? new Color(.4f, .3f, .22f) : new Color(.42f, .52f, .23f));
                Color foliage = growing ? new Color(.48f, .8f, .4f) : resting ? new Color(.35f, .3f, .24f)
                    : waiting ? new Color(.62f, .57f, .3f) : new Color(.23f, .65f, .36f);
                for (int i = 0; i < plant.leaves.Count; i++)
                {
                    var leaf = plant.leaves[i];
                    float side = i % 2 == 0 ? -1 : 1;
                    float spread = growing ? .22f : resting ? .2f : waiting ? .57f : .48f;
                    leaf.transform.localPosition = new Vector3(side * spread, resting ? -.22f : growing ? i / 2 * .23f : .1f + i / 2 * .55f, -.04f);
                    leaf.transform.localScale = new Vector3(growing ? .5f : resting ? .4f : .95f, growing ? .24f : resting ? .16f : .4f, .06f);
                    leaf.transform.localRotation = Quaternion.Euler(0, 0, side * (waiting ? -30 : resting ? -15 : 30));
                    Tint(leaf, foliage);
                }
            }
            foreach (var item in world.State.fruits)
            {
                if (!fruits.TryGetValue(item.fruit, out var fruit)) fruits.Add(item.fruit, fruit = CreateFruit(item.fruit));
                Vector3 position = new Vector3(item.x, item.y, -.12f);
                float diameter = .38f;
                if (item.holder != 0)
                {
                    var holder = world.State.individuals.Find(a => a.individual == item.holder);
                    if (holder == null) { fruit.root.SetActive(false); continue; }
                    var held = world.State.fruits.Where(f => f.holder == item.holder).OrderBy(f => f.fruit).ToList();
                    int index = held.FindIndex(f => f.fruit == item.fruit);
                    float angle = (140 + index * 65) * Mathf.Deg2Rad;
                    float radius = holder.age < world.Config.maturityAge ? .43f : .6f;
                    position = new Vector3(holder.x + Mathf.Cos(angle) * radius, holder.y + Mathf.Sin(angle) * radius, -.28f);
                    diameter = .25f;
                }
                else if (item.source != 0 && plants.TryGetValue(item.source, out var plant))
                {
                    // 位置只由當時快照決定，前後跳轉與逐格前進會得到相同畫面。
                    int index = world.State.fruits.Where(f => f.source == item.source).OrderBy(f => f.fruit)
                        .ToList().FindIndex(f => f.fruit == item.fruit);
                    Vector2[] offsets = { new Vector2(-.6f,.3f), new Vector2(.6f,.3f), new Vector2(-.35f,.88f),
                        new Vector2(.35f,.88f), new Vector2(-.82f,.82f), new Vector2(.82f,.82f), new Vector2(0,1.35f) };
                    var offset = offsets[index % offsets.Length] * plant.root.transform.localScale.x;
                    position = plant.root.transform.position + new Vector3(offset.x, offset.y, -.4f);
                }
                fruit.root.SetActive(true);
                fruit.root.transform.position = position;
                fruit.root.transform.localScale = new Vector3(diameter, diameter, .1f);
                bool old = item.NearRot(world.State.time);
                Tint(fruit.root, old ? new Color(.61f, .4f, .2f) : new Color(1f, .66f, .16f));
                fruit.shine.SetActive(!old);
                foreach (var spot in fruit.spots) spot.SetActive(old);
            }
        }
        public void SetFocus(IndividualState selected)
        {
            foreach (var entry in plants)
            {
                var source = world.State.foods.Find(f => f.food == entry.Key);
                entry.Value.root.SetActive(selected == null || world.CanSeePoint(selected, new Vector2(source.x, source.y)));
            }
            foreach (var entry in fruits)
            {
                var fruit = world.State.fruits.Find(f => f.fruit == entry.Key);
                var holder = fruit.holder == 0 ? null : world.State.individuals.Find(a => a.individual == fruit.holder);
                entry.Value.root.SetActive(selected == null || holder == selected || (holder != null ? world.CanSee(selected, holder)
                    : world.CanSeePoint(selected, new Vector2(fruit.x, fruit.y))));
            }
        }
        void OnDestroy() { if (material != null) Destroy(material); }
    }
}
