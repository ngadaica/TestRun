using System;
using System.Globalization;
using System.Threading;
using System.Web;
using System.Web.Mvc;

namespace TrueTestRun.Controllers
{
    public class LanguageController : Controller
    {
        public ActionResult ChangeLanguage(string langCode, string returnUrl)
        {
            if (string.IsNullOrEmpty(langCode))
                langCode = "vi";

            // Validate language code
            if (langCode != "vi" && langCode != "ja")
                langCode = "vi";

            // Set cookie for language preference
            var cookie = new HttpCookie("UserLanguage", langCode)
            {
                Expires = DateTime.Now.AddYears(1),
                HttpOnly = true,
                Secure = Request.IsSecureConnection
            };
            Response.Cookies.Add(cookie);

            // Set current culture
            SetLanguage(langCode);

            // Redirect back to the page user came from
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        private void SetLanguage(string langCode)
        {
            try
            {
                var cultureInfo = new CultureInfo(langCode == "ja" ? "ja-JP" : "vi-VN");
                Thread.CurrentThread.CurrentCulture = cultureInfo;
                Thread.CurrentThread.CurrentUICulture = cultureInfo;

                // Store in session as well
                Session["UserLanguage"] = langCode;
            }
            catch (Exception)
            {
                // Fallback to Vietnamese if culture setting fails
                var cultureInfo = new CultureInfo("vi-VN");
                Thread.CurrentThread.CurrentCulture = cultureInfo;
                Thread.CurrentThread.CurrentUICulture = cultureInfo;
                Session["UserLanguage"] = "vi";
            }
        }
    }
}