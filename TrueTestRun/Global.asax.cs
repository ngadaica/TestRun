using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Security;
using TrueTestRun.Models; // THÊM
using WebApplication1;

namespace TrueTestRun
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }

        protected void Application_AuthenticateRequest(Object sender, EventArgs e)
        {
            // 1) Nếu đã có Forms cookie => dùng như cũ
            var authCookie = Request.Cookies[FormsAuthentication.FormsCookieName];
            if (authCookie != null)
            {
                var ticket = FormsAuthentication.Decrypt(authCookie.Value);
                if (ticket != null)
                {
                    var roles = new[] { ticket.UserData };
                    var id = new FormsIdentity(ticket);
                    HttpContext.Current.User =
                        new System.Security.Principal.GenericPrincipal(id, roles);
                }
                return;
            }

            // 2) KHÔNG có Forms cookie: tự động đăng nhập từ WindowsIdentity ngay tại đây
            try
            {
                var winUser = HttpContext.Current?.User;
                var winName = winUser?.Identity?.IsAuthenticated == true ? winUser.Identity.Name : null; // VD: DOMAIN\user
                if (!string.IsNullOrWhiteSpace(winName))
                {
                    var adid = winName.Contains("\\") ? winName.Split('\\').Last() : winName;

                    using (var db = new TrueTestRunDbContext())
                    {
                        var user = db.Users.FirstOrDefault(u => u.ADID.Equals(adid, StringComparison.OrdinalIgnoreCase));
                        if (user != null)
                        {
                            // Tạo Forms ticket NGAY ở AuthenticateRequest để danh tính thống nhất cho GET đầu tiên
                            var ticket = new FormsAuthenticationTicket(
                                1,
                                user.ADID,
                                DateTime.Now,
                                DateTime.Now.AddHours(8),
                                false,
                                user.ApprovalRole.ToString()
                            );

                            var encrypted = FormsAuthentication.Encrypt(ticket);
                            var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, encrypted)
                            {
                                HttpOnly = true,
                                Secure = Request.IsSecureConnection,
                                SameSite = SameSiteMode.Lax,
                                Path = FormsAuthentication.FormsCookiePath
                            };
                            Response.Cookies.Add(cookie);

                            // Set principal theo FormsIdentity (Name = ADID) để AntiForgery dùng đúng danh tính ngay từ GET đầu tiên
                            var id = new FormsIdentity(ticket);
                            var roles = new[] { ticket.UserData };
                            HttpContext.Current.User =
                                new System.Security.Principal.GenericPrincipal(id, roles);
                        }
                        else
                        {
                            // Không có user trong DB => để nguyên Windows principal (vẫn xem trang được nếu [AllowAnonymous])
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Global.asax] Auto-auth from Windows failed: {ex.Message}");
                // Không chặn request, chỉ log
            }
        }

        protected void Application_BeginRequest()
        {
            SetCultureFromCookie();
        }

        private void SetCultureFromCookie()
        {
            try
            {
                var languageCookie = Request.Cookies["UserLanguage"];
                string langCode = "vi"; // Default to Vietnamese

                if (languageCookie != null && !string.IsNullOrEmpty(languageCookie.Value))
                {
                    langCode = languageCookie.Value;
                }

                // Validate language code
                if (langCode != "vi" && langCode != "ja")
                    langCode = "vi";

                var cultureInfo = new CultureInfo(langCode == "ja" ? "ja-JP" : "vi-VN");
                Thread.CurrentThread.CurrentCulture = cultureInfo;
                Thread.CurrentThread.CurrentUICulture = cultureInfo;

                if (HttpContext.Current.Session != null)
                {
                    HttpContext.Current.Session["UserLanguage"] = langCode;
                }
            }
            catch (Exception)
            {
                var cultureInfo = new CultureInfo("vi-VN");
                Thread.CurrentThread.CurrentCulture = cultureInfo;
                Thread.CurrentThread.CurrentUICulture = cultureInfo;

                if (HttpContext.Current.Session != null)
                {
                    HttpContext.Current.Session["UserLanguage"] = "vi";
                }
            }
        }
    }
}