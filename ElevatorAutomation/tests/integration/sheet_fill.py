"""L2c evidence: the fill and the largest empty rectangle of generated sheets, from the layouts-native *-expected.json files.

usage: sheet_fill.py <evidence folder> [<case id part> ...]

Fill = viewport area / drawing area. Largest empty = the largest axis-aligned rectangle of the drawing area (5 mm grid, a 10 mm
margin all round, every viewport grown by 5 mm) that holds no viewport, as a share of the drawing area (the same measure as
SheetArrangementTests.LargestEmpty).
"""
import glob
import json
import os
import sys

CELL = 5.0
GAP = 10.0


def largest_empty(area, boxes):
    ax, ay, aw, ah = area
    nx, ny = int(-(-aw // CELL)), int(-(-ah // CELL))
    used = [[False] * nx for _ in range(ny)]
    for (x, y, w, h) in boxes:
        x, y, w, h = x - GAP / 2, y - GAP / 2, w + GAP, h + GAP
        x0, x1 = max(0, int((x - ax) // CELL)), min(nx, int(-(-(x + w - ax) // CELL)))
        y0, y1 = max(0, int((y - ay) // CELL)), min(ny, int(-(-(y + h - ay) // CELL)))
        for j in range(y0, y1):
            for i in range(x0, x1):
                used[j][i] = True
    m = int(-(-GAP // CELL))
    best = 0
    heights = [0] * nx
    for j in range(m, ny - m):
        for i in range(nx):
            heights[i] = 0 if (i < m or i >= nx - m or used[j][i]) else heights[i] + 1
        stack = []
        for i in range(nx + 1):
            h = 0 if i == nx else heights[i]
            while stack and heights[stack[-1]] >= h:
                top = heights[stack.pop()]
                left = stack[-1] if stack else -1
                best = max(best, top * (i - left - 1))
            stack.append(i)
    return best * CELL * CELL / (aw * ah)


def main(folder, parts):
    for f in sorted(glob.glob(os.path.join(folder, "*-expected.json"))):
        d = json.load(open(f, encoding="utf-8-sig"))
        if parts and not any(p in d["Id"] for p in parts):
            continue
        a = d["Area"]
        area = (a["DrawingAreaX"], a["DrawingAreaY"], a["DrawingAreaWidth"], a["DrawingAreaHeight"])
        boxes = [(v["PaperX"], v["PaperY"], v["PaperWidth"], v["PaperHeight"]) for v in d["Viewports"]]
        fill = sum(w * h for (_, _, w, h) in boxes) / (area[2] * area[3])
        print("%-28s fill %.2f  largest empty %.3f  %s" % (d["Id"], fill, largest_empty(area, boxes),
              " / ".join("%s 1:%d" % (v["Name"], v["ScaleDenominator"]) for v in d["Viewports"])))


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2:])
