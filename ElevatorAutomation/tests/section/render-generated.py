"""Diagnostic drawings from saved/reopened native section curves and dimensions."""
import json, re
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root=Path(__file__).resolve().parent
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',19)
small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',14)
title=ImageFont.truetype('C:/Windows/Fonts/arialbd.ttf',29)
colors={'rope':'#ce2323','generatedRope':'#ce2323','traction':'#1598aa','deflector':'#187e9e','dimension':'#354b91','steel':'#334456','slab':'#9299a1','concrete':'#a59372','stand':'#505763','machine':'#394450','hanger':'#146852'}
def render(number):
    data=json.loads((root/f'case-{number}.json').read_text(encoding='utf-8-sig'))
    design=data['Design']; a=design['Arrangement']; beam=design['Beam']; geo=data['Geometry']
    im=Image.new('RGB',(2200,1700),'white'); d=ImageDraw.Draw(im)
    d.text((45,25),f"SECTION B-B  |  SW {data['SW']:g} / SD {data['SD']:g} / TZ {data['TZ']:g} mm",font=title,fill='#203247')
    facts=[f"{beam['SectionDesignation']} | support span {beam['SpanMm']:.3f} mm | {beam['StructuralStatus']}",f"Concrete {design['ConcreteHeight']:.3f} mm | deflector {design['DeflectorPitchMm']:g} mm | layout {design['LayoutStatus']}"]
    if design['HasCalculatedPath']:
        facts += [f"V = {a['RequiredVerticalCenterSeparationMm']:.3f} mm | hanger = {a['RequiredHangerLengthMm']:.3f} mm | wrap = {a['Reeving']['CalculatedWrapAngleDeg']:.3f} deg / target >=152",f"Actual pitch-circle/slab clearance {a['DeflectorToSlabClearanceMm']:.3f} mm | {a['DeflectorSlabClearanceStatus']}"]
    else: facts += [design['ReevingGeometryStatus'],'Uncalculated rope path suppressed. No wrap/clearance PASS.']
    facts+=['Red: calculated rope path. Hanger: MECHANICAL REVIEW REQUIRED. Concrete: NOT EVALUATED.']
    for i,line in enumerate(facts):d.text((45,76+i*27),line,font=font,fill='#283a4b')
    points=[p for e in geo if e['Role']!='status' for p in e['Points']]
    xmin=min(p['X'] for p in points)-90;xmax=max(p['X'] for p in points)+100
    ymin=min(p['Y'] for p in points)-70;ymax=max(p['Y'] for p in points)+100
    scale=min(1680/(xmax-xmin),1370/(ymax-ymin))
    def xy(p):return (65+(p['X']-xmin)*scale,1600-(p['Y']-ymin)*scale)
    for e in sorted(geo,key=lambda x:0 if x['Hatch'] else 2 if x['Role'] in ('rope','generatedRope') else 1):
        if e['Role']=='status': continue
        pts=[xy(p) for p in e['Points']];color=colors.get(e['Role'],'#737981')
        if e['Hatch'] and len(pts)>3:
            d.polygon(pts,fill='#ece9e2' if e['Role']=='concrete' else '#f0f1f2',outline=color)
        elif e['Role']=='dimension' and len(pts)==3:
            a,m,b=pts
            if abs(a[0]-b[0])>abs(a[1]-b[1]):ends=[(a[0],m[1]),(b[0],m[1])]
            else:ends=[(m[0],a[1]),(m[0],b[1])]
            d.line([a,ends[0]],fill=color);d.line(ends,fill=color);d.line([b,ends[1]],fill=color)
        elif len(pts)>1:d.line(pts,fill=color,width=3 if e['Role'] in ('rope','generatedRope') else 1)
        if e.get('Text') and e.get('TextPosition'):
            content=re.sub(r'\\[^;]*;','',e['Text']).replace('\\P',' ').strip('{}')
            d.text(xy(e['TextPosition']),content,font=small,fill=color)
    d.text((1760,340),'Native geometry audit',font=font,fill='#203247')
    d.text((1760,375),str(data['Audit']['NativePointsChecked'])+' points checked',font=small,fill='#203247')
    d.text((1760,410),'Saved + reopened DWG',font=small,fill='#203247')
    d.text((1760,445),'Complete section extent',font=small,fill='#203247')
    d.text((1760,480),'Curve/dimension diagnostic',font=small,fill='#203247')
    d.text((1760,515),'Not a plot-style proof',font=small,fill='#203247')
    d.text((45,1650),'Opening widths retained pending engineering sizing. UPN outer depth only; no structural or code compliance claimed.',font=font,fill='#526170')
    out=root/f'generated-{number}.png';im.save(out);return im

if __name__=='__main__':
    images=[render(n) for n in (1,10,14,6)]
    comparison=Image.new('RGB',(2200,1700),'white')
    for i,im in enumerate(images):comparison.paste(im.resize((1100,850)),((i%2)*1100,(i//2)*850))
    comparison.save(root/'comparison.png')
