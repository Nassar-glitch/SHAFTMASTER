"""Reviewed B-B source bootstrap. Runtime identity is persistent XData, never source handles."""
import json,math
from pathlib import Path
r=Path(__file__).resolve().parent;a=json.loads((r/'structure.json').read_text(encoding='utf-8-sig'))
es=next(b['entities'] for b in a['blocks'] if b['Name']=='*Model_Space')
dim=[e for e in es if e['id']['dxf']=='DIMENSION']
find=lambda v:next(e for e in dim if abs(e['Measurement']-v)<.02)
front=find(675)['XLine2Point'][0];floor=find(450)['XLine1Point'][1]
tc=next(e for e in es if e['id']['dxf']=='CIRCLE' and e['Radius']==260)['Center']
dc=next(e for e in es if e['id']['dxf']=='CIRCLE' and e['Radius']==125)['Center']
steel_top=max(e['XLine1Point'][1] for e in dim if e.get('Rotation')==math.pi/2 and abs(e['Measurement']-140)<.02)
steel_top=max(max(e['XLine1Point'][1],e['XLine2Point'][1]) for e in dim if e.get('Rotation')==math.pi/2 and abs(e['Measurement']-140)<.02)
steel_bottom=find(140)['XLine1Point'][1]
source=dict(SourceCarRow=front-(tc[0]+260),SourceCwRow=front-(dc[0]-125),SourceSD=front-find(130)['XLine2Point'][0],SourceTractionX=tc[0]-front,SourceTractionZ=tc[1]-floor,SourceDeflectorX=dc[0]-front,SourceDeflectorZ=dc[1]-floor,SourceSteelBottomZ=steel_bottom-floor,SourceSteelTopZ=steel_top-floor,SourceConcreteHeight=450,SourceFrontSupportX=(2544.1623943880654+2677.4146766163863)/2-front,SourceRearSupportX=(779.0554111440829+912.3076933724037)/2-front,SourceSlabThickness=200,SourceStandBottomZ=1331.0463721892334-floor,CarOpeningWidth=1915.808914236531-1756.485859730728,CwOpeningWidth=1155.6060143440077-996.0193125623296,HangerTopBelowSteelTop=10,HangerBottomAboveDeflectorCenter=1365.7942625088037-dc[1])
out=[]
for e in es:
 t=e['id']['dxf'];b=e['GeometricExtents'];x0,y0=b['min'][0]-front,b['min'][1]-floor;x1,y1=b['max'][0]-front,b['max'][1]-floor
 role='slab';key=None;prefix='Reference';dimkind=None
 if t=='DIMENSION':
  role='dimension';prefix='Dimension'
  if 'Angular' in e['managedType']:dimkind='Wrap'
  elif 'Diametric' in e['managedType']:dimkind='TractionDiameter' if e['Measurement']>300 else 'DeflectorDiameter'
  elif 'TZ' in e.get('DimensionText',''):dimkind='TZ'
  elif abs(e['Measurement']-640)<.02:dimkind='CenterSeparation'
  elif abs(e['Measurement']-450)<.02:dimkind='ConcreteHeight'
  elif abs(e['Measurement']-675)<.02:dimkind='FrontToCar'
  elif abs(e['Measurement']-130)<.02:dimkind='RearToCW'
  elif abs(e['Measurement']-140)<.02:dimkind='SteelDepth' if max(e['XLine1Point'][1],e['XLine2Point'][1])>steel_bottom+50 else 'StandDepth'
  else:raise Exception('Unknown dimension')
  key='MachineRoomSection.Dimension.'+dimkind
 elif t=='CIRCLE' and e['Radius']==260:role='traction';key='MachineRoomSection.TractionSheave.PitchCircle'
 elif t=='CIRCLE' and e['Radius']==125:role='deflector';key='MachineRoomSection.DeflectorSheave.PitchCircle'
 elif t=='LINE' and e['Layer']=='CL':
  role='rope';prefix='Rope'
  if abs(e['StartPoint'][0]-e['EndPoint'][0])>10:key='MachineRoomSection.Rope.CommonTangent'
  elif e['StartPoint'][0]<tc[0]:key='MachineRoomSection.Rope.CounterweightDrop'
  else:key='MachineRoomSection.Rope.CarDrop'
 elif e['Layer']=='Car' or t=='INSERT' or (t=='CIRCLE' and e['Radius']<10):
  prefix='DeflectorSheave';role='deflector'
  if t=='LINE' and y1-y0>200:role='hanger';prefix='Chassis.Hanger'
 elif e.get('PatternName')=='AR-CONC':role='concrete';prefix='ConcreteBeam.Hatch'
 elif e['Layer']=='Wall' and y1>400 and y0<5:role='concrete';prefix='ConcreteBeam.Profile'
 elif e['Layer']=='Wall' and 440<y0<452:role='concrete';prefix='ConcreteBeam.Cap'
 elif y0>=source['SourceSteelBottomZ']-1 and y1<=source['SourceSteelTopZ']+1:
  role='steel';prefix='SteelBeam'
  if x1-x0>1000 and y1-y0<2 and y0>source['SourceSteelBottomZ']+2 and y1<source['SourceSteelTopZ']-2:role='steelDetail';prefix='SteelBeam.NativeProfileDetail'
 elif y0>source['SourceSteelTopZ']+1:role='machine';prefix='Machine'
 elif y0>440 and (x1<source['SourceDeflectorX']-180 or x0>source['SourceTractionX']+300):role='stand';prefix='SteelBeam.StandAndPad'
 elif y1<=5 or (y0<0 and y1<100):role='slab';prefix='Slab'
 elif t=='MTEXT':role='slab';prefix='Annotation'
 elif y0>10 and y1<130 and x1-x0>500:role='spanNote';prefix='Annotation.RopeFallBox'
 else:raise Exception(f'Unreviewed {t} {x0,y0,x1,y1}')
 if key is None:prefix='MachineRoomSection.'+prefix
 else:prefix=key
 out.append(dict(Key=key,Prefix=prefix,Type=t,Role=role,MinX=x0,MinY=y0,MaxX=x1,MaxY=y1))
data=dict(SourceSha256=a['sha256Before'],Reference=source,Entities=out)
(r.parents[1]/'config'/'section-seed.json').write_text(json.dumps(data,indent=2))
from collections import Counter
print(Counter(v['Role'] for v in out));print(source)


