using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Security;

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
    }
}
