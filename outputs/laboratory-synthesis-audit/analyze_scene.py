from pathlib import Path
import re,json,collections,bisect,subprocess
root=Path.cwd(); assets=root/'Assets'; out=root/'outputs/laboratory-synthesis-audit'; out.mkdir(parents=True,exist_ok=True)
guidmap=json.loads((out/'guid-map.json').read_text(encoding='utf-8')) if (out/'guid-map.json').exists() else {}
for p in ([] if guidmap else assets.rglob('*.meta')):
    try:
        m=re.search(r'^guid: ([0-9a-f]{32})$',p.read_text(encoding='utf-8-sig'),re.M)
        if m: guidmap[m[1]]=p.relative_to(root).as_posix()[:-5]
    except (OSError,UnicodeError): pass
(out/'guid-map.json').write_text(json.dumps(guidmap,ensure_ascii=False),encoding='utf-8')
scene='Assets/Scenes/scn_LaboratorySynthesis.unity'
src=(root/scene).read_text(encoding='utf-8-sig'); headers=list(re.finditer(r'^--- !u!(\d+) &(-?\d+)([^\r\n]*)\r?$',src,re.M))
newlines=[m.start() for m in re.finditer('\n',src)]
unresolved={g for g in re.findall(r'guid: ([0-9a-f]{32})',src) if g not in guidmap and not g.startswith('000000')}
if unresolved:
    found=subprocess.run(['rg','-l','^guid: ('+'|'.join(unresolved)+')$','Packages','Library/PackageCache','-g','*.meta'],capture_output=True,text=True,encoding='utf-8',errors='replace')
    for name in found.stdout.splitlines():
        m=re.search(r'^guid: (\w+)',Path(name).read_text(encoding='utf-8-sig',errors='replace'),re.M)
        if m:guidmap[m[1]]=Path(name).as_posix()[:-5]
(out/'guid-map.json').write_text(json.dumps(guidmap,ensure_ascii=False),encoding='utf-8')
docs={}
for i,h in enumerate(headers):
    text=src[h.end():headers[i+1].start() if i+1<len(headers) else len(src)]
    docs[h[2]]={'type':int(h[1]),'id':h[2],'stripped':'stripped' in h[3],'text':text,'line':bisect.bisect_left(newlines,h.start())+1}
def val(text,k,default=None):
    m=re.search(r'^  '+re.escape(k)+r': (.*)$',text,re.M)
    return m[1] if m else default
def fid(text,k):
    m=re.search(r'^  '+re.escape(k)+r': \{fileID: (-?\d+)',text,re.M)
    return m[1] if m else '0'
gos={k:d for k,d in docs.items() if d['type']==1 and not d['stripped']}
trans={k:d for k,d in docs.items() if d['type'] in (4,224)}
go_trans={fid(d['text'],'m_GameObject'):k for k,d in trans.items()}
def path_for(gid,seen=None):
    seen=set() if seen is None else seen
    if gid in seen:return '<cycle>'
    seen.add(gid); go=gos.get(gid)
    if not go:return '<prefab:'+gid+'>'
    name=val(go['text'],'m_Name','?'); tid=go_trans.get(gid); t=trans.get(tid)
    parent=fid(t['text'],'m_Father') if t else '0'
    if parent=='0':return name
    pt=trans.get(parent)
    return (path_for(fid(pt['text'],'m_GameObject'),seen) if pt else '<parent:'+parent+'>')+'/'+name
def active_for(gid,seen=None):
    seen=set() if seen is None else seen
    if gid in seen:return False
    seen.add(gid); go=gos.get(gid)
    if not go:return None
    if val(go['text'],'m_IsActive')=='0':return False
    t=trans.get(go_trans.get(gid)); parent=fid(t['text'],'m_Father') if t else '0'
    if parent=='0':return True
    pt=trans.get(parent)
    return active_for(fid(pt['text'],'m_GameObject'),seen) if pt else None
rows=[]
for gid,go in gos.items():
    comps=[]
    for cid in re.findall(r'component: \{fileID: (-?\d+)\}',go['text']):
        d=docs.get(cid)
        if not d: comps.append({'id':cid,'missingLocal':True}); continue
        m=re.search(r'm_Script: \{fileID: (-?\d+), guid: ([0-9a-f]{32})',d['text'])
        comps.append({'id':cid,'type':d['type'],'enabled':val(d['text'],'m_Enabled'),'script':guidmap.get(m[2],'<unresolved:'+m[2]+'>') if m else None,'scriptGuid':m[2] if m else None,'line':d['line'],'nullScript':d['type']==114 and fid(d['text'],'m_Script')=='0'})
    t=trans.get(go_trans.get(gid)); rows.append({'id':gid,'name':val(go['text'],'m_Name'),'path':path_for(gid),'selfActive':val(go['text'],'m_IsActive'),'active':active_for(gid),'parentTransform':fid(t['text'],'m_Father') if t else None,'line':go['line'],'staticFlags':val(go['text'],'m_StaticEditorFlags'),'components':comps})
prefabs=[]
for d in docs.values():
    if d['type']==1001:
        m=re.search(r'm_SourcePrefab: \{fileID: \d+, guid: ([0-9a-f]{32})',d['text'])
        names=re.findall(r'propertyPath: m_Name\n\s+value: (.*)',d['text'])
        prefabs.append({'id':d['id'],'source':guidmap.get(m[1],m[1]) if m else None,'names':names,'line':d['line']})
scriptcounts=collections.Counter(c['script'] for r in rows for c in r['components'] if c.get('script'))
result={'scene':scene,'serializedTypeCounts':dict(collections.Counter(d['type'] for d in docs.values())),'explicitObjects':len(rows),'explicitActive':sum(r['active'] is True for r in rows),'explicitInactive':sum(r['active'] is False for r in rows),'explicitUnknownActive':sum(r['active'] is None for r in rows),'prefabInstances':len(prefabs),'scripts':scriptcounts,'objects':rows,'prefabs':prefabs,'directGuids':[{'guid':g,'asset':guidmap.get(g)} for g in sorted(set(re.findall(r'guid: ([0-9a-f]{32})',src)))]}
(out/'scene-inventory.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
(out/'scene-hierarchy.txt').write_text('\n'.join(f"{r['path']} | active={r['active']} | fileID={r['id']} | "+', '.join((c.get('script') or str(c.get('type'))) + (' [disabled]' if c.get('enabled')=='0' else '') for c in r['components']) for r in sorted(rows,key=lambda r:r['path'])),encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k not in ('objects','prefabs','directGuids','scripts')},ensure_ascii=False,indent=2))
print('ROOTS')
for r in rows:
    if r['parentTransform']=='0':print(r['name'],r['id'],'active=',r['active'])
print('NULL/MISSING SCRIPTS')
for r in rows:
    for c in r['components']:
        if c.get('nullScript') or c.get('missingLocal') or (c.get('script') or '').startswith('<unresolved:'): print(r['path'],c)
print('PREFABS',collections.Counter(p['source'] for p in prefabs))
