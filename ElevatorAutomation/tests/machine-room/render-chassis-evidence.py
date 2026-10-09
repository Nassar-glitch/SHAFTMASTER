"""Diagnostic native vertex plots; not a replacement for a plotted CAD sheet."""
import json
from pathlib import Path
from PIL import Image, ImageDraw
root=Path(__file__).resolve().parent
cases=[13,15,18,14]
canvas=Image.new('RGB',(2000,1550),'white');draw=ImageDraw.Draw(canvas)
for panel,number in enumerate(cases):
    data=json.loads((root/f'chassis-evidence-{number}.json').read_text(encoding='utf-8-sig'))
    tile=Image.new('RGB',(500,1500),'white');draw=ImageDraw.Draw(tile);xoff=0
    def xy(p):return (xoff+30+(p['X']-350)*.52,1430-(p['Y']+230)*.52)
    draw.text((xoff+30,25),f"SD {data['SD']}  SW {data['SW']}  TZ {data['TZ']}",fill='black')
    draw.text((xoff+30,48),'Native final geometry; shaft-local frame',fill='gray')
    for entity in data['Geometry']:
        if not entity['Visible']:continue
        pts=entity['Points']
        if len(pts)<2:continue
        if max(p['X'] for p in pts)<350 or min(p['X'] for p in pts)>1200:continue
        key=entity['Key'];color='#888888'
        if '.Chassis.' in key:color='#1768c4'
        if '.Support.' in key:color='#b35400'
        if '.DeflectorSheave.' in key or '.RopeOpening.' in key:color='#9c299f'
        if entity['Role'] in ('carAxis','cwAxis'):color='#0e9c9c'
        if entity['Role'] in ('room','frontBeam'):color='#bbbbbb'
        draw.line([xy(p) for p in pts],fill=color,width=1)
    for y,label in [(0,'Front'),(data['SD'],'Rear')]:
        pos=xy({'X':350,'Y':y});draw.line((xoff+20,pos[1],xoff+480,pos[1]),fill='#bbbbbb');draw.text((xoff+20,pos[1]+3),label,fill='black')
    draw.line((xoff+499,0,xoff+499,1550),fill='#dddddd')
    canvas.paste(tile,(panel*500,0))
draw=ImageDraw.Draw(canvas)
draw.text((25,1515),'Blue: chassis edges. Brown: P1-P4. Purple: deflector/opening references. Gray: retained machine/structure. Hatches/text omitted.',fill='black')
canvas.save(root/'chassis-depth-comparison.png')
