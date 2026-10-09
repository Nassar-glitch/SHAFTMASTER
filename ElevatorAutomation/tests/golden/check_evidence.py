"""Confirms the golden baseline against earlier evidence (ARCHITECTURE-PLAN P0).

Usage: python check_evidence.py <approved folder> <q-sweep.json of output/presets-heavy-20261009/final2> <report.json>
Checks the Q sweep (selection, rope / traction checks and cases, deflector, Design Notes) against the heavy-load evidence
and the fixed sheet fixture (FF1 / FF2 2200 / 2100 N, P5 9800 N, P6 39200 N, P7 29600 / 14800 N).
"""
import json, sys
from pathlib import Path

approved, sweep_file, report_file = Path(sys.argv[1]), Path(sys.argv[2]), Path(sys.argv[3])
failures, checks = [], 0


def check(ok, name):
    global checks
    checks += 1
    if not ok:
        failures.append(name)


def near(a, b):
    return a is not None and b is not None and abs(float(a) - float(b)) <= 1e-9 * max(1.0, abs(float(b)))


for row in json.loads(sweep_file.read_text(encoding='utf-8-sig')):
    name = 'sweep-%s-q%04d' % (row['Shaft'], int(row['Q']))
    r = json.loads((approved / name / 'results.json').read_text(encoding='utf-8'))
    notes = (approved / name / 'design-notes.txt').read_text(encoding='utf-8')
    v, sel, rope = r['Values'], r['Selection'], r['RopeTraction']
    for key, ev in (('SW', 'WidthMm'), ('SD', 'DepthMm'), ('n', 'n'), ('d', 'd'), ('Dt', 'Dt'), ('Beta', 'Beta'), ('BalanceFactor', 'Q_factor'), ('CW', 'CW'), ('P', 'P')):
        check(near(v[key], row[ev]), '%s %s %s = evidence %s' % (name, key, v[key], row[ev]))
    check(sel['Status'] == row['Status'] and sel.get('LimitingCheck') == row['LimitingCheck'], name + ' selection status / limiting check')
    got = [(k['Id'], k['Actual'], k['Required'], k['Ok']) for k in rope['Checks']]
    exp = [(k['Id'], k['Actual'], k['Required'], k['Ok']) for k in row['Checks']]
    check(got == exp, name + ' rope checks')
    gt = {t['Id']: t for t in rope['Traction']}
    for t in row['Traction']:
        g = gt.get(t['Id'])
        check(g is not None and near(g['Ratio'], t['Ratio']) and near(g['EFAlpha'], t['EFAlpha']) and g['Ok'] == t['Ok'], name + ' traction ' + t['Id'])
    check(near(rope['Geometry']['DeflectorPitchMm'], row['Dp']) and (row['DpOverDr'] is None or near(rope['DpOverDr'], row['DpOverDr'])), name + ' deflector Dp and Dp/d')
    for text in row['DesignNotesWarnings'] + row['DesignNotesRecommendations'] + row['DesignNotesNotes']:
        check(text in notes, name + ' Design Notes contains: ' + text[:60])

b = json.loads((approved / 'basic-complete' / 'results.json').read_text(encoding='utf-8'))
ff, pit = b['CarGuideForces'], b['SafetyPit']
check(near(ff['FF1DesignN'], 2200) and near(ff['FF2DesignN'], 2100) and near(ff['FF1BaseN'], 1100) and near(ff['FF2BaseN'], 1050), 'basic-complete FF1 / FF2 design 2200 / 2100 N (base 1100 / 1050 N)')
check(near(pit['P5']['PerComponentLoadN'], 9800), 'basic-complete P5 9800 N per car guide rail')
check(near(pit['P6']['GroupLoadN'], 39200), 'basic-complete P6 39200 N')
check(near(pit['P7']['GroupLoadN'], 29600) and near(pit['P7']['PerComponentLoadN'], 14800), 'basic-complete P7 29600 N total, 14800 N per buffer')
n = json.loads((approved / 'new-project-q630' / 'results.json').read_text(encoding='utf-8'))['Values']
check(n['n'] == 4 and near(n['d'], 11) and near(n['Beta'], 100) and near(n['BalanceFactor'], 0.5) and near(n['CW'], 1015), 'new project Q 630: 4 x 11, beta 100, q 0.5, CW 1015')

report_file.write_text(json.dumps({'Status': 'PASS' if not failures else 'FAIL', 'Checks': checks, 'Failures': failures}, indent=2), encoding='utf-8')
print(('PASS' if not failures else 'FAIL') + ': %d evidence checks' % checks + ('' if not failures else '; ' + '; '.join(failures[:10])))
sys.exit(0 if not failures else 1)
