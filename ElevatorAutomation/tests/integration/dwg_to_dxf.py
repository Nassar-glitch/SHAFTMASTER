"""DWG -> DXF through the AutoCAD 2023 Core Console, read only, with a timeout and a log.

usage: dwg_to_dxf.py <timeout seconds> <in.dwg> <out.dxf> [<in.dwg> <out.dxf> ...]

The DWG is copied to a short path without spaces (a space ends a script answer), DXFOUT writes there, the DXF is copied back. The
Core Console is killed after the timeout and its log is kept next to the output (<out>.log) so a hang can be diagnosed.
"""
import os
import shutil
import subprocess
import sys
import tempfile

CORE_CONSOLE = r"C:\Program Files\Autodesk\AutoCAD 2023\accoreconsole.exe"


def convert(dwg, dxf, timeout, precision="16"):
    work = tempfile.mkdtemp(prefix="d2x", dir=os.environ.get("PLOT_WORK") or None)
    src, out, scr, log = os.path.join(work, "in.dwg"), os.path.join(work, "out.dxf"), os.path.join(work, "run.scr"), os.path.join(work, "console.log")
    shutil.copyfile(dwg, src)
    with open(scr, "w", newline="") as f:
        f.write("\r\n".join(["FILEDIA", "0", "CMDDIA", "0", "DXFOUT", out, precision, ""]))
    proc = subprocess.Popen([CORE_CONSOLE, "/i", src, "/s", scr, "/l", "en-US"], stdout=open(log, "w"), stderr=subprocess.STDOUT)
    try:
        proc.wait(timeout=timeout)
    except subprocess.TimeoutExpired:
        subprocess.run(["taskkill", "/F", "/T", "/PID", str(proc.pid)], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    if os.path.exists(log):
        shutil.copyfile(log, dxf + ".log")
    ok = os.path.exists(out) and os.path.getsize(out) > 0
    if ok:
        shutil.copyfile(out, dxf)
    shutil.rmtree(work, ignore_errors=True)
    return ok


def main():
    timeout = int(sys.argv[1])
    pairs = sys.argv[2:]
    rc = 0
    for dwg, dxf in zip(pairs[0::2], pairs[1::2]):
        ok = convert(dwg, dxf, timeout)
        print(("ok   " if ok else "FAIL ") + os.path.basename(dwg))
        rc = rc or (0 if ok else 1)
    return rc


sys.exit(main())
