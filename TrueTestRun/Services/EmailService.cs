using TrueTestRun.Models;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Mail;
using System.Net;
using System;
using System.Linq;

namespace TrueTestRun.Services
{
    public class EmailService
    {
        /// <summary>
        /// Lấy gmail theo DeptCode + Role từng bước (cứng, không lấy từ user đăng ký)
        /// </summary>
        private string GetApproverEmail(string deptCode, string role)
        {
            // Thay đổi các địa chỉ email sau cho đúng với đơn vị/phòng ban của bạn
            if (deptCode == "EPE-EE" && role == "Quản lý trung cấp")
                return "Doan.PhamCong@brother-bivn.com.vn";
            if (deptCode == "EPE-PCB" && role == "Quản lý trung cấp")
                return "quanly.epe.pcb@gmail.com";
            if (deptCode == "EPE-PCB" && role == "Staff")
                return "Ly.NguyenThi@brother-bivn.com.vn";
            if (deptCode == "EPE-EE" && role == "Staff")
                return "Hoangthi.Minh@brother-bivn.com.vn";
            if (deptCode == "EPE-G.M" && role == "G.M")
                return "gm.epe@gmail.com";
            if (deptCode == "EPE-PCB" && role == "Quản ")
                return "gm.epe@gmail.com";
            // Các bước đặc biệt, thêm cho đủ các bước nếu cần
            // if (deptCode == ... && role == ...) return "...";
            return "admin.backup@gmail.com"; // fallback
        }

        /// <summary>
        /// Gửi email phê duyệt cho người nhận theo workflow (gmail cứng)
        /// </summary>
        public void SendApprovalRequest(Request request, WorkflowStep step, string approvalUrl, bool isResubmission = false)
        {
            var toEmail = GetApproverEmail(step.DeptCode, step.Role);

            var host = ConfigurationManager.AppSettings["SmtpHost"];
            var port = int.Parse(ConfigurationManager.AppSettings["SmtpPort"]);
            var user = ConfigurationManager.AppSettings["SmtpUser"];
            var pass = ConfigurationManager.AppSettings["SmtpPass"];

            var fromAddress = new MailAddress(user, "Test Run System");
            var toAddress = new MailAddress(toEmail);

            string subject = isResubmission
                ? $"[YÊU CẦU PHÊ DUYỆT LẠI] Đơn Test Run: {request.RequestID}"
                : $"[YÊU CẦU PHÊ DUYỆT] Đơn Test Run: {request.RequestID}";

            string body = isResubmission
                ? $@"
                <p>Chào bạn,</p>
                <p>Đơn test run <b>{request.RequestID}</b> đã được sửa lại sau khi bị từ chối.</p>
                <ul>
                    <li><strong>Mã đơn:</strong> {request.RequestID}</li>
                    <li><strong>Người tạo:</strong> {request.CreatedByADID}</li>
                    <li><strong>Bước duyệt:</strong> {step.DeptCode} - {step.Role}</li>
                </ul>
                <p>Vui lòng nhấn vào link dưới đây để xác nhận và phê duyệt lại:</p>
                <a href='{approvalUrl}'>Xem và phê duyệt lại đơn</a>
                <p>Cảm ơn bạn.</p>"
                : $@"
                <p>Chào bạn,</p>
                <p>Bạn có một đơn test run mới cần được phê duyệt.</p>
                <ul>
                    <li><strong>Mã đơn:</strong> {request.RequestID}</li>
                    <li><strong>Người tạo:</strong> {request.CreatedByADID}</li>
                    <li><strong>Bước duyệt:</strong> {step.DeptCode} - {step.Role}</li>
                </ul>
                <p>Vui lòng nhấn vào link dưới đây để xem chi tiết và phê duyệt:</p>
                <a href='{approvalUrl}'>Xem và phê duyệt đơn</a>
                <p>Cảm ơn bạn.</p>";

            var smtp = new SmtpClient
            {
                Host = host,
                Port = port,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(fromAddress.Address, pass)
            };

            using (var message = new MailMessage(fromAddress, toAddress)
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            })
            {
                try
                {
                    smtp.Send(message);
                    System.Diagnostics.Debug.WriteLine($"Đã gửi email phê duyệt cho: {toEmail}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Lỗi gửi email: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Gửi thông báo từ chối đến người tạo đơn (lấy email theo ADID của creator, vẫn giữ như cũ)
        /// </summary>
        public void SendRejectNotification(Request request, User rejector, string comment)
        {
            // Lấy email của người tạo request từ database users
            var fs = new TrueTestRun.Services.FileStorageService();
            var users = fs.LoadUsers();
            var creator = users.FirstOrDefault(u => u.ADID == request.CreatedByADID);

            var toEmail = creator?.Email ?? "admin.backup@gmail.com";

            var host = ConfigurationManager.AppSettings["SmtpHost"];
            var port = int.Parse(ConfigurationManager.AppSettings["SmtpPort"]);
            var user = ConfigurationManager.AppSettings["SmtpUser"];
            var pass = ConfigurationManager.AppSettings["SmtpPass"];

            var fromAddress = new MailAddress(user, "Test Run System");
            var toAddress = new MailAddress(toEmail);

            string subject = $"[TỪ CHỐI] Đơn Test Run: {request.RequestID}";
            string body = $@"
                <p>Đơn test run <b>{request.RequestID}</b> đã bị từ chối bởi {rejector.Name} ({rejector.DeptCode}).</p>
                <p><b>Lý do/Ghi chú:</b> {comment}</p>
                <p>Vui lòng kiểm tra lại thông tin và liên hệ nếu cần.</p>";

            var smtp = new SmtpClient
            {
                Host = host,
                Port = port,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(fromAddress.Address, pass)
            };

            using (var message = new MailMessage(fromAddress, toAddress)
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            })
            {
                try
                {
                    smtp.Send(message);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Lỗi gửi email từ chối: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Gửi thông báo từ chối về cho người phê duyệt trước đó (nếu có)
        /// </summary>
        public void SendRejectNotificationToPrevApprover(Request request, User prevUser, User rejector, string comment, string editUrl)
        {
            var host = ConfigurationManager.AppSettings["SmtpHost"];
            var port = int.Parse(ConfigurationManager.AppSettings["SmtpPort"]);
            var user = ConfigurationManager.AppSettings["SmtpUser"];
            var pass = ConfigurationManager.AppSettings["SmtpPass"];

            var fromAddress = new MailAddress(user, "Test Run System");
            var toAddress = new MailAddress(prevUser.Email);

            string subject = $"[BỊ TỪ CHỐI] Đơn Test Run: {request.RequestID}";
            string body = $@"
                <p>Đơn test run <b>{request.RequestID}</b> đã bị từ chối bởi {rejector.Name} ({rejector.DeptCode}).</p>
                <p><b>Lý do/Ghi chú:</b> {comment}</p>
                <p>Vui lòng kiểm tra lại và chỉnh sửa thông tin đơn.</p>
                <p><a href='{editUrl}'>Xem và chỉnh sửa/phê duyệt lại đơn</a></p>";

            var smtp = new SmtpClient
            {
                Host = host,
                Port = port,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(fromAddress.Address, pass)
            };

            using (var message = new MailMessage(fromAddress, toAddress)
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            })
            {
                try
                {
                    smtp.Send(message);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Lỗi gửi email từ chối: " + ex.Message);
                }
            }
        }
    }
}