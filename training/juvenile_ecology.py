"""以正式入口執行固定配置的親子照顧配對觀察。"""
import json, subprocess, time, sys
from pathlib import Path
from collections import Counter

root=Path(__file__).resolve().parents[1]
out=Path(sys.argv[1]).resolve(); out.mkdir(parents=True,exist_ok=False)
config=json.loads((root/'training/foraging.json').read_text(encoding='utf-8'))
config.update(individualsPerSpecies=2,episodeSteps=1500,disableReproduction=True,halfExtent=20,foodSourceLimit=16,naturalFoodLimit=3,naturalSpawnBatch=1,naturalSpawnInterval=30,foodPredictionSeconds=3)
python=Path(sys.executable)
placements=[]
for i,(x,y) in enumerate([(0,0),(0.5,0),(6,0),(6.5,0),(12,0),(12.5,0)]):
    p={'slot':i+1,'x':x,'y':y,'facing':0,'setSatiety':True,'satiety':70 if i%2==0 else 30}
    if i%2: p.update(setAge=True,age=1,parentSlot=i)
    placements.append(p)
results=[]
for scene in ('abundant','limited','threat'):
    layout=json.loads(json.dumps(placements))
    if scene=='threat':
        for idx,xy in enumerate([(0,0),(.5,0),(1.5,0),(8,8),(2.5,0),(12,12)]): layout[idx]['x'],layout[idx]['y']=xy
    cfg=dict(config); cfg['naturalFoodLimit']=3 if scene=='abundant' else 1
    scenario={'placements':layout,'foods':[{'food':i+1,'relocate':True,'x':(i*6 if scene=='abundant' else 0),'y':.5,'setAmount':True,'amount':30 if scene=='abundant' else 4} for i in range(cfg['naturalFoodLimit'])]}
    for seed in (101,102,103):
      for disabled in (False,True):
        name=f'{scene}-{seed}-'+('no-care' if disabled else 'care'); folder=out/name; folder.mkdir()
        setting={**cfg,'disableCare':disabled}; (folder/'config.json').write_text(json.dumps(setting,ensure_ascii=False,indent=2),encoding='utf-8'); (folder/'scenario.json').write_text(json.dumps(scenario,ensure_ascii=False,indent=2),encoding='utf-8')
        cmd=['pwsh','-NoProfile','-File',str(root/'scripts/world.ps1'),'-Mode','forage','-Controller','rules','-WorldConfig',str(folder/'config.json'),'-Scenario',str(folder/'scenario.json'),'-Seed',str(seed),'-Steps',str(cfg['episodeSteps']),'-RecordEpisodes','1','-OutputRoot',str(folder),' -Python'.strip(),str(python)]
        start=time.perf_counter(); subprocess.run(cmd,check=True,stdout=subprocess.DEVNULL); seconds=time.perf_counter()-start
        log=next(folder.glob('forage-*/record-source.jsonl')); events=Counter(); care=0.; seen={}; food=0.; reasons=Counter(); final=None; initial=None
        with log.open(encoding='utf-8-sig') as f:
          for line in f:
            rec=json.loads(line); state=rec.get('state');
            if not state or state['episode']!=1 or rec['kind'] not in ('reset','step'): continue
            if initial is None: initial=state
            if rec['kind']=='step':
              for e in state['events']:
                events[e['kind']]+=1
                if e['kind']=='care_feed': care+=e['amount']
                if e['kind']=='ate': food+=e['amount']
              for ind in state['individuals']:
                reasons[ind['decisionReason']]+=1
                seen[ind['individual']]=ind
            final=state
        result={'scene':scene,'seed':seed,'disableCare':disabled,'wallSeconds':seconds,'simulationSeconds':final['time'],'events':dict(events),'reasons':dict(reasons),'careGiven':care,'foodEaten':food,'foodInitial':sum(f['amount'] for f in initial['foods']),'foodFinal':sum(f['amount'] for f in final['foods']),'individuals':{str(k):{'species':v['species'],'parentId':v['parentId'],'health':v['health'],'satiety':v['satiety'],'careGiven':v['careGiven'],'careReceived':v['careReceived']} for k,v in seen.items()},'alive':[v['individual'] for v in final['individuals'] if v['health']>0]}
        results.append(result); (out/'results.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8'); print(name,flush=True)

