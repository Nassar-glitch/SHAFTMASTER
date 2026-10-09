"""Read the supplied DOCX directly; no generated note text is the rule oracle."""
import argparse
import hashlib
import json
from pathlib import Path
from xml.etree import ElementTree as ET
from zipfile import ZipFile

parser = argparse.ArgumentParser()
parser.add_argument("source", type=Path)
parser.add_argument("output", type=Path)
args = parser.parse_args()
source_bytes = args.source.read_bytes()
with ZipFile(args.source) as doc:
    root = ET.fromstring(doc.read("word/document.xml"))
ns = {"w": "http://schemas.openxmlformats.org/wordprocessingml/2006/main"}
paragraphs = ["".join(text.text or "" for text in node.findall(".//w:t", ns)).strip()
              for node in root.findall(".//w:p", ns)]
expected = {
    78: "A) The Force FF1 (In the direction of the car guide rail axis)",
    79: "FF1 = Capacity x 10 x Car Width / (4 x Vertical Guide Shoe Distance).",
    80: "FF1 = 480 X 10 X 1.10  / (4 X 1.2)  =  1100 N.",
    81: "FF1 (design) = 1100 × 2 = 2200 N.",
    83: "B) The Force FF2 (Perpendicular to the car guide rail axis).",
    84: "FF2 = Capacity x 10 x Car Depth / (8 x Vertical Guide Shoe Distance).",
    85: "FF2 = 480 X 10 X 2.10  / ( 8 X 1.20  ) =   1050 N.",
    86: "FF2 (design) = 1050 × 2 = 2100 N.",
}
for index, text in expected.items():
    assert paragraphs[index] == text, (index, paragraphs[index], text)
ff1 = 480 * 10 * 1.10 / (4 * 1.2)
ff2 = 480 * 10 * 2.10 / (8 * 1.20)
assert abs(ff1 - 1100) < 1e-9 and abs(ff2 - 1050) < 1e-9
assert 2 * ff1 == 2200 and 2 * ff2 == 2100
args.output.parent.mkdir(parents=True, exist_ok=True)
args.output.write_text(json.dumps({
    "Status": "PASS",
    "Source": str(args.source.resolve()),
    "SourceSha256": hashlib.sha256(source_bytes).hexdigest().upper(),
    "ParagraphsZeroBased": expected,
    "IndependentFixtureOnly": {"FF1BaseN": ff1, "FF1DesignN": 2 * ff1,
                               "FF2BaseN": ff2, "FF2DesignN": 2 * ff2},
    "Limitations": "These are adopted note expressions. Axis/per-component interpretation requires separate documented evidence; scalar checks do not certify a rail or its connections.",
}, ensure_ascii=False, indent=2), encoding="utf-8")
print("PASS: supplied DOCX FF1/FF2 expressions, guide-shoe-distance definition, design multiplier and independent scalar fixture.")
