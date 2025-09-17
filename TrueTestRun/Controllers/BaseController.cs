using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using TrueTestRun.Models;

namespace TrueTestRun.Controllers
{
    public class BaseController : Controller
    {
        protected readonly TrueTestRunDbContext _context = new TrueTestRunDbContext();

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            try
            {
                // Skip authentication for anonymous actions
                if (filterContext.ActionDescriptor.GetCustomAttributes(typeof(AllowAnonymousAttribute), true).Any() ||
                    filterContext.ActionDescriptor.ControllerDescriptor.GetCustomAttributes(typeof(AllowAnonymousAttribute), true).Any())
                {
                    base.OnActionExecuting(filterContext);
                    return;
                }

                // SỬA: Ensure user authentication with multiple retry attempts
                var currentUser = EnsureAuthenticatedWithRetry();
                if (currentUser == null)
                {
                    System.Diagnostics.Debug.WriteLine("[BaseController] Authentication failed completely, redirecting to error page");

                    // Clear corrupted session/auth data
                    Session.Clear();
                    Session.Abandon();
                    Response.Cookies.Add(new HttpCookie(FormsAuthentication.FormsCookieName, "")
                    {
                        Expires = DateTime.Now.AddDays(-1)
                    });

                    // Set error message for user
                    TempData["ErrorMessage"] = "Phiên đăng nhập đã hết hạn hoặc không hợp lệ. Vui lòng đóng trình duyệt và thử lại từ link email mới.";

                    filterContext.Result = RedirectToAction("Index", "Home");
                    return;
                }

                // SỬA: Store user info in ViewBag for all actions
                ViewBag.CurrentUser = currentUser;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BaseController] Critical error in OnActionExecuting: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[BaseController] Stack trace: {ex.StackTrace}");

                // Fallback error handling
                TempData["ErrorMessage"] = "Có lỗi hệ thống xảy ra. Vui lòng thử lại sau.";
                filterContext.Result = RedirectToAction("Index", "Home");
                return;
            }

