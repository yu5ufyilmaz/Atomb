from pathlib import Path
import collections
import json
import re
import subprocess

root = Path.cwd()
out = root / 'outputs/laboratory-synthesis-audit'
guidmap = json.loads((out / 'guid-map.json').read_text(encoding='utf-8'))
pattern = r'guid: [0-9a-f]{32}'
args = ['rg', '-o', '--no-heading', '--no-line-number', pattern, 'Assets', 'ProjectSettings']
for ext in ['unity', 'prefab', 'asset', 'mat', 'controller', 'overrideController', 'anim', 'playable', 'lighting', 'meta', 'renderTexture', 'shadergraph', 'shadersubgraph', 'vfx']:
    args += ['-g', '*.' + ext]
proc = subprocess.run(args, capture_output=True, text=True, encoding='utf-8', errors='replace')
edges = collections.defaultdict(set)
inbound = collections.defaultdict(set)
for line in proc.stdout.splitlines():
    path, sep, guid = line.rpartition(':guid: ')
    if not sep:
        continue
    path = path.replace('\\', '/')
    if path.endswith('.meta'):
        # A meta's own GUID declares its identity, not a self-reference.
        owner = path[:-5]
        if guidmap.get(guid) == owner:
            continue
        path = owner
    target = guidmap.get(guid)
    if target:
        edges[path].add(target)
        inbound[target].add(path)

def closure(starts):
    seen = set(starts)
    pending = list(seen)
    while pending:
        for target in edges.get(pending.pop(), []):
            if target not in seen:
                seen.add(target)
                pending.append(target)
    return seen

scene = 'Assets/Scenes/scn_LaboratorySynthesis.unity'
scene_closure = closure([scene])
scene_rows = json.loads((out / 'scene-inventory.json').read_text(encoding='utf-8'))
cs_files = list((root / 'Assets/Scripts').rglob('*.cs'))
cs_texts = {p.relative_to(root).as_posix(): p.read_text(encoding='utf-8-sig', errors='replace') for p in cs_files}
all_cs = dict(cs_texts)
for p in (root / 'Assets/StarterAssets').rglob('*.cs'):
    all_cs[p.relative_to(root).as_posix()] = p.read_text(encoding='utf-8-sig', errors='replace')
scripts = []
for path, text in cs_texts.items():
    if '/Editor/' in path:
        continue
    types = re.findall(r'\b(?:class|interface|enum|struct)\s+(\w+)', text)
    usages = []
    for other, body in all_cs.items():
        if other == path:
            continue
        used = [name for name in types if re.search(r'\b' + re.escape(name) + r'\b', body)]
        if used:
            usages.append({'path': other, 'types': used})
    scripts.append({'path': path, 'types': types, 'serializedReferencers': sorted(inbound.get(path, [])), 'codeReferencers': usages, 'sceneDependency': path in scene_closure})
assets = []
for p in (root / 'Assets/Scripts/Scriptables').rglob('*.asset'):
    path = p.relative_to(root).as_posix()
    assets.append({'path': path, 'referencers': sorted(inbound.get(path, [])), 'sceneDependency': path in scene_closure})
result = {'scene': scene, 'sceneSerializedDependencyCount': len(scene_closure), 'sceneSerializedDependencies': sorted(scene_closure), 'scripts': scripts, 'scriptableAssets': assets}
(out / 'reference-inventory.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
(out / 'reference-graph.json').write_text(json.dumps({k: sorted(v) for k,v in edges.items()}, ensure_ascii=False), encoding='utf-8')
print('Dependency closure:', len(scene_closure), 'runtime script files:', len(scripts), 'scriptable assets:', len(assets))
print('Scripts with no serialized referencers and no code referencers:')
for row in scripts:
    if not row['serializedReferencers'] and not row['codeReferencers']:
        print(row['path'], row['types'])
print('Scriptable assets with no serialized referencers:')
for row in assets:
    if not row['referencers']:
        print(row['path'])
print('Project scripts not reachable through scene serialized references:')
for row in scripts:
    if not row['sceneDependency']:
        print(row['path'], 'serialized=', len(row['serializedReferencers']), 'code=', len(row['codeReferencers']))
