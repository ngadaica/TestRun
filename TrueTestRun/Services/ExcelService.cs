using System;
using System.Collections.Generic;
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
                { "MaLinhKien",         "F10"   },
                { "TenLinhKien",        "Q10"   },
                { "NhaCungCap",         "AE10"  },
                { "SoLuong",            "AC12" },
                { "GhiChuChinhSua",     "A18"  },

                // ===== CHECKBOX VÀ COMMENT CHO STEP 0 =====
                { "DaXacNhanFASample",         "B28"  },
                { "DaXacNhanLapRapTruoc",      "B35"  },
                { "DaKiemTra",                 "S28"  },
                { "CommentFASample",           "E30"  },
                { "CommentLapRapTruoc",        "E37"  },
                { "CommentDaKiemTra",          "U30"  },

                // ===== CHECKBOX VÀ COMMENT CHO STEP 2 - PCB =====
                { "DaNhanHangTestRun",         "S35"  }, // Đã nhận hàng test run cho PCB
                { "CommentStep2",              "U37"  }, // Comment của step 2

                // ===== THÔNG TIN VÀ CHECKBOX CHO STEP 4 - PCB =====
                // 7 ô thông tin
                { "ThongTin1Step4",            "O8"  },
                { "ThongTin2Step4",            "X8"  },
                { "ThongTin3Step4",            "AG8"  },
                { "ThongTin4Step4",            "V16"  },
                { "ThongTin5Step4",            "F20"  },
                { "ThongTin6Step4",            "Q20" },
                { "ThongTin7Step4",            "AA20" },
                
                // 3 checkbox Step 5
                { "LapRapStep5",               "B43"  },
                { "TinhNangStep5",             "B44"  },
                { "NgoaiQuanStep5",            "H43"  },
                
                
                // Comment tổng quát Step 5
                { "CommentStep5",              "D46"  },

                // Các checkbox EE khác
                {"EPE-EE3", "B52" },
                {"EPE-EE4", "H52" },

                // ===== STEP 6 - KẾT QUẢ TEST RUN =====
                { "KetQuaStep6",               "T43"  }, // Vị trí cho việc khoanh tròn OK/NG
                { "NGSo_R_Step6",              "X44"  }, // r=
                { "NGSo_N_Step6",              "AB44"  }, // n=
                { "NoiDungNG_Step6",           "U46"  }, // Nội dung NG
                { "CommentStep6",              "B66"  }, // Comment Step 6

                // ===== STEP 8 - EE STAFF ĐIỀN FORM =====
                // 2 Checkbox chính
                { "TinhNangStep8",             "B52"  }, // Tính năng 機能検査
                { "NgoaiQuanStep8",            "H52"  }, // Ngoại quan 外観検査
                
                // 2 Comment KTTB và KTLM
                { "CommentKTTB_Step8",         "E54"  }, // KTTB comment
                { "CommentKTLM_Step8",         "E56"  }, // KTLM comment
                
                // Bảng 1: Kiểm tra toàn bộ 全検(QA Line)
                { "NGSo_R_ToanBo_Step8",       "V54"  }, // NG数：r =
                { "OKSo_N_ToanBo_Step8",       "V55"  }, // OK数：n =
                { "NoiDungNG_ToanBo_Step8",    "S57"  }, // NG内容
                
                // Bảng 2: Kiểm tra lấy mẫu 抜取(QA Line)
                { "NGSo_R_LayMau_Step8",       "AA54" }, // NG数：r =
                { "OKSo_N_LayMau_Step8",       "AA55" }, // OK数：n =
                { "NoiDungNG_LayMau_Step8",    "X57" }, // NG内容
                
                { "Comment_Step10",            "A61" },
        };

        public ExcelService()
        {
            SpreadsheetInfo.SetLicense("FREE-LIMITED-KEY");
        }
        private static bool IsTrueLike(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return false;
            var parts = val.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var t = part.Trim();
                if (t.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                    t.Equals("on", StringComparison.OrdinalIgnoreCase) ||
                    t.Equals("1") ||
                    t.Equals("yes", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Điền dữ liệu vào template dựa trên mapping, fallback NamedRange
        /// </summary>
        public void FillFields(string excelPath, Request request)
        {
            using (var pkg = new OfficeOpenXml.ExcelPackage(new FileInfo(excelPath)))
            {
                var ws = pkg.Workbook.Worksheets["D001"] ?? throw new InvalidOperationException("Sheet 'D001' không tồn tại.");

                if (request?.Fields != null)
                {
                    // Build a fast lookup for existing fields
                    var fieldDict = request.Fields
                        .GroupBy(f => f.Key, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(g => g.Key, g => g.Last().Value ?? "", StringComparer.OrdinalIgnoreCase);

                    // Step 5 fallback: if Step 5 keys are absent but Step 4 values exist, use Step 4 for writing.
                    var step5Keys = new[] { "LapRapStep5", "TinhNangStep5", "NgoaiQuanStep5", "CommentStep5" };
                    bool hasAnyStep5 = step5Keys.Any(k => fieldDict.ContainsKey(k));

                    var effectiveFields = new List<RequestField>(request.Fields);
                    if (!hasAnyStep5)
                    {
                        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "LapRapStep5",   "LapRapStep4"   },
                    { "TinhNangStep5", "TinhNangStep4" },
                    { "NgoaiQuanStep5","NgoaiQuanStep4"},
                    { "CommentStep5",  "CommentStep4"  },
                };

                        foreach (var kv in map)
                        {
                            if (!fieldDict.ContainsKey(kv.Key) && fieldDict.ContainsKey(kv.Value))
                            {
                                // Only use fallback for writing; do not persist into request.Fields here.
                                effectiveFields.Add(new RequestField { Key = kv.Key, Value = fieldDict[kv.Value] });
                            }
                        }
                    }

                    // Render Step 6 OK/NG highlight first
                    var ketQuaField = effectiveFields.FirstOrDefault(f => f.Key.Equals("KetQuaStep6", StringComparison.OrdinalIgnoreCase));
                    if (ketQuaField != null && cellMapping.TryGetValue("KetQuaStep6", out var ketQuaCellAddress))
                    {
                        if (!string.IsNullOrEmpty(ketQuaField.Value))
                        {
                            AddCircleToResult(ws, ketQuaCellAddress, ketQuaField.Value);
                        }
                        else
                        {
                            var range = ws.Cells[ketQuaCellAddress];
                            int row = range.Start.Row;
                            int col = range.Start.Column;
                            string okCell = ketQuaCellAddress;
                            string ngCell = $"{GetColumnName(col + 2)}{row}";
                            if (!string.IsNullOrEmpty(ws.Cells[okCell].Text)) ClearCellHighlight(ws, okCell);
                            if (!string.IsNullOrEmpty(ws.Cells[ngCell].Text)) ClearCellHighlight(ws, ngCell);
                        }
                    }

                    // Write all mapped cells
                    foreach (var field in effectiveFields)
                    {
                        if (!cellMapping.TryGetValue(field.Key, out var cellAddress)) continue;
                        if (field.Key.Equals("KetQuaStep6", StringComparison.OrdinalIgnoreCase)) continue;

                        if (IsTrueLike(field.Value))
                        {
                            ws.Cells[cellAddress].Value = "v";
                        }
                        else if (!string.IsNullOrEmpty(field.Value) && !field.Value.Equals("false", StringComparison.OrdinalIgnoreCase))
                        {
                            ws.Cells[cellAddress].Value = field.Value;
                        }
                        else
                        {
                            ws.Cells[cellAddress].Value = null;
                        }
                    }
                }

                // History comments (kept)
                if (request?.History != null)
                {
                    foreach (var step in request.History)
                    {
                        string commentKey = $"Comment_Step{step.Index}";
                        if (cellMapping.TryGetValue(commentKey, out var cellAddress) && !string.IsNullOrEmpty(step.Comment))
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
        { 3, new List<string> {"AE36"} },
        { 5, new List<string> {"N45"} },
        { 7, new List<string> {"AE45"} },
        { 9, new List<string> {"N54", "AE54"} },
        { 10, new List<string> {"AE61"} },
    };

            if (!sealLocationMapping.ContainsKey(stepIndex))
            {
                return;
            }

            var targetCellAddresses = sealLocationMapping[stepIndex];

            if (sealImageData == null || sealImageData.Length == 0)
            {
                return;
            }

            if (!System.IO.File.Exists(excelPath))
            {
                return;
            }

            try
            {
                using (var pkg = new ExcelPackage(new FileInfo(excelPath)))
                using (var ms = new MemoryStream(sealImageData))
                {
                    Image sealAsImage = Image.FromStream(ms);
                    var ws = pkg.Workbook.Worksheets["D001"];
                    if (ws == null)
                    {
                        return;
                    }

                    foreach (var targetCellAddress in targetCellAddresses)
                    {
                        try
                        {
                            string imageName = $"Seal_Step_{stepIndex}_{targetCellAddress}_{Guid.NewGuid()}";
                            var picture = ws.Drawings.AddPicture(imageName, sealAsImage);

                            var targetCell = ws.Cells[targetCellAddress];
                            int rowIndex = targetCell.Start.Row;
                            int colIndex = targetCell.Start.Column;

                            // Detect merged range containing the target cell (if any)
                            string merged = ws.MergedCells.FirstOrDefault(m =>
                            {
                                var addr = new ExcelAddress(m);
                                return addr.Start.Row <= rowIndex && addr.End.Row >= rowIndex
                                    && addr.Start.Column <= colIndex && addr.End.Column >= colIndex;
                            });

                            int cellWidthPx;
                            int cellHeightPx;
                            int anchorRow = rowIndex;
                            int anchorCol = colIndex;

                            if (!string.IsNullOrEmpty(merged))
                            {
                                var ma = new ExcelAddress(merged);
                                // use top-left of merged as anchor
                                anchorRow = ma.Start.Row;
                                anchorCol = ma.Start.Column;

                                // sum widths of merged columns
                                cellWidthPx = 0;
                                for (int c = ma.Start.Column; c <= ma.End.Column; c++)
                                    cellWidthPx += ColumnWidthToPixels(ws.Column(c).Width);

                                // sum heights of merged rows
                                cellHeightPx = 0;
                                for (int r = ma.Start.Row; r <= ma.End.Row; r++)
                                    cellHeightPx += RowHeightPointsToPixels(ws.Row(r).Height);
                            }
                            else
                            {
                                cellWidthPx = ColumnWidthToPixels(ws.Column(colIndex).Width);
                                cellHeightPx = RowHeightPointsToPixels(ws.Row(rowIndex).Height);
                            }

                            // Fallback defaults if cell dims not available
                            if (cellWidthPx <= 0) cellWidthPx = 80;
                            if (cellHeightPx <= 0) cellHeightPx = 80;

                            // Provide a small padding so seal doesn't touch borders
                            const int padding = 8;
                            int availableW = Math.Max(1, cellWidthPx - padding);
                            int availableH = Math.Max(1, cellHeightPx - padding);

                            // Determine scale to fit image inside the available area preserving aspect ratio
                            int imgW = sealAsImage.Width;
                            int imgH = sealAsImage.Height;
                            if (imgW <= 0) imgW = availableW;
                            if (imgH <= 0) imgH = availableH;

                            double scale = Math.Min((double)availableW / imgW, (double)availableH / imgH);

                            // Limit upscale and then apply a small visual shrink to avoid touching borders
                            if (scale > 2.0) scale = 2.0;
                            const double visualShrink = 0.92; // make image slightly smaller for margin
                            scale = scale * visualShrink;

                            int newWidth = Math.Max(1, (int)Math.Round(imgW * scale));
                            int newHeight = Math.Max(1, (int)Math.Round(imgH * scale));

                            // Ensure the image fits inside the cell (defensive)
                            if (newWidth > cellWidthPx) newWidth = cellWidthPx - 2;
                            if (newHeight > cellHeightPx) newHeight = cellHeightPx - 2;

                            // Apply size and center it
                            picture.SetSize(newWidth, newHeight);

                            // Calculate centered offsets
                            int offsetX = (cellWidthPx - newWidth) / 2;
                            int offsetY = (cellHeightPx - newHeight) / 2;

                            // Shift left a bit more (12% of cell width) per your request
                            int leftShift = (int)Math.Round(cellWidthPx * 0.12);
                            offsetX = offsetX - leftShift;

                            // Defensive clamps
                            if (offsetX < 0) offsetX = 0;
                            if (offsetY < 0) offsetY = 0;
                            if (offsetX > cellWidthPx - newWidth) offsetX = Math.Max(0, cellWidthPx - newWidth);
                            if (offsetY > cellHeightPx - newHeight) offsetY = Math.Max(0, cellHeightPx - newHeight);

                            // EPPlus SetPosition expects zero-based row/col indexes
                            picture.SetPosition(anchorRow - 1, offsetY, anchorCol - 1, offsetX);
                        }
                        catch (Exception)
                        {
                            // Ignore individual cell errors
                        }
                    }

                    pkg.Save();
                }
            }
            catch (Exception)
            {
                // Ignore errors
            }
        }

        /// <summary>
        /// Convert Excel column width (characters) to pixels (approximate).
        /// Uses common approximation used by many Excel libraries.
        /// </summary>
        private int ColumnWidthToPixels(double excelColumnWidth)
        {
            try
            {
                if (excelColumnWidth <= 0) return 0;
                double integerPortion = Math.Floor(excelColumnWidth);
                double fractionalPortion = excelColumnWidth - integerPortion;
                double pixels = integerPortion * 7 + Math.Round(fractionalPortion * 7);
                // Add padding often present in Excel cells
                pixels += 5;
                return (int)Math.Max(1, Math.Round(pixels));
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Convert row height in points to pixels.
        /// 1 point = 96/72 pixels.
        /// </summary>
        private int RowHeightPointsToPixels(double rowHeightPoints)
        {
            try
            {
                if (rowHeightPoints <= 0) return 0;
                double pixels = rowHeightPoints * 96.0 / 72.0;
                return (int)Math.Max(1, Math.Round(pixels));
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Đọc giá trị từ Excel dựa trên field key
        /// </summary>
        public string ReadFieldFromExcel(string excelPath, string fieldKey)
        {
            try
            {
                if (!System.IO.File.Exists(excelPath))
                    return null;

                if (!cellMapping.TryGetValue(fieldKey, out var cellAddress))
                    return null;

                using (var pkg = new ExcelPackage(new FileInfo(excelPath)))
                {
                    var ws = pkg.Workbook.Worksheets["D001"];
                    if (ws == null)
                        return null;

                    var cell = ws.Cells[cellAddress];
                    return cell.Text?.Trim();
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Đọc nhiều field cùng lúc từ Excel
        /// </summary>
        public Dictionary<string, string> ReadFieldsFromExcel(string excelPath, string[] fieldKeys)
        {
            var result = new Dictionary<string, string>();

            try
            {
                if (!System.IO.File.Exists(excelPath))
                    return result;

                using (var pkg = new ExcelPackage(new FileInfo(excelPath)))
                {
                    var ws = pkg.Workbook.Worksheets["D001"];
                    if (ws == null)
                        return result;

                    foreach (var fieldKey in fieldKeys)
                    {
                        if (cellMapping.TryGetValue(fieldKey, out var cellAddress))
                        {
                            var cell = ws.Cells[cellAddress];
                            var value = cell.Text?.Trim();
                            result[fieldKey] = string.IsNullOrEmpty(value) ? "N/A" : value;
                        }
                        else
                        {
                            result[fieldKey] = "N/A";
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Fill with N/A in case of error
                foreach (var fieldKey in fieldKeys)
                {
                    result[fieldKey] = "N/A";
                }
            }

            return result;
        }

        /// <summary>
        /// Thêm hình tròn quanh OK hoặc NG dựa trên kết quả
        /// </summary>
        private void AddCircleToResult(OfficeOpenXml.ExcelWorksheet ws, string baseCell, string result)
        {
            try
            {
                // Xác định 2 ô: OK và NG
                string okCell = baseCell; // Ô OK
                var range = ws.Cells[baseCell];
                int row = range.Start.Row;
                int col = range.Start.Column + 3; // Chuyển 3 cột sang phải cho NG
                string ngCell = $"{GetColumnName(col)}{row}"; // Ô NG

                // BƯỚC 1: XÓA TẤT CẢ HIGHLIGHT CŨ (cho cả OK và NG)
                ClearCellHighlight(ws, okCell);
                ClearCellHighlight(ws, ngCell);

                // BƯỚC 2: THÊM HIGHLIGHT MỚI CHO Ô ĐƯỢC CHỌN
                string targetCell = result == "NG" ? ngCell : okCell;
                AddCellHighlight(ws, targetCell, result);
            }
            catch (Exception)
            {
                // Ignore highlight errors
            }
        }

        /// <summary>
        /// Xóa tất cả highlight khỏi một ô
        /// </summary>
        private void ClearCellHighlight(OfficeOpenXml.ExcelWorksheet ws, string cellAddress)
        {
            try
            {
                var cell = ws.Cells[cellAddress];

                // Xóa background color
                cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.None;

                // Xóa borders
                cell.Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.None;
                cell.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.None;
                cell.Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.None;
                cell.Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.None;

                // Reset font về mặc định
                cell.Style.Font.Bold = false;
                cell.Style.Font.Size = 11; // Default size
            }
            catch (Exception)
            {
                // Ignore clear errors
            }
        }

        /// <summary>
        /// Thêm highlight cho một ô cụ thể
        /// </summary>
        private void AddCellHighlight(OfficeOpenXml.ExcelWorksheet ws, string cellAddress, string result)
        {
            try
            {
                var cell = ws.Cells[cellAddress];

                // Thêm background color
                if (result == "OK")
                {
                    cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                }
                else if (result == "NG")
                {
                    cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightCoral);
                }

                // Thêm border dày
                cell.Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thick;
                cell.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thick;
                cell.Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thick;
                cell.Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thick;

                // Màu border
                var borderColor = result == "OK" ? System.Drawing.Color.Green : System.Drawing.Color.Red;
                cell.Style.Border.Top.Color.SetColor(borderColor);
                cell.Style.Border.Bottom.Color.SetColor(borderColor);
                cell.Style.Border.Left.Color.SetColor(borderColor);
                cell.Style.Border.Right.Color.SetColor(borderColor);

                // Làm đậm chữ
                cell.Style.Font.Bold = true;
                cell.Style.Font.Size = 12;
            }
            catch (Exception)
            {
                // Ignore highlight errors
            }
        }

        /// <summary>
        /// Chuyển đổi số cột thành tên cột Excel (1->A, 2->B, ...)
        /// </summary>
        private string GetColumnName(int columnNumber)
        {
            string columnName = "";
            while (columnNumber > 0)
            {
                int modulo = (columnNumber - 1) % 26;
                columnName = Convert.ToChar('A' + modulo) + columnName;
                columnNumber = (columnNumber - modulo) / 26;
            }
            return columnName;
        }
    }
}