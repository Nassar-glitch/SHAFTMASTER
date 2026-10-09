"""One-time reviewed template classification in shaft-local coordinates.
Source entity handles/order are intentionally not emitted or used for lookup.
Prepared DWGs subsequently use persistent XData exclusively.
"""
import json
from pathlib import Path
r=Path(__file__).resolve().parent
a=json.loads((r/'structure.json').read_text(encoding='utf-8-sig'))
entities=next(b['entities'] for b in a['blocks'] if b['Name']=='*Model_Space')
dw=next(e for e in entities if e.get('DimensionText','').startswith('SHAFT WIDTH'))
dd=next(e for e in entities if e.get('DimensionText','').startswith('SHAFT DEPTH'))
left=min(dw['XLine1Point'][0],dw['XLine2Point'][0]);rear=dw['XLine1Point'][1];front=rear-dd['Measurement'];sw=dw['Measurement'];sd=dd['Measurement'];center=sw/2
cy=next(e['StartPoint'][1]-front for e in entities if e['id']['dxf']=='LINE' and e['Layer']=='CL' and abs(e['EndPoint'][0]-e['StartPoint'][0])>3000 and abs(e['StartPoint'][1]-(rear-125))<.01)
car=cy-800
def near(a,b):return abs(a-b)<.04
seeds=[]
for e in entities:
 b=e.get('GeometricExtents');t=e['id']['dxf']
 if not b:raise ValueError('No native extent')
 x0,y0=b['min'][0]-left,b['min'][1]-front;x1,y1=b['max'][0]-left,b['max'][1]-front
 key=None;role='machine';group='MachineRoom.MachineChassis.ReferenceDetail'
 text=e.get('Contents','');label=e.get('DimensionText','');w=x1-x0;h=y1-y0
 if t=='LINE' and near(x0,0) and near(x1,0) and near(y0,0) and near(y1,sd):key='MachineRoom.Shaft.LeftFace';role='shaftLeft';group=key
 elif t=='LINE' and near(x0,sw) and near(x1,sw) and near(y0,0) and near(y1,sd):key='MachineRoom.Shaft.RightFace';role='shaftRight';group=key
 elif t=='LINE' and e.get('Layer')=='CL' and w>3000:
  role='cwAxis' if near(y0,cy) else 'carAxis';key='MachineRoom.Reference.'+('CounterweightAxis' if role=='cwAxis' else 'CarAxis');group=key
 elif t=='LINE' and e.get('Layer')=='CL' and h>2000:role='mainAxis';key='MachineRoom.Reference.ShaftCenterline';group=key
 elif t=='LWPOLYLINE' and e.get('Layer')=='Wall' and near(w,2500):role='room';key='MachineRoom.Room.InnerOutline';group=key
 elif e.get('Layer')=='Wall':role='room';group='MachineRoom.Room.Wall'
 elif t=='LINE' and near(x0,-200) and near(y0,0) and near(y1,sd):role='shaftLeft';group='MachineRoom.ConcreteBeam.LeftBoundary'
 elif t=='LINE' and near(x0,sw+200) and near(y0,0) and near(y1,sd):role='shaftRight';group='MachineRoom.ConcreteBeam.RightBoundary'
 elif t=='DIMENSION':
  role='dimension';group='MachineRoom.Dimension';key='MachineRoom.Dimension.'+str(round(e['Measurement'],3)).replace('.','_')
  if label.startswith('SHAFT WIDTH'):key='MachineRoom.Dimension.ShaftWidth'
  elif label.startswith('SHAFT DEPTH'):key='MachineRoom.Dimension.ShaftDepth'
  elif 'ROOM WIDTH' in label:key='MachineRoom.Dimension.RoomWidth'
  elif 'ROOM DEPTH' in label:key='MachineRoom.Dimension.RoomDepth'
  elif 'TZ' in label:key='MachineRoom.Dimension.TZ'
  elif near(e['Measurement'],250):key='MachineRoom.Governor.DiameterDimension';role='governor'
  elif near(e['Measurement'],200):key='MachineRoom.Governor.OffsetDimension';role='governor'
  elif near(e['Measurement'],900):key='MachineRoom.Dimension.FrontExtension'
  elif near(e['Measurement'],125):key='MachineRoom.Dimension.RearToCounterweight'
  elif near(e['Measurement'],675):key='MachineRoom.Dimension.FrontToCar'
  elif near(e['Measurement'],525):key='MachineRoom.Dimension.'+('RightExtension' if max(e['XLine1Point'][0],e['XLine2Point'][0])-left>sw+.05 else 'LeftExtension')
  elif near(e['Measurement'],725):key='MachineRoom.Dimension.'+('RightHalfShaft' if max(e['XLine1Point'][0],e['XLine2Point'][0])-left>center+.05 else 'LeftHalfShaft')
  else:key=None
 elif '%%C250' in text:role='deflectorLabel';group='MachineRoom.DeflectorSheave.Annotation';key=group
 elif x0>center-110 and x1<center+110 and y0>=cy-270-.04 and y1<=cy+65:
  role='cw';group='MachineRoom.RopeOpening.Counterweight.ReferenceDetail'
  if near(y0,cy-250) and near(y1,cy):
   role='deflectorProfile';group='MachineRoom.DeflectorSheave.Profile'
   if near(x0,center-50.035441943):key='MachineRoom.DeflectorSheave.LeftPitchProjection'
  elif near(y0,cy-250) or near(y0,cy):role='deflectorProfile';group='MachineRoom.DeflectorSheave.Profile'
  elif e.get('Layer')=='CL' and near(y0,cy-125):role='deflectorCenter';key='MachineRoom.DeflectorSheave.CenterMark';group=key
 elif x1<center-300 and y0>car+50 and y1<cy+100:
  role='governor';group='MachineRoom.Governor.Assembly'
 elif 'GOV.' in text:role='governor';group='MachineRoom.Governor.RopeOpeningNote'
 elif '160X160' in text:role='cw';group='MachineRoom.RopeOpening.Note'
 elif 'C. L. OF C.W.' in text:role='cw';group='MachineRoom.Reference.CounterweightLabel'
 elif 'C. L. OF GUIDES' in text:role='machine';group='MachineRoom.Reference.CarLabel'
 elif 'CONTROL PANEL' in text or (x1<0 and y1<0):role='panel';group='MachineRoom.ControlPanel.Assembly'
 elif y1<-600:role='front';group='MachineRoom.Room.EntranceAndElectricalNotes'
 elif x1<-sw*.3 and y0>car-100 and y1<cy:role='ventLeft';group='MachineRoom.Ventilation.Left'
 elif x0>sw+sw*.3 and y0>car-100 and y1<cy:role='ventRight';group='MachineRoom.Ventilation.Right'
 elif w>sw and h<=200.1 and near(y1,0):role='frontBeam';group='MachineRoom.ConcreteBeam.Front'
 elif w>sw and near(y0,-200) and near(y1,-200):role='frontBeam';group='MachineRoom.ConcreteBeam.Front'
 elif x0>center-450 and x1<center+500 and y0>-250 and y1<sd+250:
  role='machine';group='MachineRoom.Chassis.ReferenceDetail'
  if y0>sd-20:group='MachineRoom.Support.'+('P3' if (x0+x1)/2<center else 'P1')
  elif y1<-30:group='MachineRoom.Support.'+('P4' if (x0+x1)/2<center else 'P2')
  elif x0>center-90 and x1<center+90 and y0>=car-.05 and y1<=car+520.05:group='MachineRoom.TractionSheave.ReferenceDetail'
  elif x0>center+50 and x1<center+400 and y0>car-320 and y1<car+560:group='MachineRoom.Machine.ReferenceDetail'
 elif y0>sd+100 and t!='DIMENSION':role='rear';group='MachineRoom.Reference.RearSymbol'
 elif y1<0:role='front';group='MachineRoom.Reference.FrontSymbol'
 elif text=='B' and y1<0:role='front';group='MachineRoom.Reference.FrontSymbol'
 elif x0>center-350 and x1<center+400:
  # Keep all uncertain machine/chassis/support geometry coherent and rigid.
  group='MachineRoom.Chassis.ReferenceDetail'
  if y0>sd-20:group='MachineRoom.Support.'+('P3' if (x0+x1)/2<center else 'P1')
  elif y1<-30:group='MachineRoom.Support.'+('P4' if (x0+x1)/2<center else 'P2')
  elif x0>center-90 and x1<center+90 and y0>=car-.05 and y1<=car+520.05:group='MachineRoom.TractionSheave.ReferenceDetail'
 # Explicit local geometry features for the two 160 x160 suspension openings are inside the machinery drawing;
 # keep unresolved cutout detail intact instead of inventing a new rope path or moving isolated pixels.
 seeds.append(dict(Type=t,MinX=x0,MinY=y0,MaxX=x1,MaxY=y1,Role=role,Prefix=group,Key=key))
keys=[s['Key'] for s in seeds if s['Key']]
if len(keys)!=len(set(keys)):raise ValueError('Duplicate critical keys')
out=r.parents[1]/'config'/'machine-room-seed.json';out.write_text(json.dumps(dict(SourceSha256=a['sha256Before'],SW=sw,SD=sd,Entities=seeds),indent=2))
from collections import Counter
print(Counter(s['Role'] for s in seeds));print('Critical keys',keys)
