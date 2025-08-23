using System;
using System.Configuration;
using System.Net.Mail;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using TrueTestRun.Models;
using System.IO;
using System.Web.Hosting;

namespace TrueTestRun.Services
{
    public class EmailService
    {
        private readonly TrueTestRunDbContext _context;

        public EmailService()
        {
            _context = new TrueTestRunDbContext();
        }

        /// <summary>
        /// Helper method để lấy resource string theo ngôn ngữ hiện tại
        /// </summary>
        private string GetResourceString(string key)
        {
            try
            {
                return HttpContext.GetGlobalResourceObject("Resources", key)?.ToString() ?? key;
            }
            catch
            {
                return key;
            }
        }

        /// <summary>
        /// Helper method để lấy resource string cho cả hai ngôn ngữ (Việt/Nhật)
        /// </summary>
        private string GetBilingualResourceString(string key)
        {
            try
            {
                // Lấy text tiếng Việt
                var vietnameseText = System.Web.HttpContext.GetGlobalResourceObject("Resources", key, new System.Globalization.CultureInfo("vi-VN"))?.ToString() ?? key;

                // Lấy text tiếng Nhật
                var japaneseText = System.Web.HttpContext.GetGlobalResourceObject("Resources", key, new System.Globalization.CultureInfo("ja-JP"))?.ToString() ?? key;

                // Kết hợp với dấu /
                return $"{vietnameseText}/{japaneseText}";
            }
            catch
            {
                return key;
            }
        }


        /// <summary>
        /// Helper method để lấy thông tin Part từ Excel file - CHỈNH SỬA: Đọc từ Excel thay vì RequestFields
        /// </summary>
        private (string PartNumber, string PartName) GetPartInfo(Request request)
        {
            try
            {
                // Đường dẫn đến file Excel của request
                var requestFolderPath = HostingEnvironment.MapPath($"~/App_Data/Requests/{request.RequestID}");
                var excelFilePath = Path.Combine(requestFolderPath, "request.xlsx");

                if (!File.Exists(excelFilePath))
                {
                    return ("未定義/Chưa định", "");
                }

                // Sử dụng ExcelService để đọc dữ liệu
                var excelService = new ExcelService();
                var excelData = excelService.ReadFieldsFromExcel(excelFilePath, new string[] { "MaLinhKien", "TenLinhKien" });

                // SỬA: Sử dụng TryGetValue thay vì GetValueOrDefault cho .NET Framework 4.7.2
                var partNumber = "";
                var partName = "";
                
                if (!excelData.TryGetValue("MaLinhKien", out partNumber))
                {
                    partNumber = "";
                }
                
                if (!excelData.TryGetValue("TenLinhKien", out partName))
                {
                    partName = "";
                }

                // Xử lý trường hợp không có dữ liệu
                if (string.IsNullOrEmpty(partNumber) && string.IsNullOrEmpty(partName))
                {
                    return ("未定義/Chưa định", "");
                }

                if (string.IsNullOrEmpty(partNumber))
                {
                    partNumber = "未定義/Chưa định";
                }

                if (string.IsNullOrEmpty(partName))
                {
                    partName = "";
                }

                return (partNumber, partName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EmailService] Error reading Excel part info: {ex.Message}");
                return ("未定義/Chưa định", "");
            }
        }

