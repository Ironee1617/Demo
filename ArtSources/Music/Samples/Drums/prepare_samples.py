"""Reproduce selected CC0 drum downloads and phase-aligned stereo mixtures."""
from pathlib import Path
import concurrent.futures, hashlib, json, re, sys, urllib.request
sys.path.insert(0, str(Path(__file__).resolve().parents[4] / '.scratch' / 'music-python'))
import numpy as np
import soundfile as sf

ROOT = Path(__file__).resolve().parent
TREE = json.loads((ROOT/'source-tree.json').read_text(encoding='utf-8-sig'))
COMMIT = TREE['sha']
BASE = f'https://raw.githubusercontent.com/sfzinstruments/karoryfer.big-rusty-drums/{COMMIT}/'
FILES = {x['path']: x for x in TREE['tree'] if x['type']=='blob'}
CONFIG = {
 'kick': ('kick_24/kick', 'k', 'kick', 3, 0.19),
 'snare': ('snare_14/center', 'sn_center', 'top', 3, 0.27),
 'snare_rimshot': ('snare_14/rimshot', 'sn_rims', 'top', 2, 0.27),
 'hat': ('hihat_14/cl', 'ht_cl', 'cl', 3, 0.60),
 'open_hat': ('hihat_14/open', 'ht_open', 'cl', 2, 0.62),
 'ride': ('ride_22/rd', 'rd', 'cl', 2, 0.67),
 'crash': ('crash_17/cr', 'cr', 'cl', 2, 0.78),
 'tom_hi': ('tom_14/center', 't14', 'cl', 2, 0.25),
 'tom_mid': ('tom_15/center', 't15', 'cl', 2, 0.25),
 'tom_low': ('tom_18/center', 't18', 'cl', 2, 0.25),
}
JOBS=[]
HITS=[]
for kind,(directory,prefix,close,rrcount,ohgain) in CONFIG.items():
    layers=sorted({int(re.search(r'_vl(\d+)_',path).group(1)) for path in FILES if path.startswith(f'Samples/{directory}/{close}/') and re.search(r'_vl(\d+)_',path)})
    selected=sorted({layers[round((len(layers)-1)*fraction)] for fraction in [0.43,0.72,1]})
    for layer in selected:
        for rr in range(1,rrcount+1):
            name=f'{prefix}_vl{layer}_rr{rr}.flac'
            sources=[]
            for mic in [close,'oh']+(['btm'] if kind.startswith('snare') else []):
                path=f'Samples/{directory}/{mic}/{name}'
                if path not in FILES: raise ValueError(path)
                sources.append({'mic':mic,'source_path':path,'file':'originals/'+path.removeprefix('Samples/')})
                JOBS.append(sources[-1])
            velocity=round(127*layer/max(layers))
            HITS.append({'kind':kind,'velocity':velocity,'source_velocity_layer':layer,'source_velocity_layer_count':max(layers),'round_robin':rr,'mic':'mix','file':f'mixed/{kind}_v{velocity}_rr{rr}.flac','sources':sources,'overhead_gain':ohgain})
print(f'Selected {len(HITS)} mixed hits from {len(JOBS)} original mic files, {sum(FILES[j["source_path"]]["size"] for j in JOBS)/1e6:.1f} MB downloads',flush=True)
assert sum(FILES[j['source_path']]['size'] for j in JOBS)<150_000_000

def download(job):
    target=ROOT/job['file']; target.parent.mkdir(parents=True,exist_ok=True)
    if not target.exists():
        data=urllib.request.urlopen(BASE+job['source_path'],timeout=60).read()
        target.write_bytes(data)
    data=target.read_bytes()
    job['sha256']=hashlib.sha256(data).hexdigest()
    return target
with concurrent.futures.ThreadPoolExecutor(max_workers=12) as pool:
    list(pool.map(download,JOBS))
(ROOT/'LICENSE-CC0.txt').write_bytes(urllib.request.urlopen(BASE+'LICENSE').read())
(ROOT/'mixed').mkdir(exist_ok=True)
originals={}
mixes=[]
for hit in HITS:
    tracks=[]
    for source in hit['sources']:
        data,sr=sf.read(ROOT/source['file'],dtype='float32',always_2d=True)
        if data.shape[1]==1: data=np.repeat(data,2,axis=1)
        tracks.append((source['mic'],data))
        originals[source['file']]={**source,'sample_rate':sr,'channels':sf.info(ROOT/source['file']).channels,'frames':len(data)}
    length=max(len(d) for mic,d in tracks)
    mixed=np.zeros((length,2),dtype='float32')
    for mic,data in tracks:
        gain=hit['overhead_gain'] if mic=='oh' else (0.22 if mic=='btm' else 1.0)
        mixed[:len(data)]+=gain*data
    # Aligned channels remain aligned. Remove only common silence before audible onset.
    peak=float(np.max(np.abs(mixed)))
    envelope=np.max(np.abs(mixed),axis=1)
    audible=np.flatnonzero(envelope>max(peak*0.008,1e-5))
    start=max(0,int(audible[0])-round(sr*0.0015)) if len(audible) else 0
    mixed=mixed[start:]
    # End taper affects only the last 15ms; preserve original cymbal room tails.
    n=min(len(mixed),round(sr*0.015))
    mixed[-n:]*=np.linspace(1,0,n,dtype='float32')[:,None]
    hit.update(sample_rate=sr,channels=2,frames=len(mixed),trimmed_leading_frames=start)
    mixes.append((hit,mixed))
# One constant gain per instrument preserves recorded velocity dynamics and RR differences.
for kind in CONFIG:
    group=[(hit,data) for hit,data in mixes if hit['kind']==kind]
    peak=max(float(np.max(np.abs(data))) for hit,data in group)
    gain=0.90/max(peak,1e-9)
    for hit,data in group:
        data*=gain
        sf.write(ROOT/hit['file'],data,hit['sample_rate'],format='FLAC',subtype='PCM_24')
        hit['normalization_gain']=gain
        hit['peak']=float(np.max(np.abs(data)))
        hit['rms']=float(np.sqrt(np.mean(data**2)))
        hit['sha256']=hashlib.sha256((ROOT/hit['file']).read_bytes()).hexdigest()
manifest={
 'schema_version':1,'library':'Big Rusty Drums','author':'Karoryfer Samples / Karoryfer Lecolds',
 'license':'CC0-1.0','license_file':'LICENSE-CC0.txt','source_url':'https://shop.karoryfer.com/pages/free-big-rusty-drums',
 'repository_url':'https://github.com/sfzinstruments/karoryfer.big-rusty-drums','commit':COMMIT,
 'license_evidence':['https://shop.karoryfer.com/pages/free-samples',f'https://github.com/sfzinstruments/karoryfer.big-rusty-drums/blob/{COMMIT}/LICENSE'],
 'mix_notes':'Original close microphone + stereo overhead, plus quiet bottom snare microphone. Common onset trim and 15ms tail fade. Constant gain per articulation; no synthesized drums, no pitch changes, no normalization per hit.',
 'velocity_notes':'velocity is the rounded upper bound of the original evenly spaced source layer (127 * layer / layer_count). Choose nearest desired velocity, then rotate round_robin. Recorded level differences are preserved; do not normalize each hit.',
 'samples':HITS,'originals':list(originals.values())}
(ROOT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'mixed_hits':len(HITS),'original_mics':len(originals),'total_size_MB':round(sum(p.stat().st_size for p in ROOT.rglob('*') if p.is_file())/1e6,2),'kinds':{kind:sorted({h['velocity'] for h in HITS if h['kind']==kind}) for kind in CONFIG}},indent=2),flush=True)
