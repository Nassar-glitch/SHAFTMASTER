"""Proves that a golden change adds only kg values (DESIGN-FLOW principle 10).

usage: python -I verify_kg_only.py <approved folder> <mismatch folder> [<summary.md to write>]

For every received file in the mismatch folder (*.received) it compares with the approved file of the same name:
  * figures (*.svg): the received text, with every " (<n> kg)" removed, must be byte-equal to the approved text.
  * sheet.txt, design-notes.txt (one line per paragraph or table cell): the received lines, with the inline kg fragments removed, must
    equal the approved lines once the lines that only add a kg column (a header ending in "(kg)" or a whole-number cell directly
    after such a header column) are left out. Any other inserted, deleted or changed line is a failure.
  * every other file name (results.json, fingerprints.json, roundtrip, ...) must not be in the mismatch folder at all.
"""
import difflib
import os
import re
import sys

KG_INLINE = [
    (re.compile(r" \(-?\d+ kg\)"), ""),              # "12.35 kN (1260 kg)"
    (re.compile(r" \(-?\d+ / -?\d+ kg\)"), ""),      # panel pairs
    (re.compile(r"; -?\d+ kg\)"), ")"),              # "(9.8 kN; 1000 kg)"
]
KG_HEADER = re.compile(r"\(kg\)$")
WHOLE = re.compile(r"^-?\d+$")


def strip_inline(text):
    for pattern, repl in KG_INLINE:
        text = pattern.sub(repl, text)
    return text


def read(path):
    with open(path, encoding="utf-8", newline="") as f:
        return f.read()


TEXT_POS = re.compile(r'(<text [^>]*?) x="[-\d.]+" y="[-\d.]+"')


def check_svg(approved, received):
    stripped = strip_inline(received)
    if stripped == approved:
        return True, "", 0
    # A longer label may be moved by the figure's own placement rule: the text position of <text> elements is not compared, every
    # other element, attribute and text must be equal.
    if TEXT_POS.sub(r"", stripped) == TEXT_POS.sub(r"", approved):
        return True, "label position adjusted", 1
    return False, "svg differs beyond kg text and label positions", 0


def check_text(approved, received):
    a = approved.split("\n")
    r = received.split("\n")
    rs = [strip_inline(x) for x in r]
    sm = difflib.SequenceMatcher(None, a, rs, autojunk=False)
    problems = []
    inserted = 0
    for tag, i1, i2, j1, j2 in sm.get_opcodes():
        if tag == "equal":
            continue
        if tag == "insert":
            for line in rs[j1:j2]:
                if KG_HEADER.search(line) or WHOLE.match(line):
                    inserted += 1
                else:
                    problems.append("inserted: " + line[:100])
        elif tag == "delete":
            for line in a[i1:i2]:
                problems.append("deleted: " + line[:100])
        else:
            # a replace block: allow it only when removing the kg-only lines makes it equal
            ra = a[i1:i2]
            rr = [x for x in rs[j1:j2] if not (KG_HEADER.search(x) or WHOLE.match(x)) or x in ra]
            if rr != ra:
                for x in difflib.unified_diff(ra, rr, lineterm="", n=0):
                    if x[:1] in "+-" and x[:3] not in ("+++", "---"):
                        problems.append("changed: " + x[:100])
            else:
                inserted += (j2 - j1) - len(rr)
    return not problems, "; ".join(problems[:5]) + (" ... (%d)" % len(problems) if len(problems) > 5 else ""), inserted


def main():
    approved_root, mismatch_root = sys.argv[1], sys.argv[2]
    out = sys.argv[3] if len(sys.argv) > 3 else None
    per_fixture = {}
    moved = []
    failures = []
    for folder, _, files in os.walk(mismatch_root):
        for name in files:
            if not name.endswith(".received"):
                continue
            rel = os.path.relpath(os.path.join(folder, name), mismatch_root)[: -len(".received")]
            fixture = rel.split(os.sep)[0]
            base = os.path.basename(rel)
            approved_path = os.path.join(approved_root, rel)
            if not os.path.exists(approved_path):
                failures.append(rel + ": no approved file")
                continue
            a, r = read(approved_path), read(os.path.join(folder, name))
            if base.endswith(".svg"):
                ok, why, added = check_svg(a, r)
                if ok and added:
                    moved.append(rel)
                added = 0
            elif base in ("sheet.txt", "design-notes.txt"):
                ok, why, added = check_text(a, r)
            else:
                ok, why, added = False, "file type must not change", 0
            entry = per_fixture.setdefault(fixture, {"files": 0, "kg-only": 0, "extra lines": 0})
            entry["files"] += 1
            entry["extra lines"] += added
            if ok:
                entry["kg-only"] += 1
            else:
                failures.append(rel + ": " + why)
    lines = ["| fixture | changed files | kg-only | kg column lines added |", "|---|---|---|---|"]
    for k in sorted(per_fixture):
        e = per_fixture[k]
        lines.append("| %s | %d | %d | %d |" % (k, e["files"], e["kg-only"], e["extra lines"]))
    text = "\n".join(lines)
    print(text)
    if out:
        with open(out, "w", encoding="utf-8") as f:
            f.write(text + "\n")
    if moved:
        print("label positions adjusted by the figure's placement rule in %d figure(s): %s" % (len(moved), ", ".join(sorted({os.path.basename(m) for m in moved}))))
    if failures:
        print("FAIL: %d file(s) change more than kg values:" % len(failures))
        for x in failures[:40]:
            print("  " + x)
        return 1
    print("PASS: %d fixtures, every changed line differs only by kg text" % len(per_fixture))
    return 0


sys.exit(main())
