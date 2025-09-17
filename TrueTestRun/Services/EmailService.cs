using System;
using System.Net;
using System.Net.Mail;
using System.Web;
using System.Web.Hosting;
using TrueTestRun.Models;
using System.Configuration;
using System.IO;
using System.Linq;

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
                return ("未定義/Chưa定", "");
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

                case 1: return "Doan.PhamCong@brother-bivn.com.vn"; //Doan.PhamCong@brother-bivn.com.vn
                case 2: return "Ly.NguyenThi@brother-bivn.com.vn"; //Ly.NguyenThi@brother-bivn.com.vn
                case 3: return "jun.sato@brother-bivn.com.vn"; //jun.sato@brother-bivn.com.vn
                case 4: return "Ly.NguyenThi@brother-bivn.com.vn"; //Ly.NguyenThi@brother-bivn.com.vn
                case 5: return "jun.sato@brother-bivn.com.vn"; //jun.sato@brother-bivn.com.vn
                case 6: return "Ly.NguyenThi@brother-bivn.com.vn"; //Ly.NguyenThi@brother-bivn.com.vn
                case 7: return "jun.sato@brother-bivn.com.vn"; //jun.sato@brother-bivn.com.vn
                case 8: return "nguyenthi.duyen5@brother-bivn.com.vn"; //nguyenthi.duyen5@brother-bivn.com.vn
                case 9: return "Doan.PhamCong@brother-bivn.com.vn"; //Doan.PhamCong@brother-bivn.com.vn
                case 10: return "naoya.yada@brother-bivn.com.vn"; //naoya.yada@brother-bivn.com.vn
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
                if (string.IsNullOrEmpty(adid)) return null;
                var user = _context.Users.FirstOrDefault(u => u.ADID == adid);
                return user?.Email;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EmailService] GetUserEmail error: {ex}");
                return null;
            }
        }

        /// <summary>
        /// THÊM: Helper method để thêm CC cho các email quan trọng
        /// </summary>
        private void AddManagementCC(MailMessage message)
        {
            try
            {
                message.CC.Add("giang.nguyenthi@brother-bivn.com.vn");
                message.CC.Add("dangthi.ngoc2@brother-bivn.com.vn");
                message.CC.Add("phamduc.anh@brother-bivn.com.vn");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EmailService] Error adding management CC: {ex.Message}");
            }
        }

        /// <summary>
        /// Create and configure SmtpClient based on web.config appSettings.
        /// Supports optional credentials (SmtpUser / SmtpPass) and SSL.
        /// </summary>
        private SmtpClient CreateSmtpClient()
        {
            var host = ConfigurationManager.AppSettings["SmtpHost"];
            var portStr = ConfigurationManager.AppSettings["SmtpPort"];
            int port;
            if (!int.TryParse(portStr, out port)) port = 25;

            var enableSsl = false;
            bool.TryParse(ConfigurationManager.AppSettings["SmtpEnableSsl"], out enableSsl);

            var smtpUser = ConfigurationManager.AppSettings["SmtpUser"];
            var smtpPass = ConfigurationManager.AppSettings["SmtpPass"];

            var client = new SmtpClient
            {
                Host = host,
                Port = port,
                EnableSsl = enableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            if (!string.IsNullOrEmpty(smtpUser) && !string.IsNullOrEmpty(smtpPass))
            {
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(smtpUser, smtpPass);
            }
            else
            {
                // If no explicit credentials provided, keep default credentials (useful for internal SMTP relay)
                client.UseDefaultCredentials = true;
            }

            return client;
        }

        /// <summary>
        /// Gửi email phê duyệt/ghi nhập - CHỈNH SỬA: Thêm CC cho management
        /// </summary>
        public void SendApprovalRequest(Request request, WorkflowStep step, string approvalUrl, bool isResubmission = false)
        {
            try
            {
                string toEmail = null;

                // Kiểm tra nếu có người được chỉ định cụ thể trong NextApproverADID
                if (!string.IsNullOrEmpty(step.NextApproverADID))
                {
                    toEmail = GetUserEmail(step.NextApproverADID);
                }

                if (string.IsNullOrEmpty(toEmail))
                {
                    toEmail = GetDesignatedApproverEmail(step.DeptCode, step.Role, step.Index);
                }

                // Validate email
                if (string.IsNullOrEmpty(toEmail) || !IsValidEmail(toEmail))
                {
                    System.Diagnostics.Debug.WriteLine($"[EmailService] Invalid email for step {step.StepName}: {toEmail}");
                    return;
                }

                var fromAddress = new MailAddress("testrun.system@brother-bivn.com.vn", "Test Run System");
                var toAddress = new MailAddress(toEmail);

                // Lấy thông tin Part từ Excel
                var (partNumber, partName) = GetPartInfo(request);
                var partInfo = !string.IsNullOrEmpty(partName) ? $"{partNumber} - {partName}" : partNumber;

                string actionType = step.Actor == StepActor.Approver ? "承認依頼/Yêu cầu phê duyệt" : "データ入力依頼/Yêu cầu ghi nhập";
                string subject = $"{actionType} [{request.RequestID}] {partInfo}";

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

                using (var message = new MailMessage(fromAddress, toAddress)
                {
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                })
                {
                    // THÊM: CC cho management
                    AddManagementCC(message);

                    try
                    {
                        using (var smtp = CreateSmtpClient())
                        {
                            smtp.Send(message);
                        }
                        System.Diagnostics.Debug.WriteLine($"[EmailService] Sent approval email to {toEmail} with management CC for request {request.RequestID} step {step.Index}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[EmailService] SMTP send failed in SendApprovalRequest to {toEmail}: {ex}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EmailService] Error in SendApprovalRequest: {ex.Message}");
            }
        }

        /// <summary>
        /// THÊM MỚI: Gửi email thông báo đã phê duyệt - CHỈNH SỬA: Thêm CC cho management
        /// </summary>
        public void SendApprovalCompletedNotification(Request request, WorkflowStep completedStep, User approver)
        {
            try
            {
                const string notificationEmail = "Hoangthi.Minh@brother-bivn.com.vn"; //Hoangthi.Minh@brother-bivn.com.vn

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

                using (var message = new MailMessage(fromAddress, toAddress)
                {
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                })
                {
                    // THÊM: CC cho management
                    AddManagementCC(message);

                    try
                    {
                        using (var smtp = CreateSmtpClient())
                        {
                            smtp.Send(message);
                        }
                        System.Diagnostics.Debug.WriteLine($"[EmailService] Sent approval completion notification to {notificationEmail} with management CC for request {request.RequestID}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[EmailService] SMTP send failed in SendApprovalCompletedNotification to {notificationEmail}: {ex}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EmailService] Error in SendApprovalCompletedNotification: {ex.Message}");
            }
        }

        /// <summary>
        /// Gửi thông báo từ chối đến người tạo đơn - CHỈNH SỬA: Thêm CC cho management
        /// </summary>
        public void SendRejectNotification(Request request, User rejector, string comment)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[EmailService.SendRejectNotification] Enter for request {request?.RequestID}");

                string toEmail = null;

                // Ưu tiên lấy email người tạo đơn
                if (!string.IsNullOrEmpty(request?.CreatedByADID))
                {
                    toEmail = GetUserEmail(request.CreatedByADID);
                    System.Diagnostics.Debug.WriteLine($"[EmailService] Creator email: {toEmail} for ADID: {request.CreatedByADID}");
                }

                // Fallback nếu không tìm được email người tạo
                if (string.IsNullOrEmpty(toEmail))
                {
                    toEmail = ConfigurationManager.AppSettings["FallbackRejectRecipient"] ?? "phamduc.anh@brother-bivn.com.vn";
                    System.Diagnostics.Debug.WriteLine($"[EmailService] Using fallback email: {toEmail}");
                }

                if (string.IsNullOrEmpty(toEmail) || !IsValidEmail(toEmail))
                {
                    System.Diagnostics.Debug.WriteLine($"[EmailService] Invalid target email for reject notification: {toEmail}");
                    return;
                }

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
                            <td style='padding: 5px 0; font-weight: bold;'>却下日時/Thời gian từ chối:</td>
                            <td style='padding: 5px 0;'>{DateTime.Now.ToString("yyyy/MM/dd HH:mm")}</td>
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
                        Brother Industries Vietnam - Test Run System<br/>
                        この通知は自動送信されています / This notification is sent automatically
                    </div>
                </div>
            </div>";

                using (var message = new MailMessage(fromAddress, toAddress)
                {
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                })
                {
                    // THÊM: CC cho management
                    AddManagementCC(message);

                    try
                    {
                        using (var smtp = CreateSmtpClient())
                        {
                            smtp.Send(message);
                        }
                        System.Diagnostics.Debug.WriteLine($"[EmailService] SUCCESSFULLY sent reject notification to {toEmail} for request {request.RequestID}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[EmailService] SMTP send failed in SendRejectNotification to {toEmail}: {ex}");
                        throw; // Re-throw để có thể debug SMTP issues
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EmailService] Error in SendRejectNotification: {ex.Message}");
                throw; // Re-throw để có thể debug issues
            }
        }

        /// <summary>
        /// Gửi thông báo từ chối về cho người có thể chỉnh sửa - SỬA: Gửi cho người ở step trước
        /// </summary>
        public void SendRejectNotificationToPrevApprover(Request request, User prevUser, User rejector, string comment, string editUrl)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[EmailService.SendRejectNotificationToPrevApprover] Enter. request={request?.RequestID}, prevUser.ADID={prevUser?.ADID}, prevUser.Email={prevUser?.Email}, rejector.ADID={rejector?.ADID}");

                string toEmail = null;

                // 1) If caller provided prevUser with email, prefer that
                if (prevUser != null && !string.IsNullOrEmpty(prevUser.Email))
                {
                    toEmail = prevUser.Email;
                    System.Diagnostics.Debug.WriteLine($"[EmailService] Using prevUser.Email: {toEmail}");
                }

                // 2) If prevUser ADID exists but email missing, try lookup in DB
                if (string.IsNullOrEmpty(toEmail) && prevUser != null && !string.IsNullOrEmpty(prevUser.ADID))
                {
                    toEmail = GetUserEmail(prevUser.ADID);
                    System.Diagnostics.Debug.WriteLine($"[EmailService] Lookup by prevUser.ADID={prevUser.ADID} -> {toEmail}");
                }

                // 4) Existing logic: try to derive from request.History (only if we still don't have an email)
                if (string.IsNullOrEmpty(toEmail))
                {
                    var rejectedStep = request?.History?.FirstOrDefault(h => h.Status == "Rejected");
                    if (rejectedStep != null && rejectedStep.Index > 0)
                    {
                        var prevStep = request.History?
                            .Where(h => h.Index == rejectedStep.Index - 1)
                            .OrderByDescending(h => h.ApprovedAt ?? DateTime.MinValue)
                            .FirstOrDefault();

                        // If previous step has ApproverADID, try lookup
                        if (prevStep != null && !string.IsNullOrEmpty(prevStep.ApproverADID))
                        {
                            var prevUser_found = _context.Users.FirstOrDefault(u => u.ADID == prevStep.ApproverADID);
                            if (prevUser_found != null && !string.IsNullOrEmpty(prevUser_found.Email))
                            {
                                toEmail = prevUser_found.Email;
                                System.Diagnostics.Debug.WriteLine($"[EmailService] Found prevStep approver email: {toEmail} from step {prevStep.Index}");
                            }
                            else
                            {
                                // fallback to designated approver for that step index
                                toEmail = GetDesignatedApproverEmail("", "", prevStep.Index);
                                System.Diagnostics.Debug.WriteLine($"[EmailService] Using hardcode email for step {prevStep.Index}: {toEmail}");
                            }
                        }
                        else if (prevStep != null)
                        {
                            // fallback when previous step exists but no approver info
                            var fallback = GetDesignatedApproverEmail("", "", prevStep.Index);
                            System.Diagnostics.Debug.WriteLine($"[EmailService] prevStep has no ApproverADID, using hardcode fallback: {fallback}");
                            toEmail = fallback;
                        }

                        // SPECIAL CASE: if rejected at step 3 we want to notify step 2 (explicit)
                        // ensure mapping for the user request: case 3 rejection -> notify step 2 approver
                        try
                        {
                            if (rejectedStep.Index == 3)
                            {
                                var step2Email = GetDesignatedApproverEmail("", "", 2);
                                if (!string.IsNullOrEmpty(step2Email))
                                {
                                    System.Diagnostics.Debug.WriteLine($"[EmailService] Special mapping: rejected step 3 -> notify step 2: {step2Email}");
                                    toEmail = step2Email;
                                }
                            }
                        }
                        catch (Exception exMap)
                        {
                            System.Diagnostics.Debug.WriteLine($"[EmailService] Error applying special mapping for reject notification: {exMap}");
                        }
                    }
                }

                if (string.IsNullOrEmpty(toEmail))
                {
                    System.Diagnostics.Debug.WriteLine("[EmailService] No email found for reject notification, aborting.");
                    return;
                }

                if (!IsValidEmail(toEmail))
                {
                    System.Diagnostics.Debug.WriteLine($"[EmailService] Resolved email is not valid: {toEmail}");
                    return;
                }

                var fromAddress = new MailAddress("testrun.system@brother-bivn.com.vn", "Test Run System");
                var toAddress = new MailAddress(toEmail);

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
                <td style='padding: 5px 0;'>{rejector?.Name} ({rejector?.DeptCode})</td>
            </tr>
            <tr>
                <td style='padding: 5px 0; font-weight: bold;'>状態/Trạng thái:</td>
                <td style='padding: 5px 0; color: #856404; font-weight: bold;'>修正必要/Cần sửa đổi</td>
            </tr>
        </table>

        {(string.IsNullOrEmpty(comment) ? "" : $@"
        <div style='margin: 15px 0; padding: 10px; background: #f8d7da; border-left: 3px solid #dc3545;'>
            <div style='font-weight: bold; font-size: 12px; margin-bottom: 5px;'>修正理由/Lý do từ chối:</div>
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

                using (var message = new MailMessage(fromAddress, toAddress)
                {
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                })
                {
                    AddManagementCC(message);

                    try
                    {
                        using (var smtp = CreateSmtpClient())
                        {
                            smtp.Send(message);
                        }
                        System.Diagnostics.Debug.WriteLine($"[EmailService.SendRejectNotificationToPrevApprover] Sent edit notification to {toEmail} for request {request.RequestID}");
                    }
                    catch (Exception sendEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[EmailService.SendRejectNotificationToPrevApprover] SMTP send failed to {toEmail}: {sendEx}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EmailService] Error in SendRejectNotificationToPrevApprover: {ex}");
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

                using (var message = new MailMessage(fromAddress, toAddress)
                {
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                })
                {
                    // THÊM: CC cho management
                    AddManagementCC(message);

                    try
                    {
                        using (var smtp = CreateSmtpClient())
                        {
                            smtp.Send(message);
                        }
                        System.Diagnostics.Debug.WriteLine($"[EmailService] Sent specific approval email to {selectedApprover.Email} for request {request.RequestID}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[EmailService] SMTP send failed in SendApprovalRequestToSpecificUser to {selectedApprover.Email}: {ex}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EmailService] Error in SendApprovalRequestToSpecificUser: {ex.Message}");
            }
        }

        // Thêm method này vào EmailService

        /// <summary>
        /// Gửi email nhắc nhở xúc tiến lưu trình
        /// </summary>
        public void SendReminderEmail(Request request, WorkflowStep step, string approvalUrl, User sender)
        {
            try
            {
                string toEmail = null;

                // Kiểm tra nếu có người được chỉ định cụ thể trong NextApproverADID
                if (!string.IsNullOrEmpty(step.NextApproverADID))
                {
                    toEmail = GetUserEmail(step.NextApproverADID);
                }

                if (string.IsNullOrEmpty(toEmail))
                {
                    toEmail = GetDesignatedApproverEmail(step.DeptCode, step.Role, step.Index);
                }

                // Validate email
                if (string.IsNullOrEmpty(toEmail) || !IsValidEmail(toEmail))
                {
                    System.Diagnostics.Debug.WriteLine($"[EmailService] Invalid email for reminder step {step.StepName}: {toEmail}");
                    return;
                }

                var fromAddress = new MailAddress("testrun.system@brother-bivn.com.vn", "Test Run System");
                var toAddress = new MailAddress(toEmail);

                // Lấy thông tin Part từ Excel
                var (partNumber, partName) = GetPartInfo(request);
                var partInfo = !string.IsNullOrEmpty(partName) ? $"{partNumber} - {partName}" : partNumber;

                string actionType = step.Actor == StepActor.Approver ? "承認依頼/Yêu cầu phê duyệt" : "データ入力依頼/Yêu cầu ghi nhập";
                string subject = $"【催促】{actionType} [{request.RequestID}] {partInfo}";

                string body = $@"
            <div style='font-family: ""Yu Gothic"", ""Hiragino Kaku Gothic ProN"", ""Meiryo"", sans-serif; max-width: 500px; margin: 0 auto; border: 1px solid #ddd;'>
                <div style='background: #fff3cd; padding: 10px 15px; border-bottom: 1px solid #ddd;'>
                    <div style='font-weight: bold; font-size: 14px; color: #856404;'>⚠ 【催促】{actionType}</div>
                    <div style='font-size: 12px; color: #666; margin-top: 3px;'>テストラン承認システム / Test Run Reminder System</div>
                </div>
                
                <div style='padding: 15px;'>
                    <div style='margin-bottom: 15px; padding: 10px; background: #fef3c7; border-left: 3px solid #f59e0b; border-radius: 4px;'>
                        <div style='font-weight: bold; font-size: 12px; color: #92400e; margin-bottom: 5px;'>処理催促/Nhắc nhở xử lý</div>
                        <div style='font-size: 12px; color: #92400e;'>下記申請の処理をお願いいたします / Vui lòng xử lý đơn dưới đây</div>
                    </div>
                    
                    <table style='width: 100%; font-size: 13px; border-collapse: collapse;'>
                        <tr>
                            <td style='padding: 5px 0; font-weight: bold; width: 100px;'>申請番号/Mã đơn:</td>
                            <td style='padding: 5px 0; color: #f59e0b; font-weight: bold;'>{request.RequestID}</td>
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
                            <td style='padding: 5px 0; font-weight: bold;'>現在のステップ/Bước hiện tại:</td>
                            <td style='padding: 5px 0; color: #f59e0b; font-weight: bold;'>{step.StepName}</td>
                        </tr>
                        <tr>
                            <td style='padding: 5px 0; font-weight: bold;'>催促者/Người nhắc:</td>
                            <td style='padding: 5px 0;'>{sender.Name} ({sender.DeptCode})</td>
                        </tr>
                        <tr>
                            <td style='padding: 5px 0; font-weight: bold;'>状態/Trạng thái:</td>
                            <td style='padding: 5px 0; color: #f59e0b;'>処理待ち/Chờ xử lý</td>
                        </tr>
                    </table>
                    
                    <div style='text-align: center; margin: 20px 0;'>
                        <a href='{approvalUrl}' 
                           style='display: inline-block; padding: 8px 16px; background: #f59e0b; color: white; 
                                  text-decoration: none; border-radius: 3px; font-size: 13px; font-weight: bold;'>
                            システムを開く / Mở hệ thống ≫
                        </a>
                    </div>
                
                    <div style='font-size: 11px; color: #999; text-align: center; border-top: 1px solid #eee; padding-top: 10px; margin-top: 15px;'>
                        Brother Industries Vietnam - Test Run System<br/>
                        この催促メールは手動で送信されました / This reminder email was sent manually
                    </div>
                </div>
            </div>";

                using (var message = new MailMessage(fromAddress, toAddress)
                {
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                })
                {
                    // Thêm CC cho management
                    AddManagementCC(message);

                    try
                    {
                        using (var smtp = CreateSmtpClient())
                        {
                            smtp.Send(message);
                        }
                        System.Diagnostics.Debug.WriteLine($"[EmailService] Sent reminder email to {toEmail} for request {request.RequestID} step {step.Index} by {sender.ADID}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[EmailService] SMTP send failed in SendReminderEmail to {toEmail}: {ex}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EmailService] Error in SendReminderEmail: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Helper method để validate email
        /// </summary>
        private bool IsValidEmail(string email)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email)) return false;
                email = email.Trim();
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