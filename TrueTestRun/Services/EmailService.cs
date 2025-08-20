using System;
using System.Configuration;
using System.Net.Mail;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using TrueTestRun.Models;

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
        /// Lấy email của người được chỉ định cho từng step - HARDCODE
        /// </summary>
        private string GetDesignatedApproverEmail(string deptCode, string role, int stepIndex)
        {
            // HARDCODE EMAIL CHO TỪNG STEP CỤ THỂ
            switch (stepIndex)
            {
                case 1: return "Doan.PhamCong@brother-bivn.com.vn";
                case 2: return "Ly.NguyenThi@brother-bivn.com.vn";
                case 3: return "ChuVan.Long@brother-bivn.com.vn";
                case 4: return "Ly.NguyenThi@brother-bivn.com.vn";
                case 5: return "jun.sato@brother-bivn.com.vn";
                case 6: return "Ly.NguyenThi@brother-bivn.com.vn";
                case 7: return "jun.sato@brother-bivn.com.vn";
                case 8: return "nguyenthi.duyen5@brother-bivn.com.vn";
                case 9: return "Doan.PhamCong@brother-bivn.com.vn";
                case 10: return "naoya.yada@brother-bivn.com.vn";
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
        /// Gửi email phê duyệt - SỬA: Hiển thị cả tiếng Việt và tiếng Nhật
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

                // SỬA: Subject sử dụng bilingual
                string subject = isResubmission
                    ? $"[{GetBilingualResourceString("Resubmitted")}] {GetBilingualResourceString("TestRunRequestProcessing")}: {request.RequestID}"
                    : $"[{GetBilingualResourceString("Processing")}] {GetBilingualResourceString("TestRunRequestProcessing")}: {request.RequestID}";

                // Tùy chỉnh nội dung email theo step
                string actionText = GetActionTextByStep(step);

                // SỬA: Body sử dụng bilingual strings
                string body = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                        <div style='background: #007bff; color: white; padding: 20px; text-align: center;'>
                            <h2 style='margin: 0;'>🔔 Test Run System - {GetBilingualResourceString("ProcessingNotification")}</h2>
                        </div>
                        <div style='padding: 20px; background: #f8f9fa;'>
                            <p style='font-size: 16px; margin-bottom: 20px;'>
                                <strong>{GetBilingualResourceString("NewTestRunRequest")}:</strong>
                            </p>
                            <table style='width: 100%; border-collapse: collapse; margin-bottom: 20px;'>
                                <tr style='background: white;'>
                                    <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>{GetBilingualResourceString("RequestCode")}:</td>
                                    <td style='padding: 10px; border: 1px solid #dee2e6; color: #007bff; font-weight: bold;'>{request.RequestID}</td>
                                </tr>
                                <tr style='background: #f8f9fa;'>
                                    <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>{GetBilingualResourceString("Creator")}:</td>
                                    <td style='padding: 10px; border: 1px solid #dee2e6;'>{request.CreatedByADID}</td>
                                </tr>
                                <tr style='background: white;'>
                                    <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>{GetBilingualResourceString("CreatedDate")}:</td>
                                    <td style='padding: 10px; border: 1px solid #dee2e6;'>{request.CreatedAt:dd/MM/yyyy HH:mm}</td>
                                </tr>
                                <tr style='background: #f8f9fa;'>
                                    <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>{GetBilingualResourceString("CurrentStep")}:</td>
                                    <td style='padding: 10px; border: 1px solid #dee2e6;'>{step.StepName}</td>
                                </tr>
                                <tr style='background: white;'>
                                    <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>{GetBilingualResourceString("Department")}:</td>
                                    <td style='padding: 10px; border: 1px solid #dee2e6;'>{step.DeptCode}</td>
                                </tr>
                                <tr style='background: #f8f9fa;'>
                                    <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>{GetBilingualResourceString("RequiredAction")}:</td>
                                    <td style='padding: 10px; border: 1px solid #dee2e6; color: #28a745; font-weight: bold;'>{GetBilingualActionText(step)}</td>
                                </tr>
                            </table>
                            
                            <div style='text-align: center; margin: 30px 0;'>
                                <a href='{approvalUrl}' 
                                   style='background-color: #007bff; color: white; padding: 15px 30px; 
                                          text-decoration: none; border-radius: 8px; font-weight: bold; 
                                          display: inline-block; box-shadow: 0 2px 4px rgba(0,123,255,0.3);'>
                                    🔗 {GetBilingualResourceString("ViewAndProcess")}
                                </a>
                            </div>
                            
                            <div style='background: #e9ecef; padding: 15px; border-radius: 8px; margin-top: 20px;'>
                                <p style='margin: 0; font-size: 14px; color: #6c757d;'>
                                    ⚠️ <strong>{GetBilingualResourceString("Warning")}:</strong> {GetBilingualResourceString("ProcessingNote")}
                                </p>
                            </div>
                        </div>
                        <div style='background: #6c757d; color: white; padding: 10px; text-align: center; font-size: 12px;'>
                            © Test Run System - Brother Industries Vietnam
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
        /// Gửi thông báo từ chối đến người tạo đơn - SỬA: Hiển thị cả tiếng Việt và tiếng Nhật
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

                string subject = $"[{GetBilingualResourceString("Rejected")}] {GetBilingualResourceString("TestRunRequest")}: {request.RequestID}";
                string body = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                        <div style='background: #dc3545; color: white; padding: 20px; text-align: center;'>
                            <h2 style='margin: 0;'>❌ Test Run System - {GetBilingualResourceString("RejectNotification")}</h2>
                        </div>
                        <div style='padding: 20px; background: #f8f9fa;'>
                            <div style='background: #f8d7da; border: 1px solid #f5c6cb; padding: 15px; border-radius: 8px; margin-bottom: 20px;'>
                                <p style='margin: 0; color: #721c24; font-weight: bold;'>
                                    {GetBilingualResourceString("TestRunRequest")} <strong>{request.RequestID}</strong> {GetBilingualResourceString("WasRejectedBy")} {rejector.Name} ({rejector.DeptCode}).
                                </p>
                            </div>
                            
                            <table style='width: 100%; border-collapse: collapse; margin-bottom: 20px;'>
                                <tr style='background: white;'>
                                    <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>{GetBilingualResourceString("RequestCode")}:</td>
                                    <td style='padding: 10px; border: 1px solid #dee2e6; color: #dc3545; font-weight: bold;'>{request.RequestID}</td>
                                </tr>
                                <tr style='background: #f8f9fa;'>
                                    <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>{GetBilingualResourceString("RejectedBy")}:</td>
                                    <td style='padding: 10px; border: 1px solid #dee2e6;'>{rejector.Name} ({rejector.DeptCode})</td>
                                </tr>
                                <tr style='background: white;'>
                                    <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>{GetBilingualResourceString("RejectionTime")}:</td>
                                    <td style='padding: 10px; border: 1px solid #dee2e6;'>{DateTime.Now:dd/MM/yyyy HH:mm}</td>
                                </tr>
                            </table>
                            
                            <div style='background: #fff3cd; border: 1px solid #ffeaa7; padding: 15px; border-radius: 8px; margin-bottom: 20px;'>
                                <p style='margin: 0; font-weight: bold; color: #856404;'>💬 {GetBilingualResourceString("ReasonNotes")}:</p>
                                <p style='margin: 10px 0 0 0; color: #856404;'>{comment}</p>
                            </div>
                            
                            <div style='background: #e9ecef; padding: 15px; border-radius: 8px;'>
                                <p style='margin: 0; font-size: 14px; color: #6c757d;'>
                                    {GetBilingualResourceString("PleaseReviewAndEdit")}
                                </p>
                            </div>
                        </div>
                        <div style='background: #6c757d; color: white; padding: 10px; text-align: center; font-size: 12px;'>
                            © Test Run System - Brother Industries Vietnam
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
        /// Gửi thông báo từ chối về cho người có thể chỉnh sửa - SỬA: Hiển thị cả tiếng Việt và tiếng Nhật
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

                string subject = $"[{GetBilingualResourceString("NeedEdit")}] {GetBilingualResourceString("TestRunRequest")}: {request.RequestID}";
                string body = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                        <div style='background: #ffc107; color: #212529; padding: 20px; text-align: center;'>
                            <h2 style='margin: 0;'>⚠️ Test Run System - {GetBilingualResourceString("NeedEdit")}</h2>
                        </div>
                        <div style='padding: 20px; background: #f8f9fa;'>
                            <div style='background: #fff3cd; border: 1px solid #ffeaa7; padding: 15px; border-radius: 8px; margin-bottom: 20px;'>
                                <p style='margin: 0; color: #856404; font-weight: bold;'>
                                    {GetBilingualResourceString("TestRunRequest")} <strong>{request.RequestID}</strong> {GetBilingualResourceString("NeedsEditAccordingToRejection")}.
                                </p>
                            </div>
                            
                            <table style='width: 100%; border-collapse: collapse; margin-bottom: 20px;'>
                                <tr style='background: white;'>
                                    <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>{GetBilingualResourceString("RequestCode")}:</td>
                                    <td style='padding: 10px; border: 1px solid #dee2e6; color: #ffc107; font-weight: bold;'>{request.RequestID}</td>
                                </tr>
                                <tr style='background: #f8f9fa;'>
                                    <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>{GetBilingualResourceString("RejectedBy")}:</td>
                                    <td style='padding: 10px; border: 1px solid #dee2e6;'>{rejector.Name} ({rejector.DeptCode})</td>
                                </tr>
                                <tr style='background: white;'>
                                    <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>{GetBilingualResourceString("Time")}:</td>
                                    <td style='padding: 10px; border: 1px solid #dee2e6;'>{DateTime.Now:dd/MM/yyyy HH:mm}</td>
                                </tr>
                            </table>
                            
                            <div style='background: #f8d7da; border: 1px solid #f5c6cb; padding: 15px; border-radius: 8px; margin-bottom: 20px;'>
                                <p style='margin: 0; font-weight: bold; color: #721c24;'>💬 {GetBilingualResourceString("RejectReason")}:</p>
                                <p style='margin: 10px 0 0 0; color: #721c24;'>{comment}</p>
                            </div>
                            
                            <div style='text-align: center; margin: 30px 0;'>
                                <a href='{editUrl}' 
                                   style='background-color: #ffc107; color: #212529; padding: 15px 30px; 
                                          text-decoration: none; border-radius: 8px; font-weight: bold; 
                                          display: inline-block; box-shadow: 0 2px 4px rgba(255,193,7,0.3);'>
                                    ✏️ {GetBilingualResourceString("EditRequestNow")}
                                </a>
                            </div>
                            
                            <div style='background: #e9ecef; padding: 15px; border-radius: 8px;'>
                                <p style='margin: 0; font-size: 14px; color: #6c757d;'>
                                    {GetBilingualResourceString("PleaseReviewEditAndResubmit")}
                                </p>
                            </div>
                        </div>
                        <div style='background: #6c757d; color: white; padding: 10px; text-align: center; font-size: 12px;'>
                            © Test Run System - Brother Industries Vietnam
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
        /// Method gửi email cho người được chỉ định cụ thể - SỬA: Hiển thị cả tiếng Việt và tiếng Nhật
        /// </summary>
        public void SendApprovalRequestToSpecificUser(Request request, WorkflowStep step, string approvalUrl, User selectedApprover)
        {
            try
            {
                var fromAddress = new MailAddress("testrun.system@brother-bivn.com.vn", "Test Run System");
                var toAddress = new MailAddress(selectedApprover.Email ?? "admin@brother-bivn.com.vn");

                var host = ConfigurationManager.AppSettings["SmtpHost"];
                var port = int.Parse(ConfigurationManager.AppSettings["SmtpPort"]);

                string subject = $"[{GetBilingualResourceString("Approval")}] {GetBilingualResourceString("TestRunRequestApproval")}: {request.RequestID}";
                string body = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                        <div style='background: #28a745; color: white; padding: 20px; text-align: center;'>
                            <h2 style='margin: 0;'>✅ Test Run System - {GetBilingualResourceString("DesignatedApproval")}</h2>
                        </div>
                        <div style='padding: 20px; background: #f8f9fa;'>
                            <p style='font-size: 16px; margin-bottom: 20px;'>
                                {GetBilingualResourceString("Hello")} <strong>{selectedApprover.Name}</strong>,
                            </p>
                            <p style='margin-bottom: 20px;'>{GetBilingualResourceString("YouAreDesignatedToApprove")}:</p>
                            
                            <table style='width: 100%; border-collapse: collapse; margin-bottom: 20px;'>
                                <tr style='background: white;'>
                                    <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>{GetBilingualResourceString("RequestCode")}:</td>
                                    <td style='padding: 10px; border: 1px solid #dee2e6; color: #28a745; font-weight: bold;'>{request.RequestID}</td>
                                </tr>
                                <tr style='background: #f8f9fa;'>
                                    <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>{GetBilingualResourceString("Creator")}:</td>
                                    <td style='padding: 10px; border: 1px solid #dee2e6;'>{request.CreatedByADID}</td>
                                </tr>
                                <tr style='background: white;'>
                                    <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>{GetBilingualResourceString("CreatedDate")}:</td>
                                    <td style='padding: 10px; border: 1px solid #dee2e6;'>{request.CreatedAt:dd/MM/yyyy HH:mm}</td>
                                </tr>
                                <tr style='background: #f8f9fa;'>
                                    <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>{GetBilingualResourceString("CurrentStep")}:</td>
                                    <td style='padding: 10px; border: 1px solid #dee2e6;'>{step.StepName}</td>
                                </tr>
                            </table>
                            
                            <div style='text-align: center; margin: 30px 0;'>
                                <a href='{approvalUrl}' 
                                   style='background-color: #28a745; color: white; padding: 15px 30px; 
                                          text-decoration: none; border-radius: 8px; font-weight: bold; 
                                          display: inline-block; box-shadow: 0 2px 4px rgba(40,167,69,0.3);'>
                                    ✅ {GetBilingualResourceString("ViewAndApproveRequest")}
                                </a>
                            </div>
                        </div>
                        <div style='background: #6c757d; color: white; padding: 10px; text-align: center; font-size: 12px;'>
                            © Test Run System - Brother Industries Vietnam
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