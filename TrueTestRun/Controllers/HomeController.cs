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
            // SỬA: Improved authentication with retry mechanism
            var currentUser = EnsureAuthenticated();
            if (currentUser == null)
            {
                // SỬA: Try authentication once more before showing error
                System.Threading.Thread.Sleep(100);
                currentUser = EnsureAuthenticated();

                if (currentUser == null)
                {
                    // If no matching user found
                    ViewBag.ErrorMessage = "Không tìm thấy tài khoản tương ứng với tên đăng nhập Windows của bạn. Vui lòng liên hệ quản trị viên.";
                    ViewBag.WindowsUser = GetWindowsUsername() ?? "(unknown)";
                    return View("Index");
                }
            }

            // Lấy tên user Windows an toàn
            ViewBag.WindowsUser = GetWindowsUsername() ?? "(unknown)";

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
        /// SỬA: Unified authentication method consistent with BaseController
        /// </summary>
        private User EnsureAuthenticated()
        {
            try
            {
                // Check if user is already authenticated in session
                var sessionUser = Session["CurrentUser"] as User;
                if (sessionUser != null)
                {
                    return sessionUser;
                }

                // Get Windows username safely
                var windowsUsername = GetWindowsUsername();
                if (string.IsNullOrEmpty(windowsUsername))
                {
                    System.Diagnostics.Debug.WriteLine("[HomeController] Failed to get Windows username");
                    return null;
                }

                System.Diagnostics.Debug.WriteLine($"[HomeController] Attempting authentication for: {windowsUsername}");

                // Find user in database with matching ADID
                var user = _context.Users
                              .FirstOrDefault(u => u.ADID.Equals(windowsUsername, StringComparison.OrdinalIgnoreCase));

                if (user == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[HomeController] User not found in database: {windowsUsername}");
                    return null;
                }

                // SỬA: Improved authentication ticket creation
                try
                {
                    var ticket = new FormsAuthenticationTicket(
                        version: 1,
                        name: user.ADID,
                        issueDate: DateTime.Now,
                        expiration: DateTime.Now.AddHours(8),
                        isPersistent: false,
                        userData: user.ApprovalRole.ToString()
                    );

                    var encrypted = FormsAuthentication.Encrypt(ticket);

                    // SỬA: Better cookie handling
                    var authCookie = new HttpCookie(FormsAuthentication.FormsCookieName, encrypted)
                    {
                        HttpOnly = true,
                        Secure = Request.IsSecureConnection,
                        SameSite = SameSiteMode.Lax
                    };

                    Response.Cookies.Add(authCookie);
                }
                catch (Exception cookieEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[HomeController] Cookie creation error: {cookieEx.Message}");
                    // Continue without cookie - session auth should still work
                }

                // Store in session
                Session["CurrentUser"] = user;
                System.Diagnostics.Debug.WriteLine($"[HomeController] Successfully authenticated: {user.Name} ({user.ADID})");

                return user;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HomeController] Authentication error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// SỬA: Improved Windows username extraction
        /// </summary>
        private string GetWindowsUsername()
        {
            try
            {
                string username = null;

                // Method 1: Controller User property (preferred in MVC)
                if (User?.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(User.Identity.Name))
                {
                    username = User.Identity.Name;
                }

                // Method 2: HttpContext.User (MVC context, not HttpContext.Current)
                if (string.IsNullOrEmpty(username) && HttpContext?.User?.Identity?.IsAuthenticated == true)
                {
                    username = HttpContext.User.Identity.Name;
                }

                // Method 3: Environment username (fallback)
                if (string.IsNullOrEmpty(username))
                {
                    username = Environment.UserName;
                    if (!string.IsNullOrEmpty(username))
                    {
                        username = Environment.UserDomainName + "\\" + username;
                    }
                }

                if (string.IsNullOrEmpty(username))
                {
                    System.Diagnostics.Debug.WriteLine("[HomeController] All username extraction methods failed");
                    return null;
                }

                // Extract username from domain\user format
                var parts = username.Split('\\');
                var extractedUsername = parts.Length > 1 ? parts.Last() : username;

                System.Diagnostics.Debug.WriteLine($"[HomeController] Extracted username: {extractedUsername} from: {username}");
                return extractedUsername;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HomeController] Error extracting Windows username: {ex.Message}");
                return null;
            }
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