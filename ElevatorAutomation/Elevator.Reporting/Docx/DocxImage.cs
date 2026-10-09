using System;
using System.IO;

namespace Elevator.Reporting.Docx
{
    /// <summary>
    /// PNG or JPEG image for embedding. Pixel size is read from the file header so callers can keep the aspect ratio without WPF. Reusable
    /// for logos now and drawings later.
    /// </summary>
    public sealed class DocxImage
    {
        public byte[] Data { get; private set; }

        // Optional vector version (SVG); Data is then its PNG fallback.
        public byte[] SvgData { get; private set; }

        /// <summary>A copy of a PNG image that carries an SVG version.</summary>
        public static DocxImage WithSvg(DocxImage png, byte[] svg)
        {
            if (png == null || png.Extension != "png" || svg == null || svg.Length == 0)
                throw new ArgumentException("SVG images need a PNG fallback.");
            return new DocxImage
            {
                Data = png.Data,
                Extension = png.Extension,
                ContentType = png.ContentType,
                PixelWidth = png.PixelWidth,
                PixelHeight = png.PixelHeight,
                SvgData = svg
            };
        }

        public string Extension { get; private set; }
        public string ContentType { get; private set; }
        public int PixelWidth { get; private set; }
        public int PixelHeight { get; private set; }

        /// <summary>Reads an image from a file.</summary>
        public static DocxImage FromFile(string path)
        {
            return FromBytes(File.ReadAllBytes(path));
        }

        /// <summary>Reads an image from its bytes.</summary>
        public static DocxImage FromBytes(byte[] d)
        {
            if (d == null)
                throw new ArgumentException("No image data.");
            if (d.Length >= 24
                && d[0] == 0x89
                && d[1] == 0x50
                && d[2] == 0x4E
                && d[3] == 0x47
                && d[4] == 0x0D
                && d[5] == 0x0A
                && d[6] == 0x1A
                && d[7] == 0x0A
                && d[12] == 'I'
                && d[13] == 'H'
                && d[14] == 'D'
                && d[15] == 'R')
            {
                return Checked(new DocxImage
                {
                    Data = d,
                    Extension = "png",
                    ContentType = "image/png",
                    PixelWidth = BigEndian(d, 16),
                    PixelHeight = BigEndian(d, 20)
                });
            }
            if (d.Length >= 4 && d[0] == 0xFF && d[1] == 0xD8)
            {
                int i = 2;
                while (i + 9 < d.Length)
                {
                    if (d[i] != 0xFF)
                    {
                        i++;
                        continue;
                    }
                    byte marker = d[i + 1];
                    if (marker == 0xFF)
                    {
                        i++;
                        continue;
                    }
                    if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
                    {
                        i += 2;
                        continue;
                    }
                    int length = (d[i + 2] << 8) | d[i + 3];
                    if (marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
                    {
                        return Checked(new DocxImage
                        {
                            Data = d,
                            Extension = "jpg",
                            ContentType = "image/jpeg",
                            PixelHeight = (d[i + 5] << 8) | d[i + 6],
                            PixelWidth = (d[i + 7] << 8) | d[i + 8]
                        });
                    }
                    if (length < 2)
                        break;
                    i += 2 + length;
                }
            }
            throw new ArgumentException("Only PNG or JPEG images are supported.");
        }

        static int BigEndian(byte[] d, int o)
        {
            return (d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3];
        }

        static DocxImage Checked(DocxImage image)
        {
            if (image.PixelWidth <= 0 || image.PixelHeight <= 0)
                throw new ArgumentException("The image has no valid pixel size.");
            return image;
        }

        /// <summary>Largest size within the box that keeps the aspect ratio.</summary>
        public void Fit(double maxWidthMm, double maxHeightMm, out double widthMm, out double heightMm)
        {
            double scale = Math.Min(maxWidthMm / PixelWidth, maxHeightMm / PixelHeight);
            widthMm = PixelWidth * scale;
            heightMm = PixelHeight * scale;
        }
    }
}