        /// <summary>
        /// Lấy email của người được chỉ định cho từng step - HARDCODE
        /// </summary>
        private string GetDesignatedApproverEmail(string deptCode, string role, int stepIndex)
        {
            // HARDCODE EMAIL CHO TỪNG STEP CỤ THỂ
            switch (stepIndex)
            {
                case 1: return "phamduc.anh@brother-bivn.com.vn";
                //case 1: return "Doan.PhamCong@brother-bivn.com.vn";
                //case 2: return "Ly.NguyenThi@brother-bivn.com.vn";
                //case 3: return "ChuVan.Long@brother-bivn.com.vn";
                //case 4: return "Ly.NguyenThi@brother-bivn.com.vn";
                //case 5: return "jun.sato@brother-bivn.com.vn";
                //case 6: return "Ly.NguyenThi@brother-bivn.com.vn";
                //case 7: return "jun.sato@brother-bivn.com.vn";
                //case 8: return "nguyenthi.duyen5@brother-bivn.com.vn";
                //case 9: return "Doan.PhamCong@brother-bivn.com.vn";
                //case 10: return "naoya.yada@brother-bivn.com.vn";
                case 2: return "phamduc.anh@brother-bivn.com.vn";
                case 3: return "phamduc.anh@brother-bivn.com.vn";
                case 4: return "phamduc.anh@brother-bivn.com.vn";
                case 5: return "phamduc.anh@brother-bivn.com.vn";
                case 6: return "phamduc.anh@brother-bivn.com.vn";
                case 7: return "phamduc.anh@brother-bivn.com.vn";
                case 8: return "phamduc.anh@brother-bivn.com.vn";
                case 9: return "phamduc.anh@brother-bivn.com.vn";
                case 10: return "phamduc.anh@brother-bivn.com.vn";
                default: return "phamduc.anh@brother-bivn.com.vn";

            }
        }

        /// <summary>
        /// Lấy email của user cụ thể theo ADID
        /// </summary>
        private string GetUserEmail(string adid)
        {
            try
            {
                var user = _context.Users.FirstOrDefault(u => u.ADID == adid);
                return user?.Email ?? "admin@brother-bivn.com.vn";
            }
            catch (Exception)
            {
                return "admin@brother-bivn.com.vn";
            }
        }

