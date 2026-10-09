# Independent hand check of the rope / traction engine (EN 81-20:2014 5.5; EN 81-50:2014 5.11, 5.12, Annex D/E).
# Re-implements the standard's formulas from the PDF (not from the C# engine) and compares every engine value of the
# fixtures in hand-check-fixtures.json. Usage: python verify_rope_traction.py <core evidence folder>
import json, math, sys, pathlib

GN = 9.81
TABLE_V = [(35, 18.5), (36, 16), (38, 12), (40, 10), (42, 8), (45, 6.5), (50, 5)]        # EN 81-50 Table 2, V-grooves by gamma
TABLE_U = [(75, 2.5), (80, 3.0), (85, 3.8), (90, 5.0), (95, 6.7), (100, 10.0), (105, 15.2)]  # Table 2, U-undercut by beta

def interp(table, x):
    if x <= table[0][0]: return table[0][1]
    if x >= table[-1][0]: return table[-1][1]
    for (x0, y0), (x1, y1) in zip(table, table[1:]):
        if x <= x1: return y0 + (y1 - y0) * (x - x0) / (x1 - x0)

def nequiv_t(groove, beta, gamma):
    if groove == 'UndercutU':
        return 1.0 if beta <= 0 else interp(TABLE_U, max(75.0, beta))     # no undercut = 1; beta < 75 uses the 75 value
    return interp(TABLE_V, min(50.0, gamma))                                # gamma > 50 uses the 50 value

def sf_8150(dt_dr, n_equiv):                                                # 5.12.3, Figure 10 formula, log = log10
    return 10 ** (2.6834 - math.log10(695.85e6 * n_equiv / dt_dr ** 8.567) / math.log10(77.09 * dt_dr ** -2.894))

def mu(case, v):                                                            # 5.11.2.3.2
    return {'loading': 0.1, 'braking': 0.1 / (1 + v / 10), 'stalled': 0.2}[case]

def friction(groove, case, m, beta_deg, gamma_deg):                         # 5.11.2.3.1
    b = math.radians(beta_deg or 0); g = math.radians(gamma_deg or 0)
    if groove == 'UndercutU':
        return m * 4 * (math.cos(g / 2) - math.sin(b / 2)) / (math.pi - b - g - math.sin(b) + math.sin(g))
    if groove == 'VHardened' or case == 'stalled':
        return m / math.sin(g / 2)
    return m * 4 * (1 - math.sin(b / 2)) / (math.pi - b - math.sin(b))

def hand(i):
    out = {}
    P, Q, M, n, d, Dt, Dp = i['P'], i['Q'], i['M'], i['n'], i['d'], i['Dt'], i['Dp']
    H, Htr, w, wt, mbl = i['H'], i['Htr'], i['w'], i['wt'], i['MBL']
    alpha = math.radians(i['Alpha']) if i['Alpha'] is not None else None
    if None not in (mbl, w, n, d, Dt, P, Q, H) and (not i['Deflector'] or Dp is not None):
        out['NequivT'] = nequiv_t(i['Groove'], i['Beta'], i['Gamma'])
        out['Kp'] = (Dt / Dp) ** 4 if i['Deflector'] else 1.0
        out['NequivP'] = out['Kp'] * (1 + 4 * 0) if i['Deflector'] else 0.0       # 1:1 + deflector: one simple bend (Annex E.2)
        out['Nequiv'] = out['NequivT'] + out['NequivP']
        out['Sf8150'] = sf_8150(Dt / d, out['Nequiv'])
        out['SfMinimum8120'] = 12.0 if n >= 3 else 16.0                           # EN 81-20 5.5.2.2
        out['SfRequired'] = max(out['SfMinimum8120'], out['Sf8150'])
        out['TmaxN'] = ((P + Q) / n + H * w) * GN                                  # loaded car at the lowest landing, r = 1
        out['SfActual'] = mbl * 1000 / out['TmaxN']
    if alpha is not None and None not in (P, Q, M, w, n, H, Htr, wt):
        msr_car_low = n * H * w; msr_cwt_high = n * H * w; mtrav_high = 0.5 * Htr * wt   # MSR = ns(0.5H ± y)w; MTrav = (0.25H ± 0.5y)·nt·w
        cases = {}
        def pack(case, positions, cond):
            m = mu(case, i['v'] or 0); f = friction(i['Groove'], case, m, i['Beta'], i['Gamma']); efa = math.exp(f * alpha)
            ratios = [t1 / t2 for t1, t2 in positions]
            ok = all(r <= efa for r in ratios) if cond == '≤' else all(r >= efa for r in ratios)
            cases[case] = {'Mu': m, 'F': f, 'EFAlpha': efa, 'Positions': positions, 'Ok': ok}
        pack('loading', [((P + 1.25 * Q) * GN + msr_car_low * GN, M * GN)], '≤')
        if i['v'] and i['a'] is not None and (not i['Deflector'] or i['mDef']):
            a = i['a']; mdp = i['mDef'] / 2 if i['Deflector'] else 0.0             # J = 1/2 m R^2, (v_pulley/v) = 1
            loaded = ((P + Q) * (GN + a) + msr_car_low * (GN + a), M * (GN - a) - mdp * a)
            empty = (M * (GN + a) + msr_cwt_high * (GN + a) + mdp * a, (P + mtrav_high) * (GN - a))
            pack('braking', [loaded, empty], '≤')
        if i['Method'] == 'RopesSlip':
            pack('stalled', [((P + mtrav_high) * GN, msr_cwt_high * GN)], '≥')
        out['Traction'] = cases
    return out

