"""Read-only source check. No DOCX/DWG changes; write evidence only in the test folder."""
import hashlib, json, sys, zipfile
from pathlib import Path
from xml.etree import ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
source = ROOT.parent / 'Elevator_Calculations_GEARBOX_Code_Corrected_Dual_Units.docx'
out = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / 'output/safety-pit-ui-20261006/source-note-verification.json'
blob = source.read_bytes()
digest = hashlib.sha256(blob).hexdigest()
assert digest.upper() == 'ECFD591133F5F97ED923F62A3A3EEA60C2B97A67805E60BCB7131C3DA7FB13DF', 'Source note changed; re-review rule basis.'
with zipfile.ZipFile(source) as doc:
    xml = ET.fromstring(doc.read('word/document.xml'))
ns = {'w': 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}
paragraphs = xml.findall('.//w:p', ns)
def text(i):
    return ''.join(node.text or '' for node in paragraphs[i].findall('.//w:t', ns))
assert [text(i) for i in (58,59,60)] == ['Parachute_Load_Multiplier','10','N/kg']
assert [text(i) for i in (64,65,66)] == ['Gravity_G','9.81','m/s²']
assert [text(i) for i in (67,68)] == ['Buffer_Factor','4']
assert text(105) == 'P5 = (Capacity + Car + Compens) × 10 N/kg'
assert text(109) == 'P6 = (Capacity + Car + Compens) × max(40, Buffer × g) N/kg'
assert text(113) == 'P7 = CW × max(40, Buffer × g) N/kg'
assert '15800 N' in text(106) and '[Per Guide Rail]' in text(106)
assert '63200 N' in text(110) and '[One Buffer]' in text(110)
assert '29600 N' in text(114) and '[One Buffer]' in text(114)
results = {'P5PerCarGuideN': (480+500+600)*10,
           'P6SingleCarBufferN': (480+500+600)*max(40,4*9.81),
           'P7SingleCounterweightBufferN':740*max(40,4*9.81)}
assert results == {'P5PerCarGuideN':15800,'P6SingleCarBufferN':63200,'P7SingleCounterweightBufferN':29600}
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps({'Status':'PASS','SourceSha256':digest,'Results':results,
    'SafetyCoefficientNPerKg':10,'GravityMetresPerSecondSquared':9.81,'BufferFactor':4,
    'BufferCoefficientNPerKg':max(40,4*9.81),'Evidence':{str(i):text(i) for i in (105,106,109,110,113,114)},
    'RuleQualification':'Adopted note equations; no independent manufacturer or standards verification. Multiple-buffer equal sharing is not stated in the note.'},indent=2,ensure_ascii=False),encoding='utf-8')
print('PASS: source equations, units, adopted coefficients, per-component meanings and three note fixtures verified directly.')
