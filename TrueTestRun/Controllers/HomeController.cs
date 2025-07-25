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

            var allRequests = fs.LoadAllRequests();
            var vm = new DashboardViewModel
            {
                CompletedCount = allRequests.Count(r => r.IsCompleted),
                PendingCount = allRequests.Count(r => !r.IsCompleted && !r.IsRejected)
            };

            return View(vm);
        }
    }
}