            base.OnActionExecuting(filterContext);
        }

        /// <summary>
        /// SỬA: Enhanced authentication with retry logic
        /// </summary>
        private User EnsureAuthenticatedWithRetry()
        {
            const int maxRetries = 3;
            const int delayMs = 200;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[BaseController] Authentication attempt {attempt}/{maxRetries}");

                    var user = EnsureAuthenticated();
                    if (user != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[BaseController] Authentication successful on attempt {attempt}");
                        return user;
                    }

                    if (attempt < maxRetries)
                    {
                        System.Diagnostics.Debug.WriteLine($"[BaseController] Authentication failed on attempt {attempt}, retrying...");
                        System.Threading.Thread.Sleep(delayMs * attempt); // Progressive delay
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[BaseController] Authentication attempt {attempt} threw exception: {ex.Message}");
                    if (attempt == maxRetries)
                    {
                        throw; // Re-throw on final attempt
                    }
                    System.Threading.Thread.Sleep(delayMs * attempt);
                }
            }

            System.Diagnostics.Debug.WriteLine($"[BaseController] All authentication attempts failed");
            return null;
        }

        /// <summary>
        /// Automatically authenticate user based on Windows username - IMPROVED
        /// </summary>
        protected User EnsureAuthenticated()
        {
            try
            {
                // Check if user is already authenticated in session
                var sessionUser = Session["CurrentUser"] as User;
                if (sessionUser != null)
                {
                    // SỬA: Verify session user still exists in database
                    try
                    {
                        var dbUser = _context.Users.FirstOrDefault(u => u.ADID == sessionUser.ADID);
                        if (dbUser != null)
                        {
                            // Update session with latest DB data
                            Session["CurrentUser"] = dbUser;
                            return dbUser;
                        }
                        else
                        {
                            // User no longer exists in DB, clear session
                            Session["CurrentUser"] = null;
                        }
                    }
                    catch (Exception dbEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[BaseController] DB verification failed: {dbEx.Message}");
                        // Continue to Windows auth fallback
                    }
                }

                // SỬA: Improved Windows username extraction with better error handling
                var windowsUsername = GetWindowsUsername();
                if (string.IsNullOrEmpty(windowsUsername))
                {
                    System.Diagnostics.Debug.WriteLine("[BaseController] Failed to get Windows username");
                    return null;
                }

                System.Diagnostics.Debug.WriteLine($"[BaseController] Attempting authentication for: {windowsUsername}");

                // Find user in database with matching ADID
                User user = null;
                try
                {
                    user = _context.Users
                              .FirstOrDefault(u => u.ADID.Equals(windowsUsername, StringComparison.OrdinalIgnoreCase));
                }
                catch (Exception dbEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[BaseController] Database query failed: {dbEx.Message}");
                    throw new InvalidOperationException("Database connection error during authentication", dbEx);
                }

                if (user == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[BaseController] User not found in database: {windowsUsername}");
                    return null;
                }

                // SỬA: Improved authentication ticket creation with better error handling
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

                    // SỬA: Better cookie handling with error recovery
                    try
                    {
                        var authCookie = new HttpCookie(FormsAuthentication.FormsCookieName, encrypted)
                        {
                            HttpOnly = true,
                            Secure = Request.IsSecureConnection,
                            SameSite = SameSiteMode.Lax,
                            Path = FormsAuthentication.FormsCookiePath
                        };

                        Response.Cookies.Add(authCookie);
                    }
                    catch (Exception cookieEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[BaseController] Cookie creation error (non-fatal): {cookieEx.Message}");
                        // Continue - session auth should still work
                    }
                }
                catch (Exception ticketEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[BaseController] Forms authentication ticket error: {ticketEx.Message}");
                    // Continue - we can still use session-only auth
                }

                // Store user in session with additional validation
                try
                {
                    Session["CurrentUser"] = user;
                    Session["AuthTimestamp"] = DateTime.Now;
                    Session["WindowsUser"] = windowsUsername;
                }
                catch (Exception sessionEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[BaseController] Session storage error: {sessionEx.Message}");
                    throw new InvalidOperationException("Session storage failed during authentication", sessionEx);
                }

                System.Diagnostics.Debug.WriteLine($"[BaseController] Successfully authenticated: {user.Name} ({user.ADID})");
                return user;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BaseController] Authentication error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[BaseController] Stack trace: {ex.StackTrace}");

                // SỬA: Don't rethrow here, let the retry mechanism handle it
                return null;
            }
        }

        /// <summary>
        /// SỬA: Improved Windows username extraction with multiple fallback methods
        /// </summary>
        private string GetWindowsUsername()
        {
            try
            {
                string username = null;

                // Method 1: Controller User property (preferred in MVC)
                try
                {
                    if (User?.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(User.Identity.Name))
                    {
                        username = User.Identity.Name;
                        System.Diagnostics.Debug.WriteLine($"[BaseController] Got username from User.Identity: {username}");
                    }
                }
                catch (Exception ex1)
                {
                    System.Diagnostics.Debug.WriteLine($"[BaseController] User.Identity method failed: {ex1.Message}");
                }

                // Method 2: HttpContext.User (MVC context)
                try
                {
                    if (string.IsNullOrEmpty(username) && HttpContext?.User?.Identity?.IsAuthenticated == true)
                    {
                        username = HttpContext.User.Identity.Name;
                        System.Diagnostics.Debug.WriteLine($"[BaseController] Got username from HttpContext.User: {username}");
                    }
                }
                catch (Exception ex2)
                {
                    System.Diagnostics.Debug.WriteLine($"[BaseController] HttpContext.User method failed: {ex2.Message}");
                }

                // Method 3: Request.ServerVariables (Windows Auth specific)
                try
                {
                    if (string.IsNullOrEmpty(username))
                    {
                        username = Request.ServerVariables["AUTH_USER"] ?? Request.ServerVariables["LOGON_USER"];
                        if (!string.IsNullOrEmpty(username))
                        {
                            System.Diagnostics.Debug.WriteLine($"[BaseController] Got username from ServerVariables: {username}");
                        }
                    }
                }
                catch (Exception ex3)
                {
                    System.Diagnostics.Debug.WriteLine($"[BaseController] ServerVariables method failed: {ex3.Message}");
                }

                // Method 4: Environment username (fallback - least reliable)
                try
                {
                    if (string.IsNullOrEmpty(username))
                    {
                        var envUser = Environment.UserName;
                        if (!string.IsNullOrEmpty(envUser))
                        {
                            username = Environment.UserDomainName + "\\" + envUser;
                            System.Diagnostics.Debug.WriteLine($"[BaseController] Got username from Environment: {username}");
                        }
                    }
                }
                catch (Exception ex4)
                {
                    System.Diagnostics.Debug.WriteLine($"[BaseController] Environment method failed: {ex4.Message}");
                }

                if (string.IsNullOrEmpty(username))
                {
                    System.Diagnostics.Debug.WriteLine("[BaseController] All username extraction methods failed");
                    return null;
                }

                // Extract username from domain\user format
                var parts = username.Split('\\');
                var extractedUsername = parts.Length > 1 ? parts.Last() : username;

                // SỬA: Validate extracted username
                if (string.IsNullOrWhiteSpace(extractedUsername))
                {
                    System.Diagnostics.Debug.WriteLine($"[BaseController] Extracted username is empty from: {username}");
                    return null;
                }

                System.Diagnostics.Debug.WriteLine($"[BaseController] Final extracted username: {extractedUsername} from: {username}");
                return extractedUsername.Trim();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BaseController] Error extracting Windows username: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// SỬA: Helper method to get current authenticated user safely
        /// </summary>
        protected User GetCurrentUser()
        {
            try
            {
                var user = Session["CurrentUser"] as User;
                if (user == null)
                {
                    // Try to re-authenticate
                    user = EnsureAuthenticated();
                }
                return user;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BaseController] GetCurrentUser error: {ex.Message}");
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