        /// <summary>
        /// Gửi email phê duyệt/ghi nhập - CHỈNH SỬA: Loại bỏ ステップ/Bước và lấy thông tin từ Excel
        /// </summary>
        public void SendApprovalRequest(Request request, WorkflowStep step, string approvalUrl, bool isResubmission = false)
        {
            try
            {
                string toEmail = "";

                // Kiểm tra nếu có người được chỉ định cụ thể trong NextApproverADID
                if (!string.IsNullOrEmpty(step.NextApproverADID))
                {
                    toEmail = GetUserEmail(step.NextApproverADID);
                }
                else
                {
                    toEmail = GetDesignatedApproverEmail(step.DeptCode, step.Role, step.Index);
                }

                // Validate email
                if (string.IsNullOrEmpty(toEmail) || !IsValidEmail(toEmail))
                {
                    System.Diagnostics.Debug.WriteLine($"[EmailService] Invalid email for step {step.StepName}: {toEmail}");
                    return;
                }

                var host = ConfigurationManager.AppSettings["SmtpHost"];
                var port = int.Parse(ConfigurationManager.AppSettings["SmtpPort"]);
                var fromAddress = new MailAddress("testrun.system@brother-bivn.com.vn", "Test Run System");
                var toAddress = new MailAddress(toEmail);

                // Lấy thông tin Part từ Excel
                var (partNumber, partName) = GetPartInfo(request);
                var partInfo = !string.IsNullOrEmpty(partName) ? $"{partNumber} - {partName}" : partNumber;

                // SỬA: Subject đơn giản hóa với thông tin Part từ Excel
                string actionType = step.Actor == StepActor.Approver ? "承認依頼/Yêu cầu phê duyệt" : "データ入力依頼/Yêu cầu ghi nhập";
                string subject = $"{actionType} [{request.RequestID}] {partInfo}";

                // SỬA: Body đơn giản hóa theo phong cách Nhật - LOẠI BỎ ステップ/Bước
                string body = $@"
                    <div style='font-family: ""Yu Gothic"", ""Hiragino Kaku Gothic ProN"", ""Meiryo"", sans-serif; max-width: 500px; margin: 0 auto; border: 1px solid #ddd;'>
                        <div style='background: #f0f0f0; padding: 10px 15px; border-bottom: 1px solid #ddd;'>
                            <div style='font-weight: bold; font-size: 14px;'>{actionType}</div>
                            <div style='font-size: 12px; color: #666; margin-top: 3px;'>テストラン承認システム / Test Run Approval System</div>
                        </div>
                        
                        <div style='padding: 15px;'>
                            <table style='width: 100%; font-size: 13px; border-collapse: collapse;'>
                                <tr>
                                    <td style='padding: 5px 0; font-weight: bold; width: 100px;'>申請番号/Mã đơn:</td>
                                    <td style='padding: 5px 0; color: #0066cc; font-weight: bold;'>{request.RequestID}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 5px 0; font-weight: bold;'>部品情報/Linh kiện:</td>
                                    <td style='padding: 5px 0;'>{partInfo}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 5px 0; font-weight: bold;'>申請者/Người tạo:</td>
                                    <td style='padding: 5px 0;'>{request.CreatedByADID}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 5px 0; font-weight: bold;'>状態/Trạng thái:</td>
                                    <td style='padding: 5px 0; color: #ff6600;'>処理待ち/Chờ xử lý</td>
                                </tr>
                            </table>
                            
                            <div style='text-align: center; margin: 20px 0;'>
                                <a href='{approvalUrl}' 
                                   style='display: inline-block; padding: 8px 16px; background: #0066cc; color: white; 
                                          text-decoration: none; border-radius: 3px; font-size: 13px; font-weight: bold;'>
                                    システムを開く / Mở hệ thống ≫
                                </a>
                            </div>
                        
                            <div style='font-size: 11px; color: #999; text-align: center; border-top: 1px solid #eee; padding-top: 10px; margin-top: 15px;'>
                                Brother Industries Vietnam - Test Run System
                            </div>
                        </div>
                    </div>";

                var smtp = new SmtpClient
                {
                    Host = host,
                    Port = port,
                    EnableSsl = false,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = true
                };

                using (var message = new MailMessage(fromAddress, toAddress)
                {
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                })
                {
                    smtp.Send(message);
                    System.Diagnostics.Debug.WriteLine($"[EmailService] Sent approval email to {toEmail} for request {request.RequestID} step {step.Index}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EmailService] Error in SendApprovalRequest: {ex.Message}");
            }
        }

        /// <summary>
        /// THÊM MỚI: Gửi email thông báo đã phê duyệt cho Hoangthi.Minh@brother-bivn.com.vn
        /// </summary>
        public void SendApprovalCompletedNotification(Request request, WorkflowStep completedStep, User approver)
        {
            try
            {
                const string notificationEmail = "Hoangthi.Minh@brother-bivn.com.vn"; //Hoangthi.Minh@brother-bivn.com.vn

                var host = ConfigurationManager.AppSettings["SmtpHost"];
                var port = int.Parse(ConfigurationManager.AppSettings["SmtpPort"]);
                var fromAddress = new MailAddress("testrun.system@brother-bivn.com.vn", "Test Run System");
                var toAddress = new MailAddress(notificationEmail);

                // Lấy thông tin Part từ Excel
                var (partNumber, partName) = GetPartInfo(request);
                var partInfo = !string.IsNullOrEmpty(partName) ? $"{partNumber} - {partName}" : partNumber;

                string subject = $"承認完了通知/Thông báo đã phê duyệt [{request.RequestID}] {partInfo}";

                string body = $@"
                    <div style='font-family: ""Yu Gothic"", ""Hiragino Kaku Gothic ProN"", ""Meiryo"", sans-serif; max-width: 500px; margin: 0 auto; border: 1px solid #ddd;'>
                        <div style='background: #e8f5e8; padding: 10px 15px; border-bottom: 1px solid #ddd;'>
                            <div style='font-weight: bold; font-size: 14px; color: #2d5a2d;'>✓ 承認完了通知/Thông báo đã phê duyệt</div>
                            <div style='font-size: 12px; color: #666; margin-top: 3px;'>テストラン承認システム / Test Run Approval System</div>
                        </div>
                        
                        <div style='padding: 15px;'>
                            <table style='width: 100%; font-size: 13px; border-collapse: collapse;'>
                                <tr>
                                    <td style='padding: 5px 0; font-weight: bold; width: 100px;'>申請番号/Mã đơn:</td>
                                    <td style='padding: 5px 0; color: #0066cc; font-weight: bold;'>{request.RequestID}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 5px 0; font-weight: bold;'>部品情報/Linh kiện:</td>
                                    <td style='padding: 5px 0;'>{partInfo}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 5px 0; font-weight: bold;'>承認者/Người duyệt:</td>
                                    <td style='padding: 5px 0;'>{approver?.Name ?? completedStep.ApproverADID} ({completedStep.DeptCode})</td>
                                </tr>
                                <tr>
                                    <td style='padding: 5px 0; font-weight: bold;'>承認日時/Thời gian:</td>
                                    <td style='padding: 5px 0;'>{completedStep.ApprovedAt?.ToString("yyyy/MM/dd HH:mm") ?? DateTime.Now.ToString("yyyy/MM/dd HH:mm")}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 5px 0; font-weight: bold;'>状態/Trạng thái:</td>
                                    <td style='padding: 5px 0; color: #009900; font-weight: bold;'>承認済み/Đã phê duyệt</td>
                                </tr>
                            </table>
                            
                            {(string.IsNullOrEmpty(completedStep.Comment) ? "" : $@"
                            <div style='margin: 15px 0; padding: 10px; background: #f9f9f9; border-left: 3px solid #0066cc;'>
                                <div style='font-weight: bold; font-size: 12px; margin-bottom: 5px;'>コメント/Ghi chú:</div>
                                <div style='font-size: 12px;'>{completedStep.Comment}</div>
                            </div>")}
                            
                            <div style='font-size: 11px; color: #999; text-align: center; border-top: 1px solid #eee; padding-top: 10px; margin-top: 15px;'>
                                Brother Industries Vietnam - Test Run System<br/>
                                この通知は自動送信されています / This notification is sent automatically
                            </div>
                        </div>
                    </div>";

                var smtp = new SmtpClient
                {
                    Host = host,
                    Port = port,
                    EnableSsl = false,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = true
                };

                using (var message = new MailMessage(fromAddress, toAddress)
                {
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                })
                {
                    smtp.Send(message);
                    System.Diagnostics.Debug.WriteLine($"[EmailService] Sent approval completion notification to {notificationEmail} for request {request.RequestID}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EmailService] Error in SendApprovalCompletedNotification: {ex.Message}");
            }
        }

        private string GetActionTextByStep(WorkflowStep step)
        {
            // Đơn giản hóa: chỉ dựa vào Actor với resource strings
            switch (step.Actor)
            {
                case StepActor.Approver:
                    return GetResourceString("ApprovalAndStamp");
                case StepActor.DataEntry:
                    return GetResourceString("DataEntryAndInput");
                default:
                    return GetResourceString("ProcessRequest");
            }
        }

        /// <summary>
        /// Helper method để lấy action text theo cả hai ngôn ngữ
        /// </summary>
        private string GetBilingualActionText(WorkflowStep step)
        {
            switch (step.Actor)
            {
                case StepActor.Approver:
                    return GetBilingualResourceString("ApprovalAndStamp");
                case StepActor.DataEntry:
                    return GetBilingualResourceString("DataEntryAndInput");
                default:
                    return GetBilingualResourceString("ProcessRequest");
            }
        }

        /// <summary>
        /// Gửi thông báo từ chối đến người tạo đơn - SỬA: Đơn giản hóa
        /// </summary>
        public void SendRejectNotification(Request request, User rejector, string comment)
        {
            try
            {
                string toEmail = GetUserEmail(request.CreatedByADID);

                var host = ConfigurationManager.AppSettings["SmtpHost"];
                var port = int.Parse(ConfigurationManager.AppSettings["SmtpPort"]);

                var fromAddress = new MailAddress("testrun.system@brother-bivn.com.vn", "Test Run System");
                var toAddress = new MailAddress(toEmail);

                // Lấy thông tin Part từ Excel
                var (partNumber, partName) = GetPartInfo(request);
                var partInfo = !string.IsNullOrEmpty(partName) ? $"{partNumber} - {partName}" : partNumber;

                string subject = $"申請却下通知/Thông báo từ chối [{request.RequestID}] {partInfo}";

                string body = $@"
                    <div style='font-family: ""Yu Gothic"", ""Hiragino Kaku Gothic ProN"", ""Meiryo"", sans-serif; max-width: 500px; margin: 0 auto; border: 1px solid #ddd;'>
                        <div style='background: #ffe8e8; padding: 10px 15px; border-bottom: 1px solid #ddd;'>
                            <div style='font-weight: bold; font-size: 14px; color: #cc0000;'>✕ 申請却下通知/Thông báo từ chối</div>
                            <div style='font-size: 12px; color: #666; margin-top: 3px;'>テストラン承認システム / Test Run Approval System</div>
                        </div>
                        
                        <div style='padding: 15px;'>
                            <table style='width: 100%; font-size: 13px; border-collapse: collapse;'>
                                <tr>
                                    <td style='padding: 5px 0; font-weight: bold; width: 100px;'>申請番号/Mã đơn:</td>
                                    <td style='padding: 5px 0; color: #cc0000; font-weight: bold;'>{request.RequestID}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 5px 0; font-weight: bold;'>部品情報/Linh kiện:</td>
                                    <td style='padding: 5px 0;'>{partInfo}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 5px 0; font-weight: bold;'>却下者/Người từ chối:</td>
                                    <td style='padding: 5px 0;'>{rejector.Name} ({rejector.DeptCode})</td>
                                </tr>
                                <tr>
                                    <td style='padding: 5px 0; font-weight: bold;'>状態/Trạng thái:</td>
                                    <td style='padding: 5px 0; color: #cc0000; font-weight: bold;'>却下済み/Đã từ chối</td>
                                </tr>
                            </table>
                            
                            {(string.IsNullOrEmpty(comment) ? "" : $@"
                            <div style='margin: 15px 0; padding: 10px; background: #fff3cd; border-left: 3px solid #ff6600;'>
                                <div style='font-weight: bold; font-size: 12px; margin-bottom: 5px;'>却下理由/Lý do từ chối:</div>
                                <div style='font-size: 12px;'>{comment}</div>
                            </div>")}
                            
                            <div style='font-size: 11px; color: #999; text-align: center; border-top: 1px solid #eee; padding-top: 10px; margin-top: 15px;'>
                                Brother Industries Vietnam - Test Run System
                            </div>
                        </div>
                    </div>";

                var smtp = new SmtpClient
                {
                    Host = host,
                    Port = port,
                    EnableSsl = false,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = true
                };

                using (var message = new MailMessage(fromAddress, toAddress)
                {
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                })
                {
                    smtp.Send(message);
                    System.Diagnostics.Debug.WriteLine($"[EmailService] Sent reject notification to {toEmail} for request {request.RequestID}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EmailService] Error in SendRejectNotification: {ex.Message}");
            }
        }

        /// <summary>
        /// Gửi thông báo từ chối về cho người có thể chỉnh sửa - SỬA: Đơn giản hóa
        /// </summary>
        public void SendRejectNotificationToPrevApprover(Request request, User prevUser, User rejector, string comment, string editUrl)
        {
            try
            {
                string toEmail = prevUser?.Email ?? GetUserEmail(request.CreatedByADID);

                if (string.IsNullOrEmpty(toEmail))
                {
                    System.Diagnostics.Debug.WriteLine($"[EmailService] No email found for reject notification");
                    return;
                }

                var host = ConfigurationManager.AppSettings["SmtpHost"];
                var port = int.Parse(ConfigurationManager.AppSettings["SmtpPort"]);

                var fromAddress = new MailAddress("testrun.system@brother-bivn.com.vn", "Test Run System");
                var toAddress = new MailAddress(toEmail);

                // Lấy thông tin Part từ Excel
                var (partNumber, partName) = GetPartInfo(request);
                var partInfo = !string.IsNullOrEmpty(partName) ? $"{partNumber} - {partName}" : partNumber;

                string subject = $"修正依頼/Yêu cầu sửa đổi [{request.RequestID}] {partInfo}";

                string body = $@"
                    <div style='font-family: ""Yu Gothic"", ""Hiragino Kaku Gothic ProN"", ""Meiryo"", sans-serif; max-width: 500px; margin: 0 auto; border: 1px solid #ddd;'>
                        <div style='background: #fff3cd; padding: 10px 15px; border-bottom: 1px solid #ddd;'>
                            <div style='font-weight: bold; font-size: 14px; color: #856404;'>⚠ 修正依頼/Yêu cầu sửa đổi</div>
                            <div style='font-size: 12px; color: #666; margin-top: 3px;'>テストラン承認システム / Test Run Approval System</div>
                        </div>
                        
                        <div style='padding: 15px;'>
                            <table style='width: 100%; font-size: 13px; border-collapse: collapse;'>
                                <tr>
                                    <td style='padding: 5px 0; font-weight: bold; width: 100px;'>申請番号/Mã đơn:</td>
                                    <td style='padding: 5px 0; color: #856404; font-weight: bold;'>{request.RequestID}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 5px 0; font-weight: bold;'>部品情報/Linh kiện:</td>
                                    <td style='padding: 5px 0;'>{partInfo}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 5px 0; font-weight: bold;'>却下者/Người từ chối:</td>
                                    <td style='padding: 5px 0;'>{rejector.Name} ({rejector.DeptCode})</td>
                                </tr>
                                <tr>
                                    <td style='padding: 5px 0; font-weight: bold;'>状態/Trạng thái:</td>
                                    <td style='padding: 5px 0; color: #856404; font-weight: bold;'>修正必要/Cần sửa đổi</td>
                                </tr>
                            </table>
                            
                            {(string.IsNullOrEmpty(comment) ? "" : $@"
                            <div style='margin: 15px 0; padding: 10px; background: #f8d7da; border-left: 3px solid #dc3545;'>
                                <div style='font-weight: bold; font-size: 12px; margin-bottom: 5px;'>修正理由/Lý do sửa đổi:</div>
                                <div style='font-size: 12px;'>{comment}</div>
                            </div>")}
                            
                            <div style='text-align: center; margin: 20px 0;'>
                                <a href='{editUrl}' 
                                   style='display: inline-block; padding: 8px 16px; background: #856404; color: white; 
                                          text-decoration: none; border-radius: 3px; font-size: 13px; font-weight: bold;'>
                                    修正画面を開く / Mở màn hình sửa đổi ≫
                                </a>
                            </div>
                            
                            <div style='font-size: 11px; color: #999; text-align: center; border-top: 1px solid #eee; padding-top: 10px; margin-top: 15px;'>
                                Brother Industries Vietnam - Test Run System
                            </div>
                        </div>
                    </div>";

                var smtp = new SmtpClient
                {
                    Host = host,
                    Port = port,
                    EnableSsl = false,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = true
                };

                using (var message = new MailMessage(fromAddress, toAddress)
                {
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                })
                {
                    smtp.Send(message);
                    System.Diagnostics.Debug.WriteLine($"[EmailService] Sent edit notification to {toEmail} for request {request.RequestID}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EmailService] Error in SendRejectNotificationToPrevApprover: {ex.Message}");
            }
        }

        /// <summary>
        /// Method gửi email cho người được chỉ định cụ thể - SỬA: Đơn giản hóa
        /// </summary>
        public void SendApprovalRequestToSpecificUser(Request request, WorkflowStep step, string approvalUrl, User selectedApprover)
        {
            try
            {
                var fromAddress = new MailAddress("testrun.system@brother-bivn.com.vn", "Test Run System");
                var toAddress = new MailAddress(selectedApprover.Email ?? "admin@brother-bivn.com.vn");

                var host = ConfigurationManager.AppSettings["SmtpHost"];
                var port = int.Parse(ConfigurationManager.AppSettings["SmtpPort"]);

                // Lấy thông tin Part từ Excel
                var (partNumber, partName) = GetPartInfo(request);
                var partInfo = !string.IsNullOrEmpty(partName) ? $"{partNumber} - {partName}" : partNumber;

                string subject = $"指名承認依頼/Yêu cầu phê duyệt [{request.RequestID}] {partInfo}";

                string body = $@"
                    <div style='font-family: ""Yu Gothic"", ""Hiragino Kaku Gothic ProN"", ""Meiryo"", sans-serif; max-width: 500px; margin: 0 auto; border: 1px solid #ddd;'>
                        <div style='background: #e8f5e8; padding: 10px 15px; border-bottom: 1px solid #ddd;'>
                            <div style='font-weight: bold; font-size: 14px; color: #2d5a2d;'>✓ 指名承認依頼/Yêu cầu phê duyệt</div>
                            <div style='font-size: 12px; color: #666; margin-top: 3px;'>テストラン承認システム / Test Run Approval System</div>
                        </div>
                        
                        <div style='padding: 15px;'>
                            <div style='margin-bottom: 15px; font-size: 13px;'>
                                {selectedApprover.Name}様へ / Gửi {selectedApprover.Name}:<br/>
                                下記申請の承認をお願いいたします / Vui lòng phê duyệt đơn dưới đây:
                            </div>
                            
                            <table style='width: 100%; font-size: 13px; border-collapse: collapse;'>
                                <tr>
                                    <td style='padding: 5px 0; font-weight: bold; width: 100px;'>申請番号/Mã đơn:</td>
                                    <td style='padding: 5px 0; color: #009900; font-weight: bold;'>{request.RequestID}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 5px 0; font-weight: bold;'>部品情報/Linh kiện:</td>
                                    <td style='padding: 5px 0;'>{partInfo}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 5px 0; font-weight: bold;'>申請者/Người tạo:</td>
                                    <td style='padding: 5px 0;'>{request.CreatedByADID}</td>
                                </tr>
                            </table>
                            
                            <div style='text-align: center; margin: 20px 0;'>
                                <a href='{approvalUrl}' 
                                   style='display: inline-block; padding: 8px 16px; background: #009900; color: white; 
                                          text-decoration: none; border-radius: 3px; font-size: 13px; font-weight: bold;'>
                                    承認画面を開く / Mở màn hình phê duyệt ≫
                                </a>
                            </div>
                        
                            <div style='font-size: 11px; color: #999; text-align: center; border-top: 1px solid #eee; padding-top: 10px; margin-top: 15px;'>
                                Brother Industries Vietnam - Test Run System
                            </div>
                        </div>
                    </div>";

                var smtp = new SmtpClient
                {
                    Host = host,
                    Port = port,
                    EnableSsl = false,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = true
                };

                using (var message = new MailMessage(fromAddress, toAddress)
                {
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                })
                {
                    smtp.Send(message);
                    System.Diagnostics.Debug.WriteLine($"[EmailService] Sent specific approval email to {selectedApprover.Email} for request {request.RequestID}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EmailService] Error in SendApprovalRequestToSpecificUser: {ex.Message}");
            }
        }

        /// <summary>
        /// Helper method để validate email
        /// </summary>
        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Dispose DbContext
        /// </summary>
        public void Dispose()
        {
            _context?.Dispose();
        }

        
    }
}