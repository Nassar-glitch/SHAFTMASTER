"""Prepare isolated regression evidence from completed read-only review, not live defaults."""
import json, hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'output/calculation-phase1'; OUT.mkdir(exist_ok=True)
def read(p): return json.loads((ROOT/p).read_text(encoding='utf-8-sig'))
ev=read('tests/machine-room/chassis-evidence-1.json')
plan=json.loads(read('output/shaftmaster-ui/native-toolbar-result.json')['DesignJson'])['Plan']
t=read('config/chassis-topology.json')
assert (ev['SW'],ev['SD'],ev['TZ'])==(plan['SW'],plan['SD'],plan['TZ'])
source={'SW':ev['SW'],'SD':ev['SD'],'CarRow':plan['CarRow'],'CwRow':plan['CwRow'],'Pitch':plan['MachineRoomSection']['DeflectorPitchMm'],'Extensions':plan['MachineRoom']['Extensions'],'GovernorStartOffset':200,'Contours':[],'Issues':[]}
for e in ev['Geometry']:
 k=e['Key']; pts=e['Points']
 if not k.startswith(('MachineRoom.Support.','MachineRoom.Chassis.','MachineRoom.MachineChassis.','MachineRoom.TractionSheave.','MachineRoom.DeflectorSheave.')):continue
 # The regression evidence lacks bulges. Only two/three-point straight contour fragments
 # are admitted here; production reader checks native entity type and every bulge.
 straight=len(pts) in (2,3)
 c={'Id':k,'Role':e['Role'],'Straight':straight,'Points':pts,'Dependency':None}
 if e['Role']=='machine':
  lo=min(p['Y'] for p in pts);hi=max(p['Y'] for p in pts)
  long=straight and max(p['X'] for p in pts)-min(p['X'] for p in pts)<2
  front=any(abs(lo-v)<.02 for v in t['FrontMemberEnds'])
  rear=any(abs(hi-(v+ev['SD']-t['SourceSD']))<.02 for v in t['RearMemberEnds'])
  mf=t['MachineFrontEnd']+plan['CarRow']-t['SourceCarRow'];mr=t['MachineRearEnd']+plan['CarRow']-t['SourceCarRow']
  outline=straight and len(pts)==3 and abs(pts[0]['Y']-pts[1]['Y'])<.02 and abs(pts[1]['X']-pts[2]['X'])<2
  refs=None;behavior='FixedToMachine';ref='Machine'
  if long and front and rear:behavior='SpanBetweenReferences';refs=['FrontBeam' if abs(p['Y']-lo)<.02 else 'RearBeam' for p in pts]
  elif long and front and abs(hi-mf)<.02:behavior='StretchFromCarSide';refs=['Machine' if abs(p['Y']-hi)<.02 else 'FrontBeam' for p in pts]
  elif (long or outline) and abs(lo-mr)<.02 and rear:behavior='StretchFromCWRearSide';refs=['Machine' if abs(p['Y']-lo)<.02 else 'RearBeam' for p in pts]
  elif k.startswith(('MachineRoom.Support.P1.','MachineRoom.Support.P3.')):ref='RearBeam'
  elif k.startswith(('MachineRoom.Support.P2.','MachineRoom.Support.P4.')):ref='FrontBeam'
  elif straight and hi<0:ref='FrontBeam'
  elif straight and lo>ev['SD']:ref='RearBeam'
  refs=refs or [ref]*len(pts);datums={'FrontBeam':0,'RearBeam':ev['SD'],'Machine':plan['CarRow'],'Counterweight':plan['CwRow']}
  c['Dependency']={'Behavior':behavior,'References':refs,'Offsets':[{'X':p['X']-ev['SW']/2,'Y':p['Y']-datums[r],'Z':p['Z']} for p,r in zip(pts,refs)]}
 source['Contours'].append(c)
(OUT/'native-evidence-fixture.json').write_text(json.dumps(source,indent=2),encoding='utf-8')
plan['MachineRoom']['Rules']=[] # Geometry fixture excludes reporting dictionaries with null values.
plan['MachineRoom'].pop('Reeving',None) # Nullable point reporting fields are not needed by the fixture.
(OUT/'reference-plan.json').write_text(json.dumps(plan,indent=2),encoding='utf-8')
protected=list(ROOT.parent.glob('*.dwg'))+list((ROOT/'Templates').glob('*.dwg'))+list((ROOT/'bin-shaftmaster-v01').glob('*'))+[ROOT/'examples/ElevatorProject.example.json',ROOT.parent/'Elevator_Calculations_GEARBOX_Code_Corrected_Dual_Units.docx']
hashes={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in protected if p.is_file()}
(OUT/'protected-hashes.json').write_text(json.dumps(hashes,indent=2),encoding='utf-8')
print('Isolated evidence fixture prepared:',len(source['Contours']),'contours;',len(hashes),'protected files hashed.')