def close(a, b, tol=1e-9):
    return a is not None and b is not None and abs(a - b) <= tol * max(1.0, abs(b))

def main():
    folder = pathlib.Path(sys.argv[1])
    fixtures = json.loads((folder / 'hand-check-fixtures.json').read_text(encoding='utf-8-sig'))
    report, failures = [], []
    for fx in fixtures:
        h = hand(fx['Inputs']); e = fx['Engine']; row = {'Fixture': fx['Name']}
        for key in ('NequivT', 'Kp', 'NequivP', 'Nequiv', 'Sf8150', 'SfMinimum8120', 'SfRequired', 'TmaxN', 'SfActual'):
            if key in h:
                row[key] = round(h[key], 6)
                if not close(e.get(key), h[key]): failures.append(f"{fx['Name']} {key}: engine {e.get(key)} vs hand {h[key]}")
            elif e.get(key) is not None: failures.append(f"{fx['Name']} {key}: engine has a value, hand check says not computable")
        et = {t['Id']: t for t in e['Traction']}
        for case, c in h.get('Traction', {}).items():
            t = et.get(case)
            if t is None: failures.append(f"{fx['Name']} {case}: missing in engine"); continue
            for key in ('Mu', 'F', 'EFAlpha'):
                if not close(t[key], c[key]): failures.append(f"{fx['Name']} {case} {key}: engine {t[key]} vs hand {c[key]}")
            for (t1, t2), q in zip(c['Positions'], t['Positions']):
                if not (close(q['T1N'], t1) and close(q['T2N'], t2)): failures.append(f"{fx['Name']} {case} T1/T2: engine {q['T1N']}/{q['T2N']} vs hand {t1}/{t2}")
            if t['Ok'] != c['Ok']: failures.append(f"{fx['Name']} {case} OK: engine {t['Ok']} vs hand {c['Ok']}")
            row[case] = {'mu': round(c['Mu'], 5), 'f': round(c['F'], 5), 'e^fa': round(c['EFAlpha'], 4), 'T1/T2': [round(a / b, 4) for a, b in c['Positions']], 'OK': c['Ok']}
        if set(et) != set(h.get('Traction', {})): failures.append(f"{fx['Name']} traction cases: engine {sorted(et)} vs hand {sorted(h.get('Traction', {}))}")
        report.append(row)
    status = 'FAIL' if failures else 'PASS'
    (folder / 'hand-check-report.json').write_text(json.dumps({'Status': status, 'Fixtures': len(fixtures), 'Failures': failures, 'Rows': report}, indent=1, ensure_ascii=False), encoding='utf-8')
    for row in report: print(json.dumps(row, ensure_ascii=False))
    print('HAND CHECK', status, len(fixtures), 'fixtures', failures[:5])
    sys.exit(1 if failures else 0)

main()
