using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OfficeOpenXml;
using TrueTestRun.Services;
using GemBox.Spreadsheet;
using TrueTestRun.Models;
using System.Drawing;
using System.IO;

namespace TrueTestRun.Services
{
    public class ExcelService
    {
        private readonly Dictionary<string, string> cellMapping =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Thông tin chung
           
            { "NgayPhatHanh",       "E8"   },
            { "Model",              "O8"   },
            { "MaLinhKien",         "F10"   },
            { "TenLinhKien",        "Q10"   },
            { "NhaCungCap",         "AE10"  },
            { "SoLuong",            "AC12" },
            

            { "Comment_Step0", "E30" }, // Ví dụ: Ghi chú của bước 0 ở ô C36
            {"Comment_Step1", "E38"},
            {"Comment_Step2", "W30" },

            // Checkbox xác nhận của các bộ phận
            { "EPE-EE", "B28"  }, // Key phải khớp với name của checkbox trong form
            { "EPE-EE1", "B35"  },
            {"EPE-EE2", "S28" },
                {"EPE-EE3", "B52" },
                {"EPE-EE4", "H52" },
                {"EPE-PCB", "S35" },
                {"EPE-PCB1", "B43" },
                {"EPE-PCB2", "H43"},
                {"EPE-PCB3", "B44"},
        };

        public ExcelService()
        {
            
            SpreadsheetInfo.SetLicense("FREE-LIMITED-KEY");
        }

        /// <summary>
        /// Điền dữ liệu vào template dựa trên mapping, fallback NamedRange
        /// </summary>
        public void FillFields(string excelPath, Request request) // Nhận vào cả đối tượng Request
        {
            using (var pkg = new ExcelPackage(new FileInfo(excelPath)))
            {
                var ws = pkg.Workbook.Worksheets["D001"] ?? throw new InvalidOperationException("Sheet 'D001' không tồn tại.");

                // Vòng lặp 1: Điền các trường dữ liệu chung như cũ
                foreach (var field in request.Fields)
                {
                    if (cellMapping.TryGetValue(field.Key, out var cellAddress))
                    {
                        if (field.Value == "true") ws.Cells[cellAddress].Value = "v";
                        else if (field.Value != "false") ws.Cells[cellAddress].Value = field.Value;
                        else ws.Cells[cellAddress].Value = null;
                    }
                }

                // Vòng lặp 2 (MỚI): Điền comment từ lịch sử phê duyệt
                foreach (var step in request.History)
                {
                    // Tìm key cho comment của bước này, ví dụ: "Comment_Step0"
                    string commentKey = $"Comment_Step{step.Index}";
                    if (cellMapping.TryGetValue(commentKey, out var cellAddress) && !string.IsNullOrEmpty(step.Comment))
                    {
                        ws.Cells[cellAddress].Value = step.Comment;
                    }
                }

                pkg.Save();
            }
        }

        public byte[] RenderToPng(string excelPath)
        {
            var workbook = ExcelFile.Load(excelPath);
            workbook.Worksheets.ActiveWorksheet = workbook.Worksheets["D001"];
            var options = new ImageSaveOptions(ImageSaveFormat.Png) { SelectionType = SelectionType.ActiveSheet };
            using (var ms = new MemoryStream())
            {
                workbook.Save(ms, options);
                return ms.ToArray();
            }
        }

        public void AddSealImage(string excelPath, int stepIndex, byte[] sealImageData)
        {
            // Ánh xạ từ Step Index sang danh sách các ô Excel để chèn dấu
            var sealLocationMapping = new Dictionary<int, List<string>>
    {
        { 1, new List<string> { "N29", "N36", "AE29" } },
                {3, new List<string> {"AE36"} },
                {5, new List<string> {"N45"} },
                {7, new List<string> {"AE45"} },
                {9, new List<string> {"N54", "AE54"} },
                {10, new List<string> {"AE62"} },
        // ... Thêm các bước khác nếu cần
    };

            if (!sealLocationMapping.ContainsKey(stepIndex)) return;

            var targetCellAddresses = sealLocationMapping[stepIndex];
            using (var pkg = new ExcelPackage(new FileInfo(excelPath)))
            using (var ms = new MemoryStream(sealImageData))
            {
                Image sealAsImage = Image.FromStream(ms);
                var ws = pkg.Workbook.Worksheets["D001"];
                if (ws == null) return;

                foreach (var targetCellAddress in targetCellAddresses)
                {
                    string imageName = $"Seal_Step_{stepIndex}_{targetCellAddress}_{Guid.NewGuid()}";
                    var picture = ws.Drawings.AddPicture(imageName, sealAsImage);

                    var targetCell = ws.Cells[targetCellAddress];
                    int row = targetCell.Start.Row - 1;
                    int col = targetCell.Start.Column - 1;

                    picture.SetPosition(row, 0, col, 0);
                    picture.SetSize(70, 70);
                }

                pkg.Save();
            }
        }
    }
}