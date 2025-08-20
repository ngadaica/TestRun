using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using TrueTestRun.ViewModels;
using TrueTestRun.Models;
using TrueTestRun.Services;

namespace TrueTestRun.Controllers
{
    public class HomeController : Controller
    {
        // GET: Home
        public ActionResult Index()
        {
            // Lấy tên user Windows của máy chủ
            ViewBag.WindowsUser = Environment.UserName;
            
            // Tìm user trong hệ thống có ADID trùng với username
            var fs = new FileStorageService();

            // Lấy toàn bộ request của hệ thống
            var allRequests = fs.LoadAllRequests();
            
            // Tính toán các số liệu thống kê
            var vm = new DashboardViewModel
            {
                TotalCount = allRequests.Count,
                CompletedCount = allRequests.Count(r => r.IsCompleted),
                PendingCount = allRequests.Count(r => !r.IsCompleted && !r.IsRejected), // Đang xử lý
                RejectedCount = allRequests.Count(r => r.IsRejected) // Bị từ chối (NG)
            };

            return View(vm);
        }
    }
}