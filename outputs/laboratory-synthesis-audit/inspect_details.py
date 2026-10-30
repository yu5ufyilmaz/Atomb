from pathlib import Path
import bisect
import collections
import json
import re

root = Path.cwd()
out = root/'outputs/laboratory-synthesis-audit'
inventory = json.loads((out/'scene-inventory.json').read_text(encoding='utf-8'))
guidmap = json.loads((out/'guid-map.json').read_text(encoding='utf-8'))
src = (root/inventory['scene']).read_text(encoding='utf-8-sig')
heads = list(re.finditer(r'^--- !u!(\d+) &(-?\d+)[^\n]*\n', src, re.M))
newlines = [m.start() for m in re.finditer('\n',src)]
docs = {h[2]: {'id': h[2], 'type': int(h[1]), 'text': src[h.end(): heads[i+1].start() if i+1<len(heads) else len(src)], 'line': bisect.bisect_left(newlines,h.start())+1} for i,h in enumerate(heads)}
objects = {r['id']:r for r in inventory['objects']}
component_owner = {c['id']:r for r in inventory['objects'] for c in r['components']}

def value(text,key,default=None):
    m=re.search(r'^  '+re.escape(key)+r': (.*)$',text,re.M)
    return m[1] if m else default

def entry(d,keys):
    owner=component_owner.get(d['id'])
    return {'id':d['id'],'line':d['line'],'path':owner['path'] if owner else None,'active':owner['active'] if owner else None,**{k:value(d['text'],k) for k in keys}}

metrics={}
for name,typ,keys in [('lights',108,['m_Enabled','m_Type','m_Lightmapping','m_Intensity','m_Range']),('reflectionProbes',215,['m_Enabled','m_Mode','m_RefreshMode','m_Resolution']),('animators',95,['m_Enabled','m_CullingMode','m_ApplyRootMotion']),('meshRenderers',23,['m_Enabled','m_CastShadows','m_ReceiveShadows']),('skinnedRenderers',137,['m_Enabled','m_UpdateWhenOffscreen','m_CastShadows']),('meshColliders',64,['m_Enabled','m_Convex','m_IsTrigger']),('canvases',223,['m_Enabled','m_RenderMode']),('audioSources',82,['m_Enabled','m_PlayOnAwake','m_Loop'])]:
    metrics[name]=[entry(d,keys) for d in docs.values() if d['type']==typ]
    print(name,len(metrics[name]),'active+enabled',sum(e['active'] is True and e['m_Enabled']=='1' for e in metrics[name]))
    if name in ('lights','reflectionProbes','animators','meshColliders'):
        for key in keys[1:]:print(key,dict(collections.Counter(e[key] for e in metrics[name])))

missing_local=[]
for d in docs.values():
    for m in re.finditer(r'\{fileID: (-?\d+)\}',d['text']):
        if m[1]!='0' and m[1] not in docs:
            missing_local.append({'owner':d['id'],'ownerPath':component_owner.get(d['id'],objects.get(d['id'],{})).get('path'),'target':m[1],'line':d['line']+d['text'].count('\n',0,m.start())})
metrics['unresolvedLocalReferences']=missing_local
print('Unresolved local references',len(missing_local),missing_local[:20])

catalogs=[]
for r in objects.values():
    if any((c.get('script') or '').endswith('FormulaCatalog.cs') for c in r['components']):
        tids={r['id']}|{c['id'] for c in r['components']}
        inbound=[]
        for d in docs.values():
            if d['id'] in tids:continue
            refs=tids & set(re.findall(r'\{fileID: (-?\d+)\}',d['text']))
            if refs:inbound.append({'id':d['id'],'path':component_owner.get(d['id'],objects.get(d['id'],{})).get('path'),'type':d['type'],'targets':sorted(refs)})
        catalogs.append({'object':r,'inbound':inbound})
metrics['catalogs']=catalogs
print('Catalog inbound',[(c['object']['id'],c['inbound']) for c in catalogs])

metrics['legacySettings']=[]
for branch in ['Canvas/SettingMenuPanel','Canvas/PauseSettingMenuPanel','ENVIRONMENT/Symbols','ENVIRONMENT/WorkTableTestAssets']:
    rows=[r for r in objects.values() if r['path']==branch or r['path'].startswith(branch+'/')]
    print('Branch',branch,'objects',len(rows),'self active',sum(r['selfActive']=='1' for r in rows))
    metrics['legacySettings'].append({'path':branch,'objects':len(rows)})

metrics['unknownScripts']=[]
for r in objects.values():
    for c in r['components']:
        if (c.get('script') or '').startswith('<') or (c.get('type')==114 and not c.get('script')):
            metrics['unknownScripts'].append({'object':r['path'],'component':c,'text':docs[c['id']]['text'][:1500]})
            print('SCRIPT CHECK',r['path'],c['id'],c.get('script'))

metrics['missingSerializedScriptReferences']=[]
for d in docs.values():
    if d['type'] != 114:
        continue
    script=re.search(r'm_Script: \{fileID: (-?\d+), guid: ([0-9a-f]{32})',d['text'])
    if script and script[2] not in guidmap:
        owner=re.search(r'm_GameObject: \{fileID: (-?\d+)\}',d['text'])
        metrics['missingSerializedScriptReferences'].append({'component':d['id'],'owner':owner[1] if owner else None,'guid':script[2],'line':d['line']})
print('Missing serialized script references',metrics['missingSerializedScriptReferences'])

books=[r for r in objects.values() if any((c.get('script') or '').endswith('InteractableBook.cs') for c in r['components'])]
book_summary=[]
for r in books:
    c=next(c for c in r['components'] if (c.get('script') or '').endswith('InteractableBook.cs'))
    text=docs[c['id']]['text']
    book_summary.append({'path':r['path'],'active':r['active'],'line':c['line'],**{k:value(text,k) for k in ['canContainPassword','usePhysicalPageBlocks','emissionIntensity','initialState','bookIdentity','clueData']}})
metrics['books']=book_summary
print('Books settings', {k:dict(collections.Counter(b[k] for b in book_summary)) for k in ['active','canContainPassword','usePhysicalPageBlocks','emissionIntensity']})
print('Other books',[(b['path'],b['active'],b['canContainPassword']) for b in book_summary if not any((c.get('script') or '').endswith('PuzzleBookIdentity.cs') for c in next(r for r in books if r['path']==b['path'])['components'])])

metrics['volumes']=[]
for d in docs.values():
    if 'd0ef8dc2c2eabfa4e8cb77be57a837c0' in d['text'] or '172515602e62fb746b5d573b38a5fe58' in d['text']:
        metrics['volumes'].append({'path':component_owner.get(d['id'],{}).get('path'),'text':d['text']})

(out/'scene-details.json').write_text(json.dumps(metrics,ensure_ascii=False,indent=2),encoding='utf-8')
(out/'scene-hierarchy.txt').write_text('\n'.join(f"{r['path']} | active={r['active']} | fileID={r['id']} | "+', '.join((c.get('script') or str(c.get('type'))) + (' [disabled]' if c.get('enabled')=='0' else '') for c in r['components']) for r in sorted(objects.values(),key=lambda r:r['path'])),encoding='utf-8')
