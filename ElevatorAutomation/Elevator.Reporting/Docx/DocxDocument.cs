using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Text;

namespace Elevator.Reporting.Docx
{
    // Minimal WordprocessingML writer. Produces an editable .docx with System.IO.Packaging only;
    // Microsoft Word is not required to create the file.
    public sealed class DocxDocument
    {
        const string W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        const string R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        const string WP = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
        const string A = "http://schemas.openxmlformats.org/drawingml/2006/main";
        const string PIC = "http://schemas.openxmlformats.org/drawingml/2006/picture";
        readonly StringBuilder body = new StringBuilder();
        public string Title;
        public string Subject;

        // CAD-style title block repeated in the header of every page, and a bordered footer band.
        public DocxTitleBlock TitleBlock = new DocxTitleBlock();
        public string FooterLeft;
        public string FooterRight;
        readonly List<DocxImage> media = new List<DocxImage>();
        readonly Dictionary<DocxImage, string> documentImages = new Dictionary<DocxImage, string>();
        readonly Dictionary<DocxImage, string> headerImages = new Dictionary<DocxImage, string>();
        int drawingId;

        string Relation(DocxImage image, bool header)
        {
            if (!media.Contains(image))
                media.Add(image);
            var map = header ? headerImages : documentImages;
            string id;
            if (!map.TryGetValue(image, out id))
            {
                id = "rIdImg" + (map.Count + 1);
                map[image] = id;
            }
            return id;
        }

        // Inline picture run sized in mm (1 mm = 36000 EMU).
        string Drawing(DocxImage image, double widthMm, double heightMm, bool header)
        {
            string rel = Relation(image, header);
            int id = ++drawingId;
            long cx = (long)Math.Round(widthMm * 36000), cy = (long)Math.Round(heightMm * 36000);
            return "<w:r><w:drawing><wp:inline distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\"><wp:extent cx=\""
                + cx
                + "\" cy=\""
                + cy
                + "\"/><wp:docPr id=\""
                + id
                + "\" name=\"Picture "
                + id
                + "\"/>"
             + "<wp:cNvGraphicFramePr><a:graphicFrameLocks noChangeAspect=\"1\"/></wp:cNvGraphicFramePr><a:graphic><a:graphicData uri=\""
                 + PIC
                 + "\"><pic:pic><pic:nvPicPr><pic:cNvPr id=\""
                 + id
                 + "\" name=\"image"
                 + id
                 + "."
                 + image.Extension
                 + "\"/><pic:cNvPicPr/></pic:nvPicPr>"
             + "<pic:blipFill><a:blip r:embed=\""
                 + rel
                 + "\">"
                 + (image.SvgData == null
                     ? ""
                     : "<a:extLst><a:ext uri=\"{96DAC541-7B7A-43D3-8B79-37D633B846F1}\"><asvg:svgBlip "
                         + "xmlns:asvg=\"http://schemas.microsoft.com/office/drawing/2016/SVG/main\" r:embed=\""
                     + rel
                     + "s\"/></a:ext></a:extLst>")
                 + "</a:blip><a:stretch><a:fillRect/></a:stretch></pic:blipFill><pic:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\""
                 + cx
                 + "\" cy=\""
                 + cy
                 + "\"/></a:xfrm><a:prstGeom "
                 + "prst=\"rect\"><a:avLst/></a:prstGeom></pic:spPr></pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r>";
        }

        // Body image fitted into maxWidthMm × maxHeightMm, keeping its aspect ratio. align: left, center or right.
        public void Image(DocxImage image, double maxWidthMm, double maxHeightMm, string align = "left", bool keepNext = false)
        {
            double w, h;
            image.Fit(maxWidthMm, maxHeightMm, out w, out h);
            body.Append("<w:p><w:pPr>")
                .Append(keepNext ? "<w:keepNext/>" : "")
                .Append("<w:jc w:val=\"")
                .Append(align)
                .Append("\"/></w:pPr>")
                .Append(Drawing(image, w, h, false))
                .Append("</w:p>");
        }

        public static string Escape(string text)
        {
            if (text == null)
                return "";
            var b = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (c == '&')
                    b.Append("&amp;");
                else if (c == '<')
                    b.Append("&lt;");
                else if (c == '>')
                    b.Append("&gt;");
                else if (c == '"')
                    b.Append("&quot;");
                else if (c < 0x20 && c != '\t' && c != '\n' && c != '\r')
                    continue;
                else if (c >= 0xFFFE)
                    continue;
                else
                    b.Append(c);
            }
            return b.ToString();
        }

        static string Runs(string text, string runProperties)
        {
            var b = new StringBuilder();
            var lines = (text ?? "").Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                b.Append("<w:r>").Append(runProperties == null ? "" : "<w:rPr>" + runProperties + "</w:rPr>");
                if (i > 0)
                    b.Append("<w:br/>");
                b.Append("<w:t xml:space=\"preserve\">").Append(Escape(lines[i])).Append("</w:t></w:r>");
            }
            return b.ToString();
        }

        public void Paragraph(
            string text,
            string style = null,
            string runProperties = null,
            bool keepNext = false,
            string extraParagraphProperties = null)
        {
            body.Append("<w:p><w:pPr>");
            if (style != null)
                body.Append("<w:pStyle w:val=\"").Append(style).Append("\"/>");
            if (keepNext)
                body.Append("<w:keepNext/>");
            if (extraParagraphProperties != null)
                body.Append(extraParagraphProperties);
            body.Append("</w:pPr>").Append(Runs(text, runProperties)).Append("</w:p>");
        }

        // Label/value runs within one paragraph.
        public void Mixed(IEnumerable<KeyValuePair<string, string>> parts, string style = null, bool keepNext = false)
        {
            body.Append("<w:p><w:pPr>");
            if (style != null)
                body.Append("<w:pStyle w:val=\"").Append(style).Append("\"/>");
            if (keepNext)
                body.Append("<w:keepNext/>");
            body.Append("</w:pPr>");
            foreach (var part in parts)
                body.Append(Runs(part.Value, part.Key));
            body.Append("</w:p>");
        }

        public void Heading(string text, int level)
        {
            Paragraph(text, "Heading" + level, null, true);
        }

        public void Banner(string text, string fillHex, string colourHex)
        {
            Paragraph(
                text,
                "Banner",
                "<w:b/><w:color w:val=\"" + colourHex + "\"/>",
                false,
                "<w:pBdr><w:top w:val=\"single\" w:sz=\"12\" w:space=\"4\" w:color=\""
                + colourHex
                + "\"/><w:left w:val=\"single\" w:sz=\"12\" w:space=\"4\" w:color=\""
                + colourHex
                + "\"/><w:bottom w:val=\"single\" w:sz=\"12\" w:space=\"4\" w:color=\""
                + colourHex
                + "\"/><w:right w:val=\"single\" w:sz=\"12\" w:space=\"4\" w:color=\""
                + colourHex
                + "\"/></w:pBdr><w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\""
                + fillHex
                + "\"/>");
        }

        // Equation block: each line kept together with the next so a derivation is not split across pages.
        public void Equations(IEnumerable<string> lines)
        {
            Equations(false, lines);
        }

        // keepWithFollowing also keeps the last line with the next block (for example its result table).
        public void Equations(bool keepWithFollowing, IEnumerable<string> lines)
        {
            var list = lines.Where(l => l != null).ToList();
            for (int i = 0; i < list.Count; i++)
                Paragraph(list[i], "Equation", null, !Flow && (keepWithFollowing || i < list.Count - 1), "<w:keepLines/>");
        }

        // Figure in a thin bordered frame with a bottom title strip: caption | units | scale note (NTS).
        public void FigureFrame(DocxImage image, double widthMm, double heightMm, string caption, string units, string scaleNote)
        {
            int[] w = { 5200, 2838, 1600 };
            body.Append("<w:tbl><w:tblPr><w:tblW w:w=\"9638\" w:type=\"dxa\"/><w:tblLayout w:type=\"fixed\"/><w:tblBorders>");
            foreach (var side in new[] { "top", "left", "bottom", "right", "insideH", "insideV" })
                body.Append("<w:").Append(side).Append(" w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"6B7B8C\"/>");
            body.Append(
                "</w:tblBorders><w:tblCellMar><w:left w:w=\"80\" w:type=\"dxa\"/><w:right w:w=\"80\" "
                + "w:type=\"dxa\"/></w:tblCellMar></w:tblPr><w:tblGrid>");
            foreach (int x in w)
                body.Append("<w:gridCol w:w=\"").Append(x).Append("\"/>");
            body.Append(
                "</w:tblGrid><w:tr><w:trPr><w:cantSplit/></w:trPr><w:tc><w:tcPr><w:tcW w:w=\"9638\" w:type=\"dxa\"/><w:gridSpan "
                + "w:val=\"3\"/></w:tcPr><w:p><w:pPr><w:keepNext/><w:keepLines/><w:spacing w:before=\"60\" w:after=\"60\"/><w:jc "
                + "w:val=\"center\"/></w:pPr>")
                .Append(Drawing(image, widthMm, heightMm, false))
                .Append("</w:p></w:tc></w:tr>");
            body.Append("<w:tr><w:trPr><w:cantSplit/></w:trPr>");
            Action<int, int, string, bool, string> cell = (width, span, text, bold, jc) => body.Append("<w:tc><w:tcPr><w:tcW w:w=\"")
                .Append(width)
                .Append("\" w:type=\"dxa\"/>")
                .Append(span > 1 ? "<w:gridSpan w:val=\"" + span + "\"/>" : "")
                .Append(
                    "<w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"EEF3F8\"/><w:vAlign w:val=\"center\"/></w:tcPr><w:p><w:pPr><w:pStyle "
                    + "w:val=\"TableText\"/><w:jc w:val=\"")
                .Append(jc)
                .Append("\"/></w:pPr>")
                .Append(Runs(text, bold ? "<w:b/>" : null))
                .Append("</w:p></w:tc>");
            cell(w[0], 1, caption, true, "left");
            if (String.IsNullOrEmpty(scaleNote))
                cell(w[1] + w[2], 2, units, false, "right");
            else
            {
                cell(w[1], 1, units, false, "center");
                cell(w[2], 1, scaleNote, true, "center");
            }
            body.Append("</w:tr></w:tbl>");
            Paragraph("", "TableGap");
        }

        static readonly int[] TitleColumns = { 1500, 3000, 3038, 2100 };

        static string SizedRuns(string text, int halfPoints, string colour, bool bold)
        {
            return Runs(text, (bold ? "<w:b/>" : "")
                + "<w:color w:val=\""
                + colour
                + "\"/><w:sz w:val=\""
                + halfPoints
                + "\"/><w:szCs w:val=\""
                + halfPoints
                + "\"/>");
        }

        static string SizedField(string instruction, int halfPoints)
        {
            string rpr = "<w:rPr><w:sz w:val=\"" + halfPoints + "\"/><w:szCs w:val=\"" + halfPoints + "\"/></w:rPr>";
            return "<w:r>"
                + rpr
                + "<w:fldChar w:fldCharType=\"begin\"/></w:r><w:r>"
                + rpr
                + "<w:instrText xml:space=\"preserve\"> "
                + instruction
                + " </w:instrText></w:r><w:r>"
                + rpr
                + "<w:fldChar w:fldCharType=\"separate\"/></w:r><w:r>"
                + rpr
                + "<w:t>1</w:t></w:r><w:r>"
                + rpr
                + "<w:fldChar w:fldCharType=\"end\"/></w:r>";
        }

        const string Tight = "<w:spacing w:before=\"0\" w:after=\"0\" w:line=\"240\" w:lineRule=\"auto\"/>";
        const string Spacer = "<w:p><w:pPr><w:spacing w:before=\"0\" w:after=\"0\" w:line=\"20\" w:lineRule=\"exact\"/></w:pPr></w:p>";

        // Nested label/value table: 8 pt labels on a light shade, 9 pt values, thin borders. Rows with blank values never reach here.
        static string LabelTable(int width, IList<KeyValuePair<string, string>> rows, bool page)
        {
            int inner = width - 120, label = (int)(inner * 0.36), value = inner - label;
            var b = new StringBuilder();
            b.Append("<w:tbl><w:tblPr><w:tblW w:w=\"")
                .Append(inner)
                .Append("\" w:type=\"dxa\"/><w:tblLayout w:type=\"fixed\"/><w:tblBorders>");
            foreach (var side in new[] { "top", "left", "bottom", "right", "insideH", "insideV" })
                b.Append("<w:").Append(side).Append(" w:val=\"single\" w:sz=\"2\" w:space=\"0\" w:color=\"9AA9B8\"/>");
            b.Append(
                "</w:tblBorders><w:tblCellMar><w:top w:w=\"10\" w:type=\"dxa\"/><w:left w:w=\"50\" w:type=\"dxa\"/><w:bottom w:w=\"10\" "
                + "w:type=\"dxa\"/><w:right w:w=\"50\" w:type=\"dxa\"/></w:tblCellMar></w:tblPr><w:tblGrid><w:gridCol w:w=\"")
                .Append(label)
                .Append("\"/><w:gridCol w:w=\"")
                .Append(value)
                .Append("\"/></w:tblGrid>");
            Action<string, string> row = (l, v) => b.Append("<w:tr><w:trPr><w:cantSplit/></w:trPr><w:tc><w:tcPr><w:tcW w:w=\"")
                .Append(label)
                .Append(
                    "\" w:type=\"dxa\"/><w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"E8EEF4\"/><w:vAlign "
                    + "w:val=\"center\"/></w:tcPr><w:p><w:pPr>"
                    + Tight
                    + "</w:pPr>")
                .Append(SizedRuns(l, 16, "4A5968", false))
                .Append("</w:p></w:tc><w:tc><w:tcPr><w:tcW w:w=\"")
                .Append(value)
                .Append("\" w:type=\"dxa\"/><w:vAlign w:val=\"center\"/></w:tcPr><w:p><w:pPr>" + Tight + "</w:pPr>")
                .Append(v)
                .Append("</w:p></w:tc></w:tr>");
            foreach (var r in rows)
                row(r.Key, SizedRuns(r.Value, 18, "1F2A36", false));
            if (page)
                row("Page", SizedField("PAGE", 18) + SizedRuns(" of ", 18, "1F2A36", false) + SizedField("NUMPAGES", 18));
            b.Append("</w:tbl>").Append(Spacer);
            return b.ToString();
        }

        string TitleBlockXml()
        {
            var t = TitleBlock;
            var b = new StringBuilder();
            int[] c = TitleColumns;
            b.Append("<w:tbl><w:tblPr><w:tblW w:w=\"9638\" w:type=\"dxa\"/><w:tblLayout w:type=\"fixed\"/><w:tblBorders>");
            foreach (var side in new[] { "top", "left", "bottom", "right" })
                b.Append("<w:").Append(side).Append(" w:val=\"single\" w:sz=\"12\" w:space=\"0\" w:color=\"1F2A36\"/>");
            b.Append(
                "<w:insideV w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"1F2A36\"/></w:tblBorders><w:tblCellMar><w:top w:w=\"40\" "
                + "w:type=\"dxa\"/><w:left w:w=\"60\" w:type=\"dxa\"/><w:bottom w:w=\"40\" w:type=\"dxa\"/><w:right w:w=\"60\" "
                + "w:type=\"dxa\"/></w:tblCellMar></w:tblPr><w:tblGrid>");
            foreach (int x in c)
                b.Append("<w:gridCol w:w=\"").Append(x).Append("\"/>");
            b.Append("</w:tblGrid><w:tr><w:trPr><w:trHeight w:val=\"1020\" w:hRule=\"exact\"/><w:cantSplit/></w:trPr>");
            Action<int, int, string> cell = (width, span, inner) => b.Append("<w:tc><w:tcPr><w:tcW w:w=\"")
                .Append(width)
                .Append("\" w:type=\"dxa\"/>")
                .Append(span > 1 ? "<w:gridSpan w:val=\"" + span + "\"/>" : "")
                .Append("<w:vAlign w:val=\"center\"/></w:tcPr>")
                .Append(inner)
                .Append("</w:tc>");
            string title = (String.IsNullOrEmpty(t.Company) ? "" : "<w:p><w:pPr>"
                + Tight
                + "</w:pPr>"
                + SizedRuns(t.Company, 18, "073365", true)
                + "</w:p>") + "<w:p><w:pPr>" + Tight + "</w:pPr>" + SizedRuns(t.Title, 21, "1F2A36", true) + "</w:p>";
            if (t.Logo != null)
            {
                double w, h;
                t.Logo.Fit(c[0] / 56.7 - 2.6, 15, out w, out h);
                cell(c[0], 1, "<w:p><w:pPr>" + Tight + "<w:jc w:val=\"center\"/></w:pPr>" + Drawing(t.Logo, w, h, true) + "</w:p>");
                cell(c[1], 1, title);
            }
            else
                cell(c[0] + c[1], 2, title); // no logo: the company name and title use the logo space
            cell(c[2], 1, t.ProjectRows.Count == 0 ? "<w:p><w:pPr>" + Tight + "</w:pPr></w:p>" : LabelTable(c[2], t.ProjectRows, false));
            cell(c[3], 1, LabelTable(c[3], t.DocumentRows, true));
            b.Append("</w:tr></w:tbl>").Append(Spacer);
            return b.ToString();
        }

        string FooterXml()
        {
            var b = new StringBuilder();
            b.Append("<w:tbl><w:tblPr><w:tblW w:w=\"9638\" w:type=\"dxa\"/><w:tblLayout w:type=\"fixed\"/><w:tblBorders>");
            foreach (var side in new[] { "top", "left", "bottom", "right" })
                b.Append("<w:").Append(side).Append(" w:val=\"single\" w:sz=\"8\" w:space=\"0\" w:color=\"1F2A36\"/>");
            b.Append(
                "</w:tblBorders><w:tblCellMar><w:left w:w=\"80\" w:type=\"dxa\"/><w:right w:w=\"80\" "
                + "w:type=\"dxa\"/></w:tblCellMar></w:tblPr><w:tblGrid><w:gridCol w:w=\"5638\"/><w:gridCol "
                + "w:w=\"4000\"/></w:tblGrid><w:tr><w:trPr><w:trHeight w:val=\"340\" w:hRule=\"exact\"/><w:cantSplit/></w:trPr>");
            b.Append(
                "<w:tc><w:tcPr><w:tcW w:w=\"5638\" w:type=\"dxa\"/><w:vAlign w:val=\"center\"/></w:tcPr><w:p><w:pPr>" + Tight + "</w:pPr>")
                .Append(String.IsNullOrEmpty(FooterLeft) ? "" : SizedRuns(FooterLeft, 16, "4A5968", false))
                .Append("</w:p></w:tc>");
            b.Append("<w:tc><w:tcPr><w:tcW w:w=\"4000\" w:type=\"dxa\"/><w:vAlign w:val=\"center\"/></w:tcPr><w:p><w:pPr>"
                + Tight
                + "<w:jc w:val=\"right\"/></w:pPr>")
                .Append(String.IsNullOrEmpty(FooterRight) ? "" : SizedRuns(FooterRight, 16, "4A5968", false))
                .Append("</w:p></w:tc></w:tr></w:tbl>")
                .Append(Spacer);
            return b.ToString();
        }

        // Flow: equation lines and table rows may break across pages (headings still keep with their first line).
        public bool Flow;

        // widths: twentieths of a point (dxa); total ≤ 9638 for A4 with 20 mm margins.
        public void Table(string[] headers, IList<string[]> rows, int[] widths, ISet<int> boldColumns = null, IList<string> rowFills = null)
        {
            Table(headers, rows, widths, boldColumns, rowFills, 0);
        }

        // minRowHeightTwips > 0 gives body rows a minimum height (for example space to sign by hand).
        public void Table(
            string[] headers,
            IList<string[]> rows,
            int[] widths,
            ISet<int> boldColumns,
            IList<string> rowFills,
            int minRowHeightTwips)
        {
            rowHeight = minRowHeightTwips;
            int total = widths.Sum();
            body.Append("<w:tbl><w:tblPr><w:tblStyle w:val=\"CalcTable\"/><w:tblW w:w=\"")
                .Append(total)
                .Append("\" w:type=\"dxa\"/><w:tblLayout w:type=\"fixed\"/>");
            body.Append("<w:tblBorders>");
            foreach (var side in new[] { "top", "left", "bottom", "right", "insideH", "insideV" })
                body.Append("<w:").Append(side).Append(" w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"9AA9B8\"/>");
            body.Append("</w:tblBorders>");
            body.Append(
                "<w:tblCellMar><w:left w:w=\"80\" w:type=\"dxa\"/><w:right w:w=\"80\" "
                + "w:type=\"dxa\"/></w:tblCellMar></w:tblPr><w:tblGrid>");
            foreach (int w in widths)
                body.Append("<w:gridCol w:w=\"").Append(w).Append("\"/>");
            body.Append("</w:tblGrid>");
            // Tables up to 12 rows are kept on one page (keepNext through all but the last row); longer ones break with a repeated header.
            bool together = !Flow && rows.Count <= 12;
            if (headers != null)
                Row(headers, widths, true, "DCE6F0", null, true);
            for (int i = 0; i < rows.Count; i++)
                Row(
                    rows[i],
                    widths,
                    false,
                    rowFills != null && i < rowFills.Count ? rowFills[i] : null,
                    boldColumns,
                    together && i < rows.Count - 1);
            body.Append("</w:tbl>");
            Paragraph("", "TableGap");
            rowHeight = 0;
        }

        int rowHeight;

        void Row(string[] cells, int[] widths, bool header, string fill, ISet<int> bold, bool keepNext)
        {
            body.Append("<w:tr><w:trPr><w:cantSplit/>")
                .Append(header ? "<w:tblHeader/>" : "")
                .Append(!header && rowHeight > 0 ? "<w:trHeight w:val=\"" + rowHeight + "\" w:hRule=\"atLeast\"/>" : "")
                .Append("</w:trPr>");
            for (int c = 0; c < widths.Length; c++)
            {
                string text = c < cells.Length ? cells[c] : "";
                body.Append("<w:tc><w:tcPr><w:tcW w:w=\"").Append(widths[c]).Append("\" w:type=\"dxa\"/>");
                if (!header && rowHeight > 0)
                    body.Append("<w:vAlign w:val=\"center\"/>");
                if (fill != null)
                    body.Append("<w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"").Append(fill).Append("\"/>");
                body.Append("</w:tcPr>");
                body.Append("<w:p><w:pPr><w:pStyle w:val=\"TableText\"/>")
                    .Append(keepNext ? "<w:keepNext/>" : "")
                    .Append("</w:pPr>")
                    .Append(Runs(text, header || (bold != null && bold.Contains(c)) ? "<w:b/>" : null))
                    .Append("</w:p></w:tc>");
            }
            body.Append("</w:tr>");
        }

        static string Styles()
        {
            Func<string, string, string, string, string> style =
                (id, name, pPr, rPr) => "<w:style w:type=\"paragraph\" w:customStyle=\"1\" w:styleId=\""
                + id
                + "\"><w:name w:val=\""
                + name
                + "\"/><w:basedOn w:val=\"Normal\"/><w:qFormat/><w:pPr>"
                + pPr
                + "</w:pPr><w:rPr>"
                + rPr
                + "</w:rPr></w:style>";
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><w:styles xmlns:w=\"" + W + "\">"
             + "<w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"Calibri\" w:hAnsi=\"Calibri\" w:eastAsia=\"Calibri\" "
             + "w:cs=\"Calibri\"/><w:sz w:val=\"20\"/><w:szCs w:val=\"20\"/><w:lang "
             + "w:val=\"en-GB\"/></w:rPr></w:rPrDefault><w:pPrDefault><w:pPr><w:spacing w:after=\"80\" w:line=\"264\" "
             + "w:lineRule=\"auto\"/></w:pPr></w:pPrDefault></w:docDefaults>"
             + "<w:style w:type=\"paragraph\" w:default=\"1\" w:styleId=\"Normal\"><w:name w:val=\"Normal\"/><w:qFormat/><w:rPr><w:color "
             + "w:val=\"1F2A36\"/></w:rPr></w:style>"
             + "<w:style w:type=\"paragraph\" w:styleId=\"Title\"><w:name w:val=\"Title\"/><w:basedOn "
             + "w:val=\"Normal\"/><w:qFormat/><w:pPr><w:spacing w:after=\"60\"/></w:pPr><w:rPr><w:b/><w:color w:val=\"073365\"/><w:sz "
             + "w:val=\"36\"/></w:rPr></w:style>"
             + "<w:style w:type=\"paragraph\" w:styleId=\"Heading1\"><w:name w:val=\"heading 1\"/><w:basedOn w:val=\"Normal\"/><w:next "
             + "w:val=\"Normal\"/><w:qFormat/><w:pPr><w:keepNext/><w:keepLines/><w:pBdr><w:bottom w:val=\"single\" w:sz=\"6\" "
             + "w:space=\"2\" w:color=\"0085DA\"/></w:pBdr><w:spacing w:before=\"280\" w:after=\"120\"/><w:outlineLvl "
             + "w:val=\"0\"/></w:pPr><w:rPr><w:b/><w:color w:val=\"073365\"/><w:sz w:val=\"28\"/></w:rPr></w:style>"
             + "<w:style w:type=\"paragraph\" w:styleId=\"Heading2\"><w:name w:val=\"heading 2\"/><w:basedOn w:val=\"Normal\"/><w:next "
             + "w:val=\"Normal\"/><w:qFormat/><w:pPr><w:keepNext/><w:keepLines/><w:spacing w:before=\"200\" w:after=\"80\"/><w:outlineLvl "
             + "w:val=\"1\"/></w:pPr><w:rPr><w:b/><w:color w:val=\"0B4F8A\"/><w:sz w:val=\"23\"/></w:rPr></w:style>"
             + "<w:style w:type=\"paragraph\" w:styleId=\"Heading3\"><w:name w:val=\"heading 3\"/><w:basedOn w:val=\"Normal\"/><w:next "
             + "w:val=\"Normal\"/><w:qFormat/><w:pPr><w:keepNext/><w:keepLines/><w:spacing w:before=\"140\" w:after=\"60\"/><w:outlineLvl "
             + "w:val=\"2\"/></w:pPr><w:rPr><w:b/><w:color w:val=\"1F2A36\"/><w:sz w:val=\"21\"/></w:rPr></w:style>"
             + style(
                 "Equation",
                 "Calculation equation",
                 "<w:ind w:left=\"284\"/><w:spacing w:after=\"30\"/><w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"F3F6F9\"/>",
                 "<w:rFonts w:ascii=\"Cambria Math\" w:hAnsi=\"Cambria Math\" w:cs=\"Cambria Math\"/><w:sz w:val=\"19\"/>")
             + style(
                 "TableText",
                 "Table text",
                 "<w:spacing w:before=\"20\" w:after=\"20\" w:line=\"240\" w:lineRule=\"auto\"/>",
                 "<w:sz w:val=\"17\"/>")
             + style("TableGap", "Table gap", "<w:spacing w:after=\"60\" w:line=\"120\" w:lineRule=\"exact\"/>", "<w:sz w:val=\"4\"/>")
             + style("Small", "Small note", "<w:spacing w:after=\"60\"/>", "<w:color w:val=\"4A5968\"/><w:sz w:val=\"17\"/>")
             + style(
                 "Issue",
                 "Calculation issue",
                 "<w:ind w:left=\"284\" w:hanging=\"170\"/><w:spacing w:after=\"40\"/>",
                 "<w:color w:val=\"9B1C1C\"/><w:sz w:val=\"18\"/>")
             + style(
                 "Banner",
                 "Status banner",
                 "<w:jc w:val=\"center\"/><w:spacing w:before=\"120\" w:after=\"160\"/>",
                 "<w:sz w:val=\"24\"/>")
             + style(
                 "HeaderText",
                 "Header text",
                 "<w:spacing w:after=\"0\"/><w:pBdr><w:bottom w:val=\"single\" w:sz=\"4\" w:space=\"2\" w:color=\"9AA9B8\"/></w:pBdr>",
                 "<w:color w:val=\"4A5968\"/><w:sz w:val=\"15\"/>")
             + style(
                 "FooterText",
                 "Footer text",
                 "<w:spacing w:after=\"0\"/><w:pBdr><w:top w:val=\"single\" w:sz=\"4\" w:space=\"2\" w:color=\"9AA9B8\"/></w:pBdr>",
                 "<w:color w:val=\"4A5968\"/><w:sz w:val=\"15\"/>")
             + "<w:style w:type=\"table\" w:styleId=\"CalcTable\"><w:name w:val=\"Calculation table\"/><w:tblPr><w:tblInd w:w=\"0\" "
             + "w:type=\"dxa\"/></w:tblPr></w:style>"
             + "</w:styles>";
        }

        static string Field(string instruction)
        {
            return "<w:r><w:fldChar w:fldCharType=\"begin\"/></w:r><w:r><w:instrText xml:space=\"preserve\"> "
                + instruction
                + " </w:instrText></w:r><w:r><w:fldChar w:fldCharType=\"separate\"/></w:r><w:r><w:t>1</w:t></w:r><w:r><w:fldChar "
                + "w:fldCharType=\"end\"/></w:r>";
        }

        static void WritePart(Package package, Uri uri, string contentType, string xml)
        {
            var part = package.CreatePart(uri, contentType, CompressionOption.Normal);
            using (var stream = part.GetStream(FileMode.Create, FileAccess.Write))
            {
                var bytes = new UTF8Encoding(false).GetBytes(xml);
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        public void Save(Stream output)
        {
            using (var package = Package.Open(output, FileMode.Create, FileAccess.ReadWrite))
            {
                var documentUri = new Uri("/word/document.xml", UriKind.Relative);
                string headerXml = TitleBlockXml(); // registers header images before the media parts are written
                                                    // A4 portrait; drawing frame as a page border ~10 mm from the paper edge: thin (0.5 pt)
                                                    // outer line, thick (1.5 pt) inner line (Word draws thickThin top/left and thinThick
                                                    // bottom/right with the thin line outside).
                string frame =
                    "<w:pgBorders w:offsetFrom=\"page\"><w:top w:val=\"thickThinSmallGap\" w:sz=\"12\" w:space=\"27\" "
                    + "w:color=\"1F2A36\"/><w:left w:val=\"thickThinSmallGap\" w:sz=\"12\" w:space=\"27\" w:color=\"1F2A36\"/><w:bottom "
                    + "w:val=\"thinThickSmallGap\" w:sz=\"12\" w:space=\"27\" w:color=\"1F2A36\"/><w:right w:val=\"thinThickSmallGap\" "
                    + "w:sz=\"12\" w:space=\"27\" w:color=\"1F2A36\"/></w:pgBorders>";
                string sect =
                    "<w:sectPr><w:headerReference w:type=\"default\" r:id=\"rIdHeader\"/><w:footerReference w:type=\"default\" "
                    + "r:id=\"rIdFooter\"/><w:pgSz w:w=\"11906\" w:h=\"16838\"/><w:pgMar w:top=\"2041\" w:right=\"1134\" w:bottom=\"1531\" "
                    + "w:left=\"1134\" w:header=\"794\" w:footer=\"737\" w:gutter=\"0\"/>"
                    + frame
                    + "</w:sectPr>";
                WritePart(
                    package,
                    documentUri,
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><w:document xmlns:w=\""
                    + W
                    + "\" xmlns:r=\""
                    + R
                    + "\" xmlns:wp=\""
                    + WP
                    + "\" xmlns:a=\""
                    + A
                    + "\" xmlns:pic=\""
                    + PIC
                    + "\"><w:body>"
                    + body
                    + sect
                    + "</w:body></w:document>");
                var document = package.GetPart(documentUri);
                package.CreateRelationship(documentUri, TargetMode.Internal, R + "/officeDocument", "rIdDocument");
                WritePart(
                    package,
                    new Uri("/word/styles.xml", UriKind.Relative),
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml",
                    Styles());
                document.CreateRelationship(new Uri("styles.xml", UriKind.Relative), TargetMode.Internal, R + "/styles", "rIdStyles");
                WritePart(
                    package,
                    new Uri("/word/settings.xml", UriKind.Relative),
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><w:settings xmlns:w=\""
                    + W
                    + "\"><w:updateFields w:val=\"false\"/><w:defaultTabStop w:val=\"709\"/><w:compat><w:compatSetting "
                    + "w:name=\"compatibilityMode\" w:uri=\"http://schemas.microsoft.com/office/word\" "
                    + "w:val=\"15\"/></w:compat></w:settings>");
                document.CreateRelationship(new Uri("settings.xml", UriKind.Relative), TargetMode.Internal, R + "/settings", "rIdSettings");
                WritePart(
                    package,
                    new Uri("/word/header1.xml", UriKind.Relative),
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.header+xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><w:hdr xmlns:w=\""
                        + W
                        + "\" xmlns:r=\""
                        + R
                        + "\" xmlns:wp=\""
                        + WP
                        + "\" xmlns:a=\""
                        + A
                        + "\" xmlns:pic=\""
                        + PIC
                        + "\">"
                        + headerXml
                        + "</w:hdr>");
                document.CreateRelationship(new Uri("header1.xml", UriKind.Relative), TargetMode.Internal, R + "/header", "rIdHeader");
                var header = package.GetPart(new Uri("/word/header1.xml", UriKind.Relative));
                for (int i = 0; i < media.Count; i++)
                {
                    var image = media[i];
                    string name = "image" + (i + 1) + "." + image.Extension;
                    var part = package.CreatePart(
                        new Uri("/word/media/" + name, UriKind.Relative),
                        image.ContentType,
                        CompressionOption.NotCompressed);
                    using (var stream = part.GetStream(FileMode.Create, FileAccess.Write))
                        stream.Write(image.Data, 0, image.Data.Length);
                    string id;
                    if (documentImages.TryGetValue(image, out id))
                        document.CreateRelationship(new Uri("media/" + name, UriKind.Relative), TargetMode.Internal, R + "/image", id);
                    if (headerImages.TryGetValue(image, out id))
                        header.CreateRelationship(new Uri("media/" + name, UriKind.Relative), TargetMode.Internal, R + "/image", id);
                    if (image.SvgData != null)
                    {
                        string svg = "image" + (i + 1) + ".svg";
                        var svgPart =
                            package.CreatePart(new Uri("/word/media/" + svg, UriKind.Relative), "image/svg+xml", CompressionOption.Normal);
                        using (var stream = svgPart.GetStream(FileMode.Create, FileAccess.Write))
                            stream.Write(image.SvgData, 0, image.SvgData.Length);
                        if (documentImages.TryGetValue(image, out id))
                            document.CreateRelationship(
                                new Uri("media/" + svg, UriKind.Relative),
                                TargetMode.Internal,
                                R + "/image",
                                id + "s");
                        if (headerImages.TryGetValue(image, out id))
                            header.CreateRelationship(
                                new Uri("media/" + svg, UriKind.Relative),
                                TargetMode.Internal,
                                R + "/image",
                                id + "s");
                    }
                }
                WritePart(
                    package,
                    new Uri("/word/footer1.xml", UriKind.Relative),
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><w:ftr xmlns:w=\""
                        + W
                        + "\" xmlns:r=\""
                        + R
                        + "\">"
                        + FooterXml()
                        + "</w:ftr>");
                document.CreateRelationship(new Uri("footer1.xml", UriKind.Relative), TargetMode.Internal, R + "/footer", "rIdFooter");
                package.PackageProperties.Title = Title;
                if (!String.IsNullOrEmpty(Subject))
                    package.PackageProperties.Subject = Subject;
                package.PackageProperties.Creator = "ShaftMaster";
                package.PackageProperties.Created = ReportClock.Now().ToUniversalTime();
            }
        }
    }
}
