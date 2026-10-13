using System;
using System.IO;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Elevator.Solution.Bridge
{
    /// <summary>
    /// Puts the company logo image onto page 1 of the plotted PDF at the logo area of the sheet: the Core Console does not plot raster
    /// images. The aspect ratio is kept, the image is centred in the area. Without a logo nothing is added.
    /// </summary>
    public static class PdfLogoOverlay
    {
        const double PointsPerMm = 72.0 / 25.4;

        /// <summary>
        /// Draws the logo. The area is in paper millimetres from the lower left corner of the sheet; <paramref name="paperWidthMm"/> and
        /// <paramref name="paperHeightMm"/> are the paper size the page was plotted at (1189 x 841).
        /// </summary>
        public static void Apply(string pdfPath, string logoFile, double areaX, double areaY, double areaWidth, double areaHeight, int logoWidthPx, int logoHeightPx)
        {
            if (String.IsNullOrWhiteSpace(logoFile))
                return;
            if (!File.Exists(pdfPath))
                throw new FileNotFoundException("The PDF is missing.", pdfPath);
            if (!File.Exists(logoFile))
                throw new FileNotFoundException("The logo image file is missing.", logoFile);
            if (!(areaWidth > 0) || !(areaHeight > 0) || logoWidthPx <= 0 || logoHeightPx <= 0)
                throw new ArgumentException("The logo area or the logo size is missing.");
            double k = Math.Min(areaWidth / logoWidthPx, areaHeight / logoHeightPx);
            // the image in the displayed page (points from its lower left corner)
            double w = logoWidthPx * k * PointsPerMm, h = logoHeightPx * k * PointsPerMm;
            double x = (areaX + (areaWidth - logoWidthPx * k) / 2) * PointsPerMm;
            double y = (areaY + (areaHeight - logoHeightPx * k) / 2) * PointsPerMm;
            var document = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Modify);
            try
            {
                if (document.PageCount < 1)
                    throw new InvalidDataException("The PDF has no page.");
                var page = document.Pages[0];
                // AutoCAD writes the landscape sheet as a portrait media box with /Rotate 270. PdfSharp draws in the unrotated page
                // (x right, y up from the media box corner) but flips y with the displayed page height: the image is placed in the
                // unrotated page and turned so that it is upright when the page is displayed.
                int rotate = ((page.Rotate % 360) + 360) % 360;
                double mediaW = page.MediaBox.Width, mediaH = page.MediaBox.Height;
                using (var graphics = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append))
                using (var image = XImage.FromFile(logoFile))
                {
                    double flipHeight = graphics.PageSize.Height;
                    double xc = x + w / 2, yc = y + h / 2; // centre in the displayed page, points from its lower left corner
                    double ux, uy, turn; // centre in the unrotated page (y up) and the turn
                    switch (rotate)
                    {
                        case 0:
                            ux = xc;
                            uy = yc;
                            turn = 0;
                            break;
                        case 270:
                            ux = yc;
                            uy = mediaH - xc;
                            turn = 90;
                            break;
                        case 90:
                            ux = mediaW - yc;
                            uy = xc;
                            turn = -90;
                            break;
                        default:
                            throw new InvalidDataException("The plotted PDF page is turned by " + rotate + " degrees; the logo position is not defined for it.");
                    }
                    double gx = ux, gy = flipHeight - uy;
                    if (turn != 0)
                        graphics.RotateAtTransform(turn, new XPoint(gx, gy));
                    graphics.DrawImage(image, gx - w / 2, gy - h / 2, w, h);
                }
                string tmp = pdfPath + ".logo.tmp";
                document.Save(tmp);
                document.Close();
                File.Delete(pdfPath);
                File.Move(tmp, pdfPath);
            }
            finally
            {
                document.Dispose();
            }
        }
    }
}
