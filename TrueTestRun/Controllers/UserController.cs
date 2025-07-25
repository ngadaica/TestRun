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
        private readonly FileStorageService _fs = new FileStorageService();

        public ActionResult Index()
        {
            var currentUser = Session["CurrentUser"] as User;
            if (currentUser == null)
                return RedirectToAction("Login", "Account");

            var users = _fs.LoadUsers();

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

            var users = _fs.LoadUsers();
            if (users.Any(u => u.ADID == model.ADID))
                ModelState.AddModelError("ADID", "ADID đã tồn tại");

            if (!ModelState.IsValid) return View(model);

            users.Add(model);
            _fs.SaveUsers(users);
            return RedirectToAction("Index");
        }

        public ActionResult Edit(string id)
        {
            var user = _fs.LoadUsers().FirstOrDefault(u => u.ADID == id);
            if (user == null) return HttpNotFound();
            return View(user);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Edit(User model)
        {
            if (!ModelState.IsValid) return View(model);

            var users = _fs.LoadUsers();
            var idx = users.FindIndex(u => u.ADID == model.ADID);
            if (idx < 0) return HttpNotFound();

            users[idx] = model;
            _fs.SaveUsers(users);
            return RedirectToAction("Index");
        }

        public ActionResult Delete(string id)
        {
            var users = _fs.LoadUsers()
                           .Where(u => u.ADID != id)
                           .ToList();
            _fs.SaveUsers(users);
            return RedirectToAction("Index");
        }
    }

}