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
        private readonly TrueTestRunDbContext _context = new TrueTestRunDbContext();

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            // Skip authentication for anonymous actions
            if (filterContext.ActionDescriptor.GetCustomAttributes(typeof(AllowAnonymousAttribute), true).Any() ||
                filterContext.ActionDescriptor.ControllerDescriptor.GetCustomAttributes(typeof(AllowAnonymousAttribute), true).Any())
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            // Ensure user is authenticated
            var currentUser = EnsureAuthenticated();
            if (currentUser == null)
            {
                // Redirect to no access page
                filterContext.Result = RedirectToAction("NoAccess", "Home");
                return;
            }

            base.OnActionExecuting(filterContext);
        }

        /// <summary>
        /// Automatically authenticate user based on Windows username
        /// </summary>
        protected User EnsureAuthenticated()
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