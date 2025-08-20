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
                { "Model",              "O8"   },
                { "MaLinhKien",         "F10"   },
                { "TenLinhKien",        "Q10"   },
                { "NhaCungCap",         "AE10"  },
                { "SoLuong",            "AC12" },
                { "GhiChuChinhSua",     "A18"  },

                // ===== CHECKBOX VÀ COMMENT CHO STEP 0 =====
                { "DaXacNhanFASample",         "B28"  },
                { "DaXacNhanLapRapTruoc",      "B35"  },
                { "DaKiemTra",                 "S28"  },
                { "CommentFASample",           "D30"  },
                { "CommentLapRapTruoc",        "D37"  },
                { "CommentDaKiemTra",          "W30"  },

                // ===== CHECKBOX VÀ COMMENT CHO STEP 2 - PCB =====
                { "DaNhanHangTestRun",         "S35"  }, // Đã nhận hàng test run cho PCB
                { "CommentStep2",              "V37"  }, // Comment của step 2

                // ===== THÔNG TIN VÀ CHECKBOX CHO STEP 4 - PCB =====
                // 7 ô thông tin
                { "ThongTin1Step4",            "O8"  },
                { "ThongTin2Step4",            "X8"  },
                { "ThongTin3Step4",            "AG8"  },
                { "ThongTin4Step4",            "V16"  },
                { "ThongTin5Step4",            "F20"  },
                { "ThongTin6Step4",            "Q20" },
                { "ThongTin7Step4",            "AA20" },
                
                // 3 checkbox Step 4
                { "LapRapStep4",               "B43"  }, 
                { "TinhNangStep4",             "B44"  }, 
                { "NgoaiQuanStep4",            "H43"  },
                
                
                // Comment tổng quát Step 4
                { "CommentStep4",              "D46"  },

                // Các checkbox EE khác
                {"EPE-EE3", "B52" },
                {"EPE-EE4", "H52" },

                // ===== STEP 6 - KẾT QUẢ TEST RUN =====
                { "KetQuaStep6",               "T43"  }, // Vị trí cho việc khoanh tròn OK/NG
                { "NGSo_R_Step6",              "X44"  }, // r=
                { "NGSo_N_Step6",              "AB44"  }, // n=
                { "NoiDungNG_Step6",           "V47"  }, // Nội dung NG
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
                { "NoiDungNG_LayMau_Step8",    "W57" }, // NG内容
                
                { "Comment_Step10",            "A62" },
        };

        public ExcelService()
        {
            SpreadsheetInfo.SetLicense("FREE-LIMITED-KEY");
        }

        /// <summary>
        /// Điền dữ liệu vào template dựa trên mapping, fallback NamedRange
        /// </summary>
        public void FillFields(string excelPath, Request request)
        {
            using (var pkg = new ExcelPackage(new FileInfo(excelPath)))
            {
                var ws = pkg.Workbook.Worksheets["D001"] ?? throw new InvalidOperationException("Sheet 'D001' không tồn tại.");

                // NULL CHECK cho request.Fields
                if (request?.Fields != null)
                {
                    // BƯỚC ĐẶC BIỆT: XỬ LÝ KetQuaStep6 TRƯỚC để đảm bảo xóa highlight cũ
                    var ketQuaField = request.Fields.FirstOrDefault(f => f.Key == "KetQuaStep6");
                    if (ketQuaField != null && cellMapping.TryGetValue("KetQuaStep6", out var ketQuaCellAddress))
                    {
                        if (!string.IsNullOrEmpty(ketQuaField.Value))
                        {
                            AddCircleToResult(ws, ketQuaCellAddress, ketQuaField.Value);
                        }
                        else
                        {
                            // Nếu không có giá trị (user chưa chọn), xóa tất cả highlight
                            var range = ws.Cells[ketQuaCellAddress];
                            int row = range.Start.Row;
                            int col = range.Start.Column;
                            string okCell = ketQuaCellAddress;
                            string ngCell = $"{GetColumnName(col + 2)}{row}";

                            // Clear only if the cells are not empty
                            if (!string.IsNullOrEmpty(ws.Cells[okCell].Text))
                                ClearCellHighlight(ws, okCell);
                            if (!string.IsNullOrEmpty(ws.Cells[ngCell].Text))
                                ClearCellHighlight(ws, ngCell);
                        }
                    }

                    // Vòng lặp cho các trường khác
                    foreach (var field in request.Fields)
                    {
                        if (cellMapping.TryGetValue(field.Key, out var cellAddress))
                        {
                            // Bỏ qua KetQuaStep6 vì đã xử lý ở trên
                            if (field.Key == "KetQuaStep6")
                                continue;

                            // Accept both "true" and "on" for checked checkboxes
                            if (field.Value == "true" || field.Value == "on")
                            {
                                ws.Cells[cellAddress].Value = "v";
                            }
                            else if (!string.IsNullOrEmpty(field.Value) && field.Value != "false")
                            {
                                ws.Cells[cellAddress].Value = field.Value;
                            }
                            else
                            {
                                ws.Cells[cellAddress].Value = null;
                            }
                        }
                    }
                }

                // NULL CHECK cho request.History
                if (request?.History != null)
                {
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
                { 10, new List<string> {"AE62"} },
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
                            int row = targetCell.Start.Row - 1;
                            int col = targetCell.Start.Column - 1;

                            picture.SetPosition(row, 0, col, 0);
                            picture.SetSize(70, 70);
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
        /// Thêm hình tròn quanh OK hoặc NG dựa trên kết quả, xóa highlight cũ
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