using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using TrueTestRun.Models;
using TrueTestRun.Services;

namespace TrueTestRun.Controllers
{
    public class AccountController : Controller
    {
        private readonly FileStorageService _fs = new FileStorageService();
        private readonly ImageService _imageService = new ImageService();

        // Helper đổ danh sách cho dropdown
        private void PopulateRegisterDrops()
        {
            ViewBag.DeptCodes = new SelectList(new[] { "EPE-EE", "EPE-PCB","EPE-G.M", });
            ViewBag.Titles = new SelectList(new[] { "Staff", "Quản lý trung cấp", "Quản lý sơ cấp", "G.M" });
            ViewBag.Factories = new SelectList(new[] { "F1", "F2", "F3", "F4", "F5" });

            // Tạo danh sách role với text tiếng Việt
            var roleList = new[]
            {
                new { Value = UserRole.Admin.ToString(), Text = "Admin" },
                new { Value = UserRole.DataEntry.ToString(), Text = "Người ghi nhập" },
                new { Value = UserRole.Approver.ToString(), Text = "Người phê duyệt" }  
            };
            ViewBag.Roles = new SelectList(roleList, "Value", "Text");
        }

        [AllowAnonymous]
        public ActionResult Register()
        {
            PopulateRegisterDrops();
            return View(new User());
        }

        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public ActionResult Register(User model)
        {
            if (!ModelState.IsValid)
            {
                PopulateRegisterDrops();
                return View(model);
            }

            var users = _fs.LoadUsers();
            if (users.Any(u => u.ADID.Equals(model.ADID, StringComparison.OrdinalIgnoreCase)))
            {
                ModelState.AddModelError("ADID", "ADID đã tồn tại");
                PopulateRegisterDrops();
                return View(model);
            }

            users.Add(model);
            _fs.SaveUsers(users);
            return RedirectToAction("Login");
        }

        [AllowAnonymous]
        public ActionResult Login()
        {
            return View();
        }

        [HttpPost, AllowAnonymous]
        public ActionResult Login(string adid)
        {
            var user = _fs.LoadUsers()
                          .FirstOrDefault(u =>
                              u.ADID.Equals(adid,
                                  StringComparison.OrdinalIgnoreCase));
            if (user == null)
            {
                ModelState.AddModelError("", "ADID không tồn tại");
                return View();
            }

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

            Session["CurrentUser"] = user;
            return RedirectToAction("Index", "Home");
        }

        [Authorize]
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            Session.Clear();
            return RedirectToAction("Login");
        }

        // ACTION MỚI: TẠO ẢNH CON DẤU
        [AllowAnonymous] // Cho phép truy cập mà không cần đăng nhập
        public ActionResult UserSeal(string department, string name)
        {
            // Lấy user theo tên và phòng ban (nếu cần)
            var user = _fs.LoadUsers().FirstOrDefault(u => u.Name == name && u.DeptCode == department);
            var role = user?.Role ?? UserRole.DataEntry;

            var imageData = _imageService.GetOrCreateSealImage(department, name, role);
            if (imageData == null)
            {
                // Trả về ảnh rỗng hoặc 404
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.NoContent);
            }
            return File(imageData, "image/png");
        }
    }
}