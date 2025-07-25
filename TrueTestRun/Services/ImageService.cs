using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Web.Hosting;
using TrueTestRun.Models;

namespace TrueTestRun.Services
{
    public class ImageService
    {
        private readonly string sealDir = HostingEnvironment.MapPath("~/App_Data/Seals");

        public ImageService()
        {
            if (!Directory.Exists(sealDir))
                Directory.CreateDirectory(sealDir);
        }

        // Lấy đường dẫn file seal theo phòng ban và tên
        private string GetSealPath(string department, string name)
        {
            var safeDept = department.Replace(" ", "_").Replace("/", "_");
            var safeName = name.Replace(" ", "_").Replace("/", "_");
            return Path.Combine(sealDir, $"{safeDept}_{safeName}.png");
        }

        // Hàm public: lấy seal từ file, nếu chưa có thì tạo mới
        public byte[] GetOrCreateSealImage(string department, string name, UserRole role)
        {
            if (role == UserRole.DataEntry)
                return null; // Không tạo seal cho DataEntry

            var path = GetSealPath(department, name);
            if (File.Exists(path))
                return File.ReadAllBytes(path);

            var bytes = GenerateSealImage(department, name);
            File.WriteAllBytes(path, bytes);
            return bytes;
        }

        // Hàm này giữ nguyên logic cũ (tạo seal động)
        public byte[] GenerateSealImage(string department, string name)
        {
            int size = 400;
            var sealColor = Color.Red;
            var fontFamily = "Arial Black";

            using (var bitmap = new Bitmap(size, size))
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.Transparent);

                var centerPoint = new PointF(size / 2f, size / 2f);
                float circlePenWidth = size / 33f;
                float linePenWidth = size / 66f;

                using (var pen = new Pen(sealColor, circlePenWidth))
                {
                    float rectOffset = circlePenWidth / 2;
                    g.DrawEllipse(pen, rectOffset, rectOffset, size - circlePenWidth, size - circlePenWidth);
                }

                using (var pen = new Pen(sealColor, linePenWidth))
                {
                    float lineStartX = circlePenWidth / 2;
                    float lineEndX = size - (circlePenWidth / 2);
                    g.DrawLine(pen, lineStartX, centerPoint.Y, lineEndX, centerPoint.Y);
                }

                using (var brush = new SolidBrush(sealColor))
                using (var stringFormat = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                })
                {
                    float textRegionHeight = size * 0.22f;
                    float textVerticalMargin = size * 0.02f;

                    var rectTop = new RectangleF(0, centerPoint.Y - textRegionHeight - textVerticalMargin, size, textRegionHeight);
                    using (var bestFitFont = FindBestFitFont(g, department.ToUpper(), fontFamily, rectTop))
                    {
                        g.DrawString(department.ToUpper(), bestFitFont, brush, rectTop, stringFormat);
                    }

                    string fullName = name.ToUpper();
                    var rectBottom = new RectangleF(0, centerPoint.Y + textVerticalMargin, size, textRegionHeight);
                    using (var bestFitFont = FindBestFitFont(g, fullName, fontFamily, rectBottom))
                    {
                        g.DrawString(fullName, bestFitFont, brush, rectBottom, stringFormat);
                    }
                }

                using (var ms = new MemoryStream())
                {
                    bitmap.Save(ms, ImageFormat.Png);
                    return ms.ToArray();
                }
            }
        }

        private Font FindBestFitFont(Graphics g, string text, string fontFamily, RectangleF layoutArea)
        {
            var safeLayoutArea = RectangleF.Inflate(layoutArea, -layoutArea.Width * 0.15f, -layoutArea.Height * 0.1f);
            float initialFontSize = safeLayoutArea.Height;
            var bestFitFont = new Font(fontFamily, initialFontSize, FontStyle.Bold, GraphicsUnit.Pixel);

            while (g.MeasureString(text, bestFitFont).Width > safeLayoutArea.Width && bestFitFont.Size > 2)
            {
                bestFitFont.Dispose();
                bestFitFont = new Font(fontFamily, bestFitFont.Size - 1, FontStyle.Bold, GraphicsUnit.Pixel);
            }
            return bestFitFont;
        }
    }
}