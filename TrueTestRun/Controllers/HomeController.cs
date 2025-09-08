using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using TrueTestRun.ViewModels;
using TrueTestRun.Models;
using TrueTestRun.Services;

namespace TrueTestRun.Controllers
{
    public class HomeController : Controller
    {
        private readonly TrueTestRunDbContext _context = new TrueTestRunDbContext();

        // GET: Home
        public ActionResult Index()
        {
            // Automatically authenticate user based on Windows username
            var currentUser = EnsureAuthenticated();
            if (currentUser == null)
            {
                // If no matching user found
                ViewBag.ErrorMessage = "Không tìm thấy tài khoản tương ứng với tên đăng nhập Windows của bạn. Vui lòng liên hệ quản trị viên.";
                ViewBag.WindowsUser = User.Identity.Name.Split('\\').Last();
                return View("Index");
            }

            // Lấy tên user Windows của máy chủ
            ViewBag.WindowsUser = User.Identity.Name.Split('\\').Last();

            // Tìm user trong hệ thống có ADID trùng với username
            var fs = new FileStorageService();

            // Lấy toàn bộ request của hệ thống
            var allRequests = fs.LoadAllRequests();

            // Tính toán các số liệu thống kê
            var vm = new DashboardViewModel
            {
                TotalCount = allRequests.Count,
                CompletedCount = allRequests.Count(r => r.IsCompleted),
                PendingCount = allRequests.Count(r => !r.IsCompleted && !r.IsRejected), // Đang xử lý
                RejectedCount = allRequests.Count(r => r.IsRejected) // Bị từ chối (NG)
            };

            return View(vm);
        }

        /// <summary>
        /// Automatically authenticate user based on Windows username
        /// </summary>
        private User EnsureAuthenticated()
        {
            // Check if user is already authenticated in session
            var sessionUser = Session["CurrentUser"] as User;
            if (sessionUser != null)
            {
                return sessionUser;
            }

            // Get Windows username
            var windowsUsername = User.Identity.Name.Split('\\').Last();
            if (string.IsNullOrEmpty(windowsUsername))
            {
                return null;
            }

            // Find user in database with matching ADID
            var user = _context.Users
                          .FirstOrDefault(u => u.ADID.Equals(windowsUsername, StringComparison.OrdinalIgnoreCase));
            
            if (user == null)
            {
                return null;
            }

            // Create authentication ticket
            var ticket = new FormsAuthenticationTicket(
                version: 1,
                name: user.ADID,
                issueDate: DateTime.Now,
                expiration: DateTime.Now.AddHours(8),
                isPersistent: false,
                userData: user.ApprovalRole.ToString()
            );

            var encrypted = FormsAuthentication.Encrypt(ticket);
            Response.Cookies.Add(new HttpCookie(
                FormsAuthentication.FormsCookieName, encrypted));

            // Store user in session
            Session["CurrentUser"] = user;
            
            return user;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _context?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}