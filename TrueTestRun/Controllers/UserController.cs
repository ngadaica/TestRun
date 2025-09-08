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
        private TrueTestRunDbContext _context;

        public UserController()
        {
            _context = new TrueTestRunDbContext();
        }

        /// <summary>
        /// Helper method để lấy resource string theo ngôn ngữ hiện tại
        /// </summary>
        private string GetResourceString(string key)
        {
            try
            {
                return HttpContext.GetGlobalResourceObject("Resources", key)?.ToString() ?? key;
            }
            catch
            {
                return key;
            }
        }

        // Helper đổ danh sách cho dropdown
        private void PopulateEditDropdowns()
        {
            ViewBag.DeptCodes = new SelectList(new[] { "EPE-EE", "EPE-PCB", "EPE-G.M" });
            ViewBag.Titles = new SelectList(new[] { "Staff", "Quản lý trung cấp", "Quản lý sơ cấp", "G.M" });
            ViewBag.Factories = new SelectList(new[] { "F1", "F2", "F3", "F4", "F5" });

            // SỬA: Tạo danh sách role với text rõ ràng hơn và giá trị số
            var roleList = new[]
            {
                new { Value = ((int)UserRole.Admin).ToString(), Text = "Admin (Quản trị viên)" },
                new { Value = ((int)UserRole.DataEntry).ToString(), Text = "DataEntry (Người nhập liệu)" },
                new { Value = ((int)UserRole.Approver).ToString(), Text = "Approver (Người phê duyệt)" }
            };
            ViewBag.Roles = new SelectList(roleList, "Value", "Text");
        }

        /// <summary>
        /// THÊM: Helper method để kiểm tra user có phải Admin không - dựa trên cả session và database
        /// </summary>
        private bool IsUserAdmin(User sessionUser)
        {
            if (sessionUser == null) return false;

            // Kiểm tra role trong session trước
            if (sessionUser.Role == UserRole.Admin) return true;

            // Nếu session không có thông tin Admin, kiểm tra lại database
            try
            {
                var dbUser = _context.Users.FirstOrDefault(u => u.ADID == sessionUser.ADID);
                if (dbUser != null && dbUser.Role == UserRole.Admin)
                {
                    // Cập nhật session với thông tin từ database
                    sessionUser.Role = UserRole.Admin;
                    Session["CurrentUser"] = sessionUser;
                    System.Diagnostics.Debug.WriteLine($"[UserController] Updated session role for {sessionUser.ADID} to Admin from database");
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UserController] Error checking user role in DB: {ex.Message}");
            }

            return false;
        }

        [HttpGet]
        public ActionResult Index()
        {
            try
            {
                var currentUser = Session["CurrentUser"] as User;
                if (currentUser == null)
                    return RedirectToAction("Login", "Account");

                // SỬA: Sử dụng helper method để kiểm tra admin
                bool isAdmin = IsUserAdmin(currentUser);
                System.Diagnostics.Debug.WriteLine($"[UserController.Index] User {currentUser.ADID} isAdmin: {isAdmin}, Role: {currentUser.Role} (value: {(int)currentUser.Role})");

                // SỬA: Thêm null check và proper error handling cho database
                List<User> users = new List<User>();

                try
                {
                    // Test database connection first
                    if (_context.Database.Connection.State != System.Data.ConnectionState.Open)
                    {
                        _context.Database.Connection.Open();
                    }

                    // Load users from database using Entity Framework with explicit error handling
                    users = _context.Users.ToList();

                    System.Diagnostics.Debug.WriteLine($"[UserController.Index] Loaded {users.Count} users from database");
                }
                catch (System.Data.Entity.Core.EntityException entityEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[UserController.Index] Entity Error: {entityEx.Message}");
                    TempData["ErrorMessage"] = "Không thể kết nối đến cơ sở dữ liệu. Vui lòng kiểm tra kết nối database.";
                    return RedirectToAction("Index", "Home");
                }
                catch (System.Data.SqlClient.SqlException sqlEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[UserController.Index] SQL Error: {sqlEx.Message}");
                    TempData["ErrorMessage"] = $"Lỗi SQL Database: {sqlEx.Message}. Vui lòng liên hệ administrator.";
                    return RedirectToAction("Index", "Home");
                }
                catch (System.InvalidOperationException invalidOpEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[UserController.Index] Invalid Operation: {invalidOpEx.Message}");
                    TempData["ErrorMessage"] = "Cấu hình database không hợp lệ. Vui lòng kiểm tra connection string.";
                    return RedirectToAction("Index", "Home");
                }

                if (isAdmin)
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
            catch (Exception ex)
            {
                // Log lỗi để debug
                System.Diagnostics.Debug.WriteLine($"[UserController.Index] General Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[UserController.Index] Stack trace: {ex.StackTrace}");

                // SỬA: Provide more specific error message
                string errorMessage = "Có lỗi xảy ra khi tải danh sách người dùng.";

                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[UserController.Index] Inner exception: {ex.InnerException.Message}");
                    errorMessage += $" Chi tiết: {ex.InnerException.Message}";
                }
                else
                {
                    errorMessage += $" Chi tiết: {ex.Message}";
                }

                TempData["ErrorMessage"] = errorMessage;
                return RedirectToAction("Index", "Home");
            }
            finally
            {
                // SỬA: Ensure connection is properly closed
                try
                {
                    if (_context?.Database?.Connection?.State == System.Data.ConnectionState.Open)
                    {
                        _context.Database.Connection.Close();
                    }
                }
                catch (Exception closeEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[UserController.Index] Error closing connection: {closeEx.Message}");
                }
            }
        }

        public ActionResult Create()
        {
            try
            {
                // SỬA: Sử dụng helper method để kiểm tra admin
                var currentUser = Session["CurrentUser"] as User;
                if (currentUser == null)
                    return RedirectToAction("Login", "Account");

                bool isAdmin = IsUserAdmin(currentUser);
                if (!isAdmin)
                {
                    TempData["ErrorMessage"] = "Chỉ Admin mới có quyền tạo tài khoản mới/管理者のみが新しいアカウントを作成できます";
                    return RedirectToAction("Index");
                }

                PopulateEditDropdowns();
                return View(new User());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UserController.Create] Error: {ex.Message}");
                TempData["ErrorMessage"] = "Có lỗi xảy ra. Vui lòng thử lại.";
                return RedirectToAction("Index");
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Create(User model)
        {
            try
            {
                // SỬA: Sử dụng helper method để kiểm tra admin
                var currentUser = Session["CurrentUser"] as User;
                if (currentUser == null)
                    return RedirectToAction("Login", "Account");

                bool isAdmin = IsUserAdmin(currentUser);
                if (!isAdmin)
                {
                    TempData["ErrorMessage"] = "Chỉ Admin mới có quyền tạo tài khoản mới/管理者のみが新しいアカウントを作成できます";
                    return RedirectToAction("Index");
                }

                if (!ModelState.IsValid)
                {
                    PopulateEditDropdowns();
                    return View(model);
                }

                // SỬA: Sử dụng instance _context thay vì using statement
                // Check if ADID already exists using Entity Framework
                if (_context.Users.Any(u => u.ADID == model.ADID))
                {
                    ModelState.AddModelError("ADID", "ADID đã tồn tại");
                    PopulateEditDropdowns();
                    return View(model);
                }

                // Add user to database using Entity Framework
                _context.Users.Add(model);
                _context.SaveChanges();

                TempData["SuccessMessage"] = $"Đã tạo thành công tài khoản/アカウント情報が正常に更新されました {model.ADID}! Role: {model.Role}";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UserController.Create] Error: {ex.Message}");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tạo tài khoản. Vui lòng thử lại.";
                PopulateEditDropdowns();
                return View(model);
            }
        }

        public ActionResult Edit(string id)
        {
            try
            {
                // SỬA: Sử dụng helper method để kiểm tra admin
                var currentUser = Session["CurrentUser"] as User;
                if (currentUser == null)
                    return RedirectToAction("Login", "Account");

                bool isAdmin = IsUserAdmin(currentUser);

                // Admin có thể edit bất kỳ user nào, user thường chỉ có thể edit chính mình
                if (!isAdmin && currentUser.ADID != id)
                {
                    TempData["ErrorMessage"] = "Bạn chỉ có thể chỉnh sửa thông tin của chính mình/自分の情報しか編集できません";
                    return RedirectToAction("Index");
                }

                // SỬA: Sử dụng instance _context thay vì using statement
                // Load user from database using Entity Framework
                var user = _context.Users.FirstOrDefault(u => u.ADID == id);
                if (user == null) return HttpNotFound();

                // SỬA: Debug log để hiện thị role mapping
                System.Diagnostics.Debug.WriteLine($"[UserController.Edit] User {user.ADID} - Role: {user.Role} (value: {(int)user.Role})");

                // Populate dropdown lists
                PopulateEditDropdowns();

                return View(user);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UserController.Edit] Error: {ex.Message}");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tải thông tin người dùng. Vui lòng thử lại.";
                return RedirectToAction("Index");
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Edit(User model)
        {
            try
            {
                // SỬA: Sử dụng helper method để kiểm tra admin
                var currentUser = Session["CurrentUser"] as User;
                if (currentUser == null)
                    return RedirectToAction("Login", "Account");

                bool isAdmin = IsUserAdmin(currentUser);

                // Admin có thể edit bất kỳ user nào, user thường chỉ có thể edit chính mình
                if (!isAdmin && currentUser.ADID != model.ADID)
                {
                    TempData["ErrorMessage"] = "Bạn chỉ có thể chỉnh sửa thông tin của chính mình/自分の情報しか編集できません";
                    return RedirectToAction("Index");
                }

                if (!ModelState.IsValid)
                {
                    PopulateEditDropdowns();
                    return View(model);
                }

                // SỬA: Sử dụng instance _context thay vì using statement
                // Find and update user in database using Entity Framework
                var existingUser = _context.Users.FirstOrDefault(u => u.ADID == model.ADID);
                if (existingUser == null) return HttpNotFound();

                // SỬA: Debug log để theo dõi role changes
                System.Diagnostics.Debug.WriteLine($"[UserController.Edit] Before update - User {existingUser.ADID}: Role {existingUser.Role} (value: {(int)existingUser.Role})");
                System.Diagnostics.Debug.WriteLine($"[UserController.Edit] New role from model: {model.Role} (value: {(int)model.Role})");

                // SỬA: Nếu không phải Admin, không cho phép thay đổi Role
                if (!isAdmin)
                {
                    // Giữ nguyên Role cũ nếu user không phải Admin
                    model.Role = existingUser.Role;
                    System.Diagnostics.Debug.WriteLine($"[UserController.Edit] Non-admin user, keeping original role: {model.Role}");
                }

                // SỬA: Cập nhật từng property thay vì dùng SetValues để tránh tracking conflict
                existingUser.Name = model.Name;
                existingUser.DeptCode = model.DeptCode;
                existingUser.Group = model.Group;
                existingUser.Title = model.Title;
                existingUser.Email = model.Email;
                existingUser.Factory = model.Factory;
                existingUser.AvatarUrl = model.AvatarUrl;
                existingUser.Role = model.Role; // SỬA: Đảm bảo Role được cập nhật

                // SỬA: Debug log trước khi save
                System.Diagnostics.Debug.WriteLine($"[UserController.Edit] About to save - User {existingUser.ADID}: Role {existingUser.Role} (value: {(int)existingUser.Role})");

                _context.SaveChanges();

                // SỬA: Verify sau khi save
                var verifyUser = _context.Users.FirstOrDefault(u => u.ADID == model.ADID);
                System.Diagnostics.Debug.WriteLine($"[UserController.Edit] After save verification - User {verifyUser.ADID}: Role {verifyUser.Role} (value: {(int)verifyUser.Role})");

                // SỬA: Cập nhật session nếu user đang edit chính mình
                if (currentUser.ADID == model.ADID)
                {
                    Session["CurrentUser"] = verifyUser;
                    System.Diagnostics.Debug.WriteLine($"[UserController.Edit] Updated session for current user with role: {verifyUser.Role}");
                }

                TempData["SuccessMessage"] = $"Đã cập nhật thành công thông tin tài khoản/アカウント情報が正常に更新されました {model.ADID}! Role: {verifyUser.Role}";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UserController.Edit] Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[UserController.Edit] Inner exception: {ex.InnerException.Message}");
                }
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi cập nhật thông tin. Vui lòng thử lại.";
                PopulateEditDropdowns();
                return View(model);
            }
        }

        public ActionResult Delete(string id)
        {
            try
            {
                // SỬA: Sử dụng helper method để kiểm tra admin
                var currentUser = Session["CurrentUser"] as User;
                if (currentUser == null)
                {
                    TempData["ErrorMessage"] = "Vui lòng đăng nhập!";
                    return RedirectToAction("Login", "Account");
                }

                bool isAdmin = IsUserAdmin(currentUser);
                if (!isAdmin)
                {
                    TempData["ErrorMessage"] = "Chỉ Admin mới có quyền xóa tài khoản/ 管理者のみがアカウントを削除する権利を持っています";
                    return RedirectToAction("Index");
                }

                // SỬA: Sử dụng instance _context thay vì using statement
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UserController.Delete] Error: {ex.Message}");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi xóa tài khoản. Vui lòng thử lại.";
                return RedirectToAction("Index");
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