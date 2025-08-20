using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;
using TrueTestRun.Models;
using TrueTestRun.Services;

namespace TrueTestRun.Controllers
{
    [Authorize]
    public class UserController : Controller
    {
        private readonly TrueTestRunDbContext _context = new TrueTestRunDbContext();
        private readonly FileStorageService _fs = new FileStorageService();

        [HttpGet]
        public ActionResult Index()
        {
            var currentUser = Session["CurrentUser"] as User;
            if (currentUser == null)
                return RedirectToAction("Login", "Account");

            // Load users from database using Entity Framework
            var users = _context.Users.ToList();

            if (currentUser.Role == UserRole.Admin)
            {
                // Admin: show all users
                return View(users);
            }
            else
            {
                // Non-admin: show only their own account
                var singleUser = users.Where(u => u.ADID == currentUser.ADID).ToList();
                return View(singleUser);
            }
        }

        public ActionResult Create() => View(new User());

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Create(User model)
        {
            if (!ModelState.IsValid) return View(model);

            // Check if ADID already exists using Entity Framework
            if (_context.Users.Any(u => u.ADID == model.ADID))
                ModelState.AddModelError("ADID", "ADID đã tồn tại");

            if (!ModelState.IsValid) return View(model);

            // Add user to database using Entity Framework
            _context.Users.Add(model);
            _context.SaveChanges();
            
            return RedirectToAction("Index");
        }

        public ActionResult Edit(string id)
        {
            // Load user from database using Entity Framework
            var user = _context.Users.FirstOrDefault(u => u.ADID == id);
            if (user == null) return HttpNotFound();
            return View(user);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Edit(User model)
        {
            if (!ModelState.IsValid) return View(model);

            // Find and update user in database using Entity Framework
            var existingUser = _context.Users.FirstOrDefault(u => u.ADID == model.ADID);
            if (existingUser == null) return HttpNotFound();

            // Update user properties
            _context.Entry(existingUser).CurrentValues.SetValues(model);
            _context.SaveChanges();
            
            return RedirectToAction("Index");
        }

        public ActionResult Delete(string id)
        {
            // Find and remove user from database using Entity Framework
            var user = _context.Users.FirstOrDefault(u => u.ADID == id);
            if (user != null)
            {
                _context.Users.Remove(user);
                _context.SaveChanges();
            }
            
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _context?.Dispose();
                _fs?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}