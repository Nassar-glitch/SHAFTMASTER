import json,math,re
from pathlib import Path
from PIL import Image,ImageDraw
r=Path(__file__).resolve().parent
a=json.loads((r/'structure.json').read_text(encoding='utf-8-sig'))
es=next(b['entities'] for b in a['blocks'] if b['Name']=='*Model_Space')
im=Image.new('RGB',(2400,1900),'white');d=ImageDraw.Draw(im)
def xy(p):return (80+(p[0]-200)*.78,1800-(p[1]-150)*.78)
for e in es:
 t=e['id']['dxf'];c='#555555';pts=[]
 if e.get('Layer')=='CL':c='#00a0ac'
 if t=='LWPOLYLINE':pts=[v['point'] for v in e['vertices']];pts+=pts[:1] if e.get('Closed') else []
 elif t=='LINE':pts=[e['StartPoint'],e['EndPoint']]
 elif t in ('CIRCLE','ARC'):
  start=e.get('StartAngle',0);end=e.get('EndAngle',2*math.pi)
  if end<=start:end+=2*math.pi
  pts=[[e['Center'][0]+e['Radius']*math.cos(start+(end-start)*i/100),e['Center'][1]+e['Radius']*math.sin(start+(end-start)*i/100)] for i in range(101)]
 if len(pts)>1:d.line([xy(p) for p in pts],fill=c,width=1)
 if t=='DIMENSION':
  v=e['Measurement'];v=v*180/math.pi if 'Angular' in e['managedType'] else v
  label=e.get('DimensionText','').replace('<>',f'{v:.2f}') or f'{v:.2f}'
  d.text(xy(e['TextPosition']),label,fill='blue')
  if e.get('XLine1Point'):d.line([xy(e['XLine1Point']),xy(e['XLine2Point'])],fill='#dddddd')
 if t=='MTEXT':d.text(xy(e['Location']),re.sub(r'\\[^;]*;','',e['Contents']).strip('{}'),fill='red')
im.save(r/'reference-overview.png')
