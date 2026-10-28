"""以相同世界配置量測記憶推測啟停後的整體執行時間。"""
import json, subprocess, sys, time, platform
from pathlib import Path
root=Path(__file__).resolve().parents[1]; out=Path(sys.argv[1]).resolve(); out.mkdir(parents=True,exist_ok=False)
config=json.loads((root/'training/foraging.json').read_text(encoding='utf-8')); config.update(individualsPerSpecies=10, episodeSteps=1000, foodPredictionSeconds=3, disableReproduction=True)
placements=[]
for i in range(30):
 parent=(i//10)*10+1
 place={'slot':i+1,'x':(i//10)*8+(i%10)*0.3,'y':0,'facing':0}
 if i+1!=parent: place.update(setAge=True,age=5,parentSlot=parent)
 placements.append(place)
scenario=out/'scenario.json'; scenario.write_text(json.dumps({'placements':placements},indent=2),encoding='utf-8')
results=[]
for seed in (101,102,103):
 for disabled in (False,True):
  name=f'{seed}-'+('off' if disabled else 'on'); folder=out/name; folder.mkdir(); cfg={**config,'disableMemoryPrediction':disabled}
  (folder/'config.json').write_text(json.dumps(cfg,indent=2),encoding='utf-8')
  cmd=['pwsh','-NoProfile','-File',str(root/'scripts/world.ps1'),'-Mode','forage','-Controller','rules','-WorldConfig',str(folder/'config.json'),'-Scenario',str(scenario),'-Seed',str(seed),'-Steps',str(config['episodeSteps']),'-RecordEpisodes','1','-OutputRoot',str(folder),'-Python',sys.executable]
  start=time.perf_counter(); subprocess.run(cmd,check=True,stdout=subprocess.DEVNULL); elapsed=time.perf_counter()-start
  log=next(folder.glob('forage-*/record-source.jsonl')); max_ind=max_food=max_family=max_area=0; steps=0; lasttime=0
  with log.open(encoding='utf-8-sig') as source:
   for line in source:
    r=json.loads(line); state=r.get('state')
    if not state or state['episode']!=1 or r['kind']!='step': continue
    steps+=1; lasttime=state['time']; inds=state['individuals']; max_ind=max(max_ind,len(inds)); max_food=max(max_food,sum(len(i.get('foodMemory',[])) for i in inds)); max_family=max(max_family,sum(len(i.get('children',[])) for i in inds)); max_area=max(max_area,sum(len(i.get('areaMemory',[])) for i in inds))
  results.append({'seed':seed,'predictionDisabled':disabled,'wallSeconds':elapsed,'steps':steps,'simulationSeconds':lasttime,'maxIndividuals':max_ind,'maxFoodMemories':max_food,'maxFamilyMemories':max_family,'maxAreaMemories':max_area,'logBytes':log.stat().st_size,'platform':platform.platform(),'python':platform.python_version()}); (out/'results.json').write_text(json.dumps(results,indent=2),encoding='utf-8'); print(name,flush=True)
