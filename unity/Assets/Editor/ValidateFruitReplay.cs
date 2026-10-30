using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
namespace Ecosystem.Editor
{
    public static class ValidateFruitReplay
    {
        static void Require(bool value,string message) { if(!value) throw new Exception(message); }
        public static void RunLegacyActions()
        {
            var world=new World(new WorldConfig {foraging=true,halfExtent=20,naturalFoodLimit=0},101);world.Reset();
            var actor=world.State.individuals[0];
            var actions=world.State.individuals.ToDictionary(a=>a.individual,a=>new WorldAction(Vector2.zero,feedChild:actor.individual));
            bool rejected=false;try {world.Step(actions);}catch(ArgumentException){rejected=true;}
            Require(rejected,"舊直接餵養動作須明確拒絕，不得靜默忽略");
            Debug.Log("舊直接餵養入口拒絕通過");
        }
        public static void RunLegacyScenario()
        {
            var world=new World(new WorldConfig {foraging=true,halfExtent=20},101);world.Reset();
            var scenario=JsonUtility.FromJson<WorldScenario>("{\"foods\":[{\"food\":1,\"setAmount\":true,\"amount\":1}]}");
            bool rejected=false;try {scenario.Apply(world);}catch(ArgumentException){rejected=true;}
            Require(rejected,"舊連續存量設定須明確拒絕，不得被下一步覆寫");
            Debug.Log("舊連續食物場景拒絕通過");
        }
        public static void RunScenarioAge()
        {
            var world=new World(new WorldConfig {foraging=true,halfExtent=20},101);world.Reset();
            var source=world.State.foods.First(f=>world.State.fruits.Any(item=>item.source==f.food));
            new WorldScenario {foods=new[] {new WorldScenario.FoodPlacement {food=source.food,setElapsedAge=true,elapsedAge=0}}}.Apply(world);
            Require(source.phase==FoodPhase.Recovering && !world.State.fruits.Any(f=>f.source==source.food),"指定來源零歲必須重建為零果首次成長");
            Require(world.State.fruitLedger.Residual(world.State.fruits.Count)==0,"場景重設後初始果實守恆");
            Debug.Log("來源年齡場景重建通過");
        }
        public static void RunScenarioActions()
        {
            var world=new World(new WorldConfig {foraging=true,halfExtent=20,naturalFoodLimit=0,idleCost=0,disableAging=true,disableReproduction=true,episodeSteps=200},101);world.Reset();
            var scenario=JsonUtility.FromJson<WorldScenario>("{\"fruits\":[{\"fruit\":9001,\"holderSlot\":1,\"ageSeconds\":5}],\"commands\":[{\"slot\":1,\"fromStep\":1,\"toStep\":10,\"foodAction\":\"Drop\",\"fruit\":9001},{\"slot\":1,\"fromStep\":11,\"toStep\":35,\"foodAction\":\"Pickup\",\"fruit\":9001},{\"slot\":1,\"fromStep\":36,\"toStep\":135,\"foodAction\":\"Eat\",\"fruit\":9001}]}");
            scenario.Apply(world);var actor=world.State.individuals[0];
            Require(world.State.fruits.Count(f=>f.holder==actor.individual)==1,"逐顆場景必須建立持果");
            void Until(int step) {while(world.State.step<step)world.Step(world.State.individuals.ToDictionary(a=>a.individual,a=>scenario.Decide(a.individual,world.State.step+1)));}
            Until(10);Require(world.State.fruits.Single().holder==0 && world.State.fruits.Single().expiresAt==35,"指定放下保留原保存期限");
            Until(35);Require(world.State.fruits.Single().holder==actor.individual,"指定拾取半秒完成");
            Until(135);Require(world.State.fruits.Count==0 && actor.satiety==80 && world.State.fruitLedger.Residual(0)==0,"指定完整進食才取得二十營養且守恆");
            Debug.Log("逐顆果實與指定操作場景通過");
        }
        public static void RunAgentInput()
        {
            var world=new World(new WorldConfig {foraging=true,halfExtent=20,naturalFoodLimit=0},101);world.Reset();
            var actor=world.State.individuals[0];world.State.fruits.Add(new FruitState {fruit=7001,holder=actor.individual,expiresAt=40});world.RefreshPerception();
            var gameObject=new GameObject("輸入契約");gameObject.SetActive(false);
            var agent=gameObject.AddComponent<WorldAgent>();agent.World=world;agent.Individual=actor;
            agent.OnActionReceived(new Unity.MLAgents.Actuators.ActionBuffers(
                new Unity.MLAgents.Actuators.ActionSegment<float>(new float[4]),
                new Unity.MLAgents.Actuators.ActionSegment<int>(new[] {1,0,0,0,0,6,1})));
            Require(agent.Action.foodAction==FoodAction.Drop && agent.Action.fruit==7001,"新版七分支須明確送出所選果實放下動作");
            UnityEngine.Object.DestroyImmediate(gameObject);
            Debug.Log("新版個體動作輸入通過");
        }
        public static void RunSourceFixture()
        {
            var world=new World(new WorldConfig {foraging=true,halfExtent=20,idleCost=0,disableAging=true,disableReproduction=true,episodeSteps=1200},101);world.Reset();
            var scenario=new WorldScenario {foods=new[] {new WorldScenario.FoodPlacement {food=1,setElapsedAge=true,elapsedAge=55}},
                fruits=Enumerable.Range(1,7).Select(n=>new WorldScenario.FruitPlacement {fruit=9000+n,source=1,ageSeconds=5}).ToArray()};
            scenario.Apply(world);
            while(world.State.step<1000)world.Step(world.State.individuals.ToDictionary(a=>a.individual,a=>new WorldAction(Vector2.zero)));
            Require(world.State.fruits.Count(f=>f.source==1)==7 && world.State.foods[0].phase==FoodPhase.Waiting,"完整來源果實配置不得追加當輪果數或突破容量");
            Debug.Log("來源逐顆配置容量通過");
        }
        public static void RunContracts() { Run(); RunLegacyActions(); RunLegacyScenario(); RunScenarioAge(); RunScenarioActions(); RunAgentInput(); RunSourceFixture(); }
        public static void RunScenarioGeometry()
        {
            var world=new World(new WorldConfig {foraging=true,halfExtent=20},101);world.Reset();
            var other=world.State.foods[1];
            var scenario=new WorldScenario {foods=new[] {new WorldScenario.FoodPlacement {food=1,relocate=true,x=other.x,y=other.y}}};
            bool rejected=false;try {scenario.Apply(world);}catch(ArgumentException){rejected=true;}
            Require(rejected,"指定場景不得繞過來源生長圓合法性");
            Debug.Log("來源場景合法空間通過");
        }
        public static void RunLegacy()
        {
            var args=Environment.GetCommandLineArgs();int option=Array.IndexOf(args,"--legacy-list");
            if(option<0) throw new Exception("請以 --legacy-list 提供歷史回放清單。");
            foreach(string path in File.ReadAllLines(args[option+1]))
            {
                using var replay=new WorldReplay(path);
                var raw=JsonUtility.FromJson<WorldReplay.Record>(File.ReadLines(path).Skip(1).First()).state;
                var first=replay.Read(0);string saved=JsonUtility.ToJson(first);
                Require(!first.fruitInformationAvailable && first.fruits.Count==0 && first.individuals.All(a=>!a.fruitKnowledgeAvailable),"歷史回放不得補造果實資訊");
                Require(first.foods.Count==raw.foods.Count && first.foods.Zip(raw.foods,(a,b)=>a.food==b.food && a.amount==b.amount && a.phase==b.phase && a.x==b.x && a.y==b.y).All(v=>v),"歷史來源存量、階段及位置保留");
                replay.Read(replay.Count-1);Require(JsonUtility.ToJson(replay.Read(0))==saved,"歷史回放雙向跳轉穩定");
                Debug.Log("歷史回放通過："+replay.Header.rules+"，影格 "+replay.Count);
            }
        }
        public static void RunInputBoundaries()
        {
            var world=new World(new WorldConfig {foraging=true,halfExtent=20,naturalFoodLimit=0},101);world.Reset();
            var actor=world.State.individuals[0];var gameObject=new GameObject("舊輸入拒絕");gameObject.SetActive(false);
            var agent=gameObject.AddComponent<WorldAgent>();agent.World=world;agent.Individual=actor;
            bool rejected=false;try {agent.OnActionReceived(new Unity.MLAgents.Actuators.ActionBuffers(
                new Unity.MLAgents.Actuators.ActionSegment<float>(new float[4]),new Unity.MLAgents.Actuators.ActionSegment<int>(new int[6])));}catch(ArgumentException){rejected=true;}
            Require(rejected,"舊六分支必須明確拒絕");
            world.State.fruits.Add(new FruitState {fruit=7001,holder=actor.individual,expiresAt=40});world.RefreshPerception();
            var first=WorldInput.Decode(world,actor,Vector2.zero,Vector2.zero,0,-1,false,false,false,FoodAction.Eat,0);
            world.State.fruits[0].expiresAt=11;
            var twin=WorldInput.Decode(world,actor,Vector2.zero,Vector2.zero,0,-1,false,false,false,FoodAction.Eat,0);
            Require(first.fruit==twin.fruit && first.foodAction==twin.foodAction,"輸入映射不讀精確保存真值");
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
        public static void BuildAndValidate()
        {
            RunContracts();RunScenarioGeometry();RunInputBoundaries();RunLegacy();ValidateProductionAI.RunAll();BuildWorld.Build();
            Debug.Log("果實資料契約、歷史回放、01至09回歸與建置通過");
        }
        public static void Run()
        {
            var folder=Path.GetFullPath("../artifacts/fruit-supply/11");Directory.CreateDirectory(folder);
            var config=new WorldConfig {foraging=true,halfExtent=20,naturalFoodLimit=0,disableAging=true,disableReproduction=true};
            var world=new World(config,101);world.Reset();
            var actor=world.State.individuals[0];
            world.State.fruits.Add(new FruitState {fruit=1,holder=actor.individual,expiresAt=40,claimedBy=actor.individual});
            world.State.fruitLedger.initial=1;actor.foodOperation=FoodAction.Eat;actor.operationFruit=1;
            world.RefreshPerception();
            string first=JsonUtility.ToJson(world.State);
            string path=Path.Combine(folder,"contract.replay.jsonl");
            var header=new WorldReplay.Record {kind="replay",schemaVersion=2,actionVersion=2,rules="survival-v15",config=config,episode=world.State.episode,seed=101};
            string body=JsonUtility.ToJson(header)+"\n"+JsonUtility.ToJson(new WorldReplay.Record {kind="frame",state=world.State})+"\n";
            world.State.step=1;world.State.time=.02f;actor.operationProgress=.02f;
            string second=JsonUtility.ToJson(world.State);
            body+=JsonUtility.ToJson(new WorldReplay.Record {kind="frame",state=world.State})+"\n";
            string hash=BitConverter.ToString(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(body))).Replace("-","").ToLowerInvariant();
            File.WriteAllText(path,body+JsonUtility.ToJson(new WorldReplay.Record {kind="complete",frames=2,sha256=hash})+"\n",new UTF8Encoding(false));
            using(var replay=new WorldReplay(path)) {
                Require(JsonUtility.ToJson(replay.Read(1))==second,"新回放保留完整第二張快照");
                Require(JsonUtility.ToJson(replay.Read(0))==first,"向後跳轉不重算感知或改變果實");
                Require(JsonUtility.ToJson(replay.Read(1))==second,"重複向前跳轉一致");
            }
            Debug.Log("果實回放公開快照契約通過");
        }
    }
}
