"""Compare data, ignoring reflection-dependent JSON member order; check protected artifacts."""
import json, hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]; OUT=ROOT/'output/calculation-phase1'
def read(name):return json.loads((OUT/name).read_text(encoding='utf-8-sig'))
new=read('new-drawing-snapshots.json');old=read('release-drawing-snapshots.json')
assert new==old, 'Four-view calculated results changed'
for key in ('new-drawing-snapshots','release-drawing-snapshots'):
 (OUT/(key+'.canonical.json')).write_text(json.dumps(read(key+'.json'),sort_keys=True,separators=(',',':')),encoding='utf-8')
protected=read('protected-hashes.json')
changed=[path for path,sha in protected.items() if hashlib.sha256(Path(path).read_bytes()).hexdigest()!=sha]
assert not changed, 'Protected reference/template/release files changed: '+repr(changed)
result={'FourViewCases':[[x['SW'],x['SD']] for x in new],'AllFourViewResultsExactlyEqual':True,'NativeDWGRegenerationPerformed':False,'ProtectedFileCount':len(protected),'ProtectedFilesUnchanged':True,'ReleaseExeRebuilt':False,'ReleaseCoreSha256':hashlib.sha256((ROOT/'bin-shaftmaster-v01/Elevator.Core.dll').read_bytes()).hexdigest()}
if (OUT/'native-readonly-result.json').exists():
 native=read('native-readonly-result.json');assert native['Status']=='PASS'
 fixture=read('native-geometry.json')
 for bearing in fixture['Bearings']:
  actual=next(b for b in native['Geometry']['Bearings'] if b['Id']==bearing['Id'])
  for axis in 'XYZ':assert abs(actual['Centre'][axis]-bearing['Centre'][axis])<.02
 result.update(NativeReadOnlyStatus='PASS',NativeContactCentresMatchReviewedEvidence=True,OldProjectCalculationStatus=native['Calculation']['Status'])
(OUT/'verification.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print('PASS: exact semantic equality of all four calculated views in three release-baseline cases;',len(protected),'protected files unchanged.')
