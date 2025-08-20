using System;
using System.Globalization;
using System.Threading;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Security;
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
            var authCookie = Request.Cookies[FormsAuthentication.FormsCookieName];
            if (authCookie == null) return;

            var ticket = FormsAuthentication.Decrypt(authCookie.Value);
            if (ticket == null) return;

            // ticket.UserData chính là "Reviewer" hoặc "Stampler"
            var roles = new[] { ticket.UserData };
            var id = new FormsIdentity(ticket);
            HttpContext.Current.User =
                new System.Security.Principal.GenericPrincipal(id, roles);
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

                // Store in session for easy access in views
                if (HttpContext.Current.Session != null)
                {
                    HttpContext.Current.Session["UserLanguage"] = langCode;
                }
            }
            catch (Exception)
            {
                // Fallback to Vietnamese if anything goes wrong
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
