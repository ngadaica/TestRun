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

        public ActionResult Create()
        {
            // SỬA: Chỉ Admin mới có thể tạo user mới
            var currentUser = Session["CurrentUser"] as User;
            if (currentUser == null)
                return RedirectToAction("Login", "Account");

            if (currentUser.Role != UserRole.Admin)
            {
                TempData["ErrorMessage"] = "Chỉ Admin mới có quyền tạo tài khoản mới/管理者のみが新しいアカウントを作成できます";
                return RedirectToAction("Index");
            }

            return View(new User());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Create(User model)
        {
            // SỬA: Kiểm tra quyền Admin
            var currentUser = Session["CurrentUser"] as User;
            if (currentUser == null)
                return RedirectToAction("Login", "Account");

            if (currentUser.Role != UserRole.Admin)
            {
                TempData["ErrorMessage"] = "Chỉ Admin mới có quyền tạo tài khoản mới/管理者のみが新しいアカウントを作成できます";
                return RedirectToAction("Index");
            }

            if (!ModelState.IsValid) return View(model);

            // Check if ADID already exists using Entity Framework
            if (_context.Users.Any(u => u.ADID == model.ADID))
                ModelState.AddModelError("ADID", "ADID đã tồn tại");

            if (!ModelState.IsValid) return View(model);

            // Add user to database using Entity Framework
            _context.Users.Add(model);
            _context.SaveChanges();
            
            TempData["SuccessMessage"] = $"Đã tạo thành công tài khoản/アカウント情報が正常に更新されました {model.ADID}!";
            return RedirectToAction("Index");
        }

        public ActionResult Edit(string id)
        {
            // SỬA: Kiểm tra quyền chỉnh sửa
            var currentUser = Session["CurrentUser"] as User;
            if (currentUser == null)
                return RedirectToAction("Login", "Account");

            // Admin có thể edit bất kỳ user nào, user thường chỉ có thể edit chính mình
            if (currentUser.Role != UserRole.Admin && currentUser.ADID != id)
            {
                TempData["ErrorMessage"] = "Bạn chỉ có thể chỉnh sửa thông tin của chính mình/自分の情報しか編集できません";
                return RedirectToAction("Index");
            }

            // Load user from database using Entity Framework
            var user = _context.Users.FirstOrDefault(u => u.ADID == id);
            if (user == null) return HttpNotFound();
            
            return View(user);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Edit(User model)
        {
            // SỬA: Kiểm tra quyền chỉnh sửa
            var currentUser = Session["CurrentUser"] as User;
            if (currentUser == null)
                return RedirectToAction("Login", "Account");

            // Admin có thể edit bất kỳ user nào, user thường chỉ có thể edit chính mình
            if (currentUser.Role != UserRole.Admin && currentUser.ADID != model.ADID)
            {
                TempData["ErrorMessage"] = "Bạn chỉ có thể chỉnh sửa thông tin của chính mình/自分の情報しか編集できません";
                return RedirectToAction("Index");
            }

            if (!ModelState.IsValid) return View(model);

            // Find and update user in database using Entity Framework
            var existingUser = _context.Users.FirstOrDefault(u => u.ADID == model.ADID);
            if (existingUser == null) return HttpNotFound();

            // SỬA: Nếu không phải Admin, không cho phép thay đổi Role
            if (currentUser.Role != UserRole.Admin)
            {
                // Giữ nguyên Role cũ nếu user không phải Admin
                model.Role = existingUser.Role;
            }

            // Update user properties
            _context.Entry(existingUser).CurrentValues.SetValues(model);
            _context.SaveChanges();
            
            TempData["SuccessMessage"] = $"Đã cập nhật thành công thông tin tài khoản/アカウント情報が正常に更新されました {model.ADID}!";
            return RedirectToAction("Index");
        }

        public ActionResult Delete(string id)
        {
            // SỬA: CHỈ ADMIN MỚI CÓ QUYỀN XÓA USER
            var currentUser = Session["CurrentUser"] as User;
            if (currentUser == null)
            {
                TempData["ErrorMessage"] = "Vui lòng đăng nhập!";
                return RedirectToAction("Login", "Account");
            }

            if (currentUser.Role != UserRole.Admin)
            {
                TempData["ErrorMessage"] = "Chỉ Admin mới có quyền xóa tài khoản/ 管理者のみがアカウントを削除する権利を持っています";
                return RedirectToAction("Index");
            }

            // Find and remove user from database using Entity Framework
            var user = _context.Users.FirstOrDefault(u => u.ADID == id);
            if (user != null)
            {
                _context.Users.Remove(user);
                _context.SaveChanges();
                TempData["SuccessMessage"] = $"Đã xóa thành công tài khoản/アカウントが正常に削除されました {user.Name} ({user.ADID})!";
            }
            else
            {
                TempData["ErrorMessage"] = "Không tìm thấy tài khoản để xóa/削除するアカウントが見つかりません";
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