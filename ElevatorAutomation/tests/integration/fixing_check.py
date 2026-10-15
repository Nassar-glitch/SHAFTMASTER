"""G1 round 3b: the main plan and the rail details agree on how each rail group is fixed.

The detail blocks are named SM_RAIL_BRACKET_PLAN_<key>_<CAR|CW>_<WALL|ARM>_G<gap>; the program's own plan symbol (drawn when the
template plan does not show the fixing of the detail) SM_RAIL_FIXING_PLAN_<key>_<CAR|CW>_<WALL|ARM>_G<gap>_SYMBOL. The template plan
draws the car brackets as wall brackets and the counterweight brackets on the rear wall (a cross arm): a group without a symbol must be
fixed that way in the detail, a group with a symbol must have the detail's type and gap.
"""
import re

DETAIL = re.compile(r"^SM_RAIL_BRACKET_PLAN_.*_(CAR|CW)_(WALL|ARM)_G(\d+)$")
SYMBOL = re.compile(r"^SM_RAIL_FIXING_PLAN_.*_(CAR|CW)_(WALL|ARM)_G(\d+)_SYMBOL$")
TEMPLATE = {"CAR": "WALL", "CW": "ARM"}


def check_fixing(doc, case, check):
    names = [e.dxf.name for e in doc.modelspace() if e.dxftype() == "INSERT"]
    detail, symbol = {}, {}
    for n in names:
        m = DETAIL.match(n)
        if m:
            detail[m.group(1)] = (m.group(2), int(m.group(3)))
        m = SYMBOL.match(n)
        if m:
            symbol[m.group(1)] = (m.group(2), int(m.group(3)))
    check(set(detail) == {"CAR", "CW"}, case + ": the details draw the fixing of the car and the counterweight rails " + str(sorted(detail)))
    for fam, (typ, gap) in sorted(detail.items()):
        if fam in symbol:
            check(symbol[fam] == (typ, gap), case + ": the plan symbol of the " + fam + " rails equals the detail " + str((typ, gap)) + " " + str(symbol[fam]))
        else:
            check(TEMPLATE[fam] == typ, case + ": the template plan draws the " + fam + " rails as " + TEMPLATE[fam] + ", the detail is " + typ)
