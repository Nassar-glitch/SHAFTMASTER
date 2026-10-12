"""Proves that a golden change is only the revision shown as 00 when blank (user decision 2026-10-12).

usage: python -I verify_revision_only.py <approved folder> <mismatch folder> [<summary.md to write>]

For every received sheet.txt / design-notes.txt (one line per paragraph or table cell) the received lines are compared with the
approved lines. Only these differences are allowed:
  * the inserted line pair "Revision" / "00" (the sheet's document block);
  * the inserted line pair "Rev." / "00" (the title block in the page header);
  * the footer line "Rev 00" (inserted, or in place of the empty right footer paragraph);
  * a footer line "Doc. No. <n>" that became "Doc. No. <n> · Rev 00".
Any other inserted, deleted or changed line is a failure. Every other file name in the mismatch folder is a failure.
"""
import difflib
import os
import re
import sys

FOOTER_CHANGED = re.compile(r"^(Doc\. No\. .+) · Rev 00$")


def read(path):
    with open(path, encoding="utf-8", newline="") as f:
        return f.read()


def check(approved, received):
    a = approved.split("\n")
    r = received.split("\n")
    sm = difflib.SequenceMatcher(None, a, r, autojunk=False)
    problems, kinds = [], {"Revision row": 0, "Rev. header row": 0, "footer Rev 00": 0}
    for tag, i1, i2, j1, j2 in sm.get_opcodes():
        if tag == "equal":
            continue
        ins = r[j1:j2]
        old = a[i1:i2]
        if tag == "insert":
            if ins == ["Revision", "00"]:
                kinds["Revision row"] += 1
            elif ins == ["Rev.", "00"]:
                kinds["Rev. header row"] += 1
            elif ins == ["Rev 00"]:
                kinds["footer Rev 00"] += 1
            else:
                problems.append("inserted: " + " | ".join(ins)[:100])
        elif tag == "replace" and old == [""] and ins == ["Rev 00"]:
            kinds["footer Rev 00"] += 1  # the empty right footer paragraph now holds "Rev 00"
        elif tag == "replace" and len(old) == 1 and len(ins) == 1 and FOOTER_CHANGED.match(ins[0]) and FOOTER_CHANGED.match(ins[0]).group(1) == old[0]:
            kinds["footer Rev 00"] += 1
        else:
            problems.append("%s: %s -> %s" % (tag, " | ".join(old)[:60], " | ".join(ins)[:60]))
    return not problems, problems, kinds


def main():
    approved_root, mismatch_root = sys.argv[1], sys.argv[2]
    out = sys.argv[3] if len(sys.argv) > 3 else None
    rows, failures = {}, []
    for folder, _, files in os.walk(mismatch_root):
        for name in files:
            if not name.endswith(".received"):
                continue
            rel = os.path.relpath(os.path.join(folder, name), mismatch_root)[: -len(".received")]
            fixture, base = rel.split(os.sep)[0], os.path.basename(rel)
            if base not in ("sheet.txt", "design-notes.txt"):
                failures.append(rel + ": file type must not change")
                continue
            approved_path = os.path.join(approved_root, rel)
            if not os.path.exists(approved_path):
                failures.append(rel + ": no approved file")
                continue
            ok, problems, kinds = check(read(approved_path), read(os.path.join(folder, name)))
            rows.setdefault(fixture, {})[base] = (ok, kinds)
            for p in problems:
                failures.append(rel + ": " + p)
    lines = ["| fixture | file | revision-only | Revision row | Rev. header row | footer Rev 00 |", "|---|---|---|---|---|---|"]
    for fx in sorted(rows):
        for base in sorted(rows[fx]):
            ok, k = rows[fx][base]
            lines.append("| %s | %s | %s | %d | %d | %d |" % (fx, base, "yes" if ok else "NO", k["Revision row"], k["Rev. header row"], k["footer Rev 00"]))
    text = "\n".join(lines)
    print(text)
    if out:
        with open(out, "w", encoding="utf-8") as f:
            f.write(text + "\n")
    if failures:
        print("FAIL: %d change(s) beyond the revision row / footer:" % len(failures))
        for x in failures[:40]:
            print("  " + x)
        return 1
    print("PASS: %d fixtures, every changed line is the revision row or footer" % len(rows))
    return 0


sys.exit(main())
