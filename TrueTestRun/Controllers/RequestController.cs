using Org.BouncyCastle.Ocsp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using TrueTestRun.Models;
using TrueTestRun.Services;

namespace TrueTestRun.Controllers
{
    [Authorize]
    public class RequestController : Controller
    {
        private readonly FileStorageService fs = new FileStorageService();
        private readonly WorkflowService wf = new WorkflowService();
        private readonly ExcelService ex = new ExcelService();
        private readonly EmailService em = new EmailService();

        public ActionResult Index()
        {
            var currentUser = Session["CurrentUser"] as User;
            if (currentUser == null)
                return RedirectToAction("Login", "Account");

            // Staff: chỉ thấy request mình tạo và chưa hoàn thành
            if (IsStaff(currentUser))
            {
                var myRequests = fs.LoadAllRequests()
                    .Where(r => r.CreatedByADID == currentUser.ADID && !r.IsCompleted)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToList();
                return View(myRequests);
            }
            // Quản lý sơ cấp, trung cấp, G.M: chỉ thấy các request cần phê duyệt
            else if (IsManagerOrGM(currentUser))
            {
                var approveRequests = fs.LoadAllRequests()
                    .Where(r =>
                        !r.IsCompleted &&
                        !r.IsRejected &&
                        r.History.Count > r.CurrentStepIndex &&
                        (
                            r.History[r.CurrentStepIndex].NextApproverADID == currentUser.ADID ||
                            (string.IsNullOrEmpty(r.History[r.CurrentStepIndex].NextApproverADID) &&
                             r.History[r.CurrentStepIndex].DeptCode == currentUser.DeptCode &&
                             r.History[r.CurrentStepIndex].Role == currentUser.Title)
                        ))
                    .OrderByDescending(r => r.CreatedAt)
                    .ToList();
                return View(approveRequests);
            }
            else // Admin: thấy tất cả request chưa hoàn thành
            {
                var allRequests = fs.LoadAllRequests()
                    .Where(r => !r.IsCompleted)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToList();
                return View(allRequests);
            }
        }

        public ActionResult Completed()
        {
            var currentUser = Session["CurrentUser"] as User;
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var allRequests = fs.LoadAllRequests();
            var completedRequests = allRequests
                .Where(r => r.IsCompleted)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            return View(completedRequests);
        }

        public ActionResult Create()
        {
            var currentUser = Session["CurrentUser"] as User;
            if (currentUser == null || !IsStaff(currentUser))
                return new HttpStatusCodeResult(403, "Chỉ Staff mới được tạo request.");

            Session.Remove("LivePreviewFields");
            return View(new Request());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Request model)
        {
            var allCheckboxKeys = new List<string> { "EPE-PCB", "EPE-PCB1", "EPE-PCB2", "EPE-PCB3", "EPE-EE", "EPE-EE1", "EPE-EE2", "EPE-EE3", "EPE-EE4" };

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Chuẩn hóa dữ liệu
            if (model.Fields != null)
            {
                var fieldKeys = model.Fields.Keys.ToList();
                foreach (var key in fieldKeys)
                    if (model.Fields[key] != null && model.Fields[key].ToLower().Contains("true"))
                        model.Fields[key] = "true";
            }

            // Tạo Request ID
            var prefix = "TR";
            var randomPart = new Random().Next(1000, 9999);
            var id = $"{prefix}-{randomPart}";
            model.RequestID = id;
            model.CreatedAt = DateTime.Now;
            model.CreatedByADID = (Session["CurrentUser"] as User)?.ADID;
            model.History = wf.InitHistory();
            model.CurrentStepIndex = 0;

            var requestFolder = Server.MapPath($"~/App_Data/Requests/{id}");
            Directory.CreateDirectory(requestFolder);
            var templatePath = Server.MapPath("~/App_Data/Data.xlsx");
            var excelPath = Path.Combine(requestFolder, "request.xlsx");
            if (!System.IO.File.Exists(excelPath)) System.IO.File.Copy(templatePath, excelPath);

            ex.FillFields(excelPath, model);

            fs.SaveRequest(model);

            var firstStep = wf.GetCurrentStep(model);
            if (firstStep != null)
            {
                var approvalUrl = Url.Action("Index", "Approval",
                    new { id = model.RequestID },
                    protocol: Request.Url.Scheme);

                em.SendApprovalRequest(model, firstStep, approvalUrl, false);
            }

            TempData["SuccessMessage"] = $"Đã tạo thành công request {model.RequestID} và gửi email cho người phê duyệt đầu tiên!";
            return RedirectToAction("Index");
        }

        public ActionResult Edit(string id)
        {
            var req = fs.LoadRequest(id);
            var currentUser = Session["CurrentUser"] as User;
            if (req == null) return HttpNotFound();

            // Chỉ Staff được sửa đơn mình tạo
            if (currentUser == null || !IsStaff(currentUser) || req.CreatedByADID != currentUser.ADID)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền sửa đơn này!";
                return RedirectToAction("Index");
            }

            Session["LivePreviewFields"] = req.Fields;
            return View(req);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Request model)
        {
            var allCheckboxKeys = new List<string> { "EPE-PCB", "EPE-PCB1", "EPE-PCB2", "EPE-PCB3", "EPE-EE", "EPE-EE1", "EPE-EE2", "EPE-EE3", "EPE-EE4" };
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Chuẩn hóa dữ liệu checkbox
            if (model.Fields != null)
            {
                var fieldKeys = model.Fields.Keys.ToList();
                foreach (var key in fieldKeys)
                {
                    if (model.Fields[key] != null && model.Fields[key].ToLower().Contains("true"))
                    {
                        model.Fields[key] = "true";
                    }
                }
            }

            var originalRequest = fs.LoadRequest(model.RequestID);
            if (originalRequest == null) return HttpNotFound();

            originalRequest.Fields = model.Fields;

            bool wasRejected = originalRequest.IsRejected;

            // Nếu đơn đang bị từ chối thì reset trạng thái để tiếp tục phê duyệt
            if (originalRequest.IsRejected)
            {
                originalRequest.IsRejected = false;

                if (originalRequest.History != null && originalRequest.History.Count > originalRequest.CurrentStepIndex)
                {
                    var currentStep = originalRequest.History[originalRequest.CurrentStepIndex];
                    currentStep.Status = "Processing";
                    currentStep.ApproverADID = null;
                    currentStep.Comment = null;
                    currentStep.ApprovedAt = null;
                }
            }

            fs.SaveRequest(originalRequest);

            var excelPath = Server.MapPath($"~/App_Data/Requests/{model.RequestID}/request.xlsx");
            ex.FillFields(excelPath, originalRequest);

            if (wasRejected)
            {
                var nextStep = originalRequest.History[originalRequest.CurrentStepIndex];
                var approvalUrl = Url.Action("Index", "Approval", new { id = originalRequest.RequestID }, protocol: Request.Url.Scheme);
                em.SendApprovalRequest(originalRequest, nextStep, approvalUrl, isResubmission: true);
            }

            TempData["SuccessMessage"] = "Cập nhật Request thành công!";
            return RedirectToAction("Index");
        }

        public ActionResult Preview(string id)
        {
            var req = fs.LoadRequest(id);
            if (req == null) return HttpNotFound();
            return View(req);
        }

        [HttpPost]
        public ActionResult LivePreviewData(Request model)
        {
            var allCheckboxKeys = new List<string> { "EPE-EE", "EPE-EE1", "EPE-EE2", "EPE-EE3", "EPE-EE4", "EPE-PCB", "EPE-PCB1", "EPE-PCB2", "EPE-PCB3", "EPE-G.M" };
            if (model.Fields == null) model.Fields = new Dictionary<string, string>();

            foreach (var chkKey in allCheckboxKeys)
            {
                if (!model.Fields.ContainsKey(chkKey))
                {
                    model.Fields.Add(chkKey, "false");
                }
            }

            Session["LivePreviewFields"] = model.Fields;
            return Json(new { success = true });
        }

        public ActionResult PreviewImage(string id)
        {
            var excelPath = Server.MapPath($"~/App_Data/Requests/{id}/request.xlsx");
            if (!System.IO.File.Exists(excelPath)) return HttpNotFound();
            var req = fs.LoadRequest(id);
            if (req == null) return HttpNotFound();
            ex.FillFields(excelPath, req);
            var png = ex.RenderToPng(excelPath);
            return File(png, "image/png");
        }

        public ActionResult GeneratePreviewImage()
        {
            var fields = Session["LivePreviewFields"] as Dictionary<string, string> ?? new Dictionary<string, string>();
            var templatePath = Server.MapPath("~/App_Data/Data.xlsx");
            if (!System.IO.File.Exists(templatePath)) return HttpNotFound();

            var tempRequest = new Request
            {
                Fields = fields,
                History = new List<WorkflowStep>()
            };

            var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xlsx");
            try
            {
                System.IO.File.Copy(templatePath, tempFilePath, true);

                ex.FillFields(tempFilePath, tempRequest);

                var pngBytes = ex.RenderToPng(tempFilePath);
                return File(pngBytes, "image/png");
            }
            finally
            {
                if (System.IO.File.Exists(tempFilePath)) System.IO.File.Delete(tempFilePath);
            }
        }

        public ActionResult TruocTestRun()
        {
            var requests = GetRequestsByPhase(TestRunPhase.TruocTestRun);
            ViewBag.Title = "Request – Trước Test Run";
            return View("PhaseList", requests);
        }

        public ActionResult GiuaTestRun()
        {
            var requests = GetRequestsByPhase(TestRunPhase.GiuaTestRun);
            ViewBag.Title = "Request – Giữa Test Run";
            return View("PhaseList", requests);
        }

        public ActionResult SauTestRun()
        {
            var requests = GetRequestsByPhase(TestRunPhase.SauTestRun);
            ViewBag.Title = "Request – Sau Test Run";
            return View("PhaseList", requests);
        }

        private List<Request> GetRequestsByPhase(TestRunPhase phase)
        {
            var allRequests = fs.LoadAllRequests();
            return allRequests
                .Where(r => !r.IsCompleted && !r.IsRejected &&
                    GetPhaseByCurrentStep(r.CurrentStepIndex) == phase)
                .ToList();
        }

        private TestRunPhase GetPhaseByCurrentStep(int stepIndex)
        {
            if (stepIndex <= 1) return TestRunPhase.TruocTestRun;
            if (stepIndex <= 7) return TestRunPhase.GiuaTestRun;
            return TestRunPhase.SauTestRun;
        }

        // ====== Helper methods for role checks ======
        private bool IsStaff(User user)
        {
            return user != null && user.Title != null && user.Title.Trim().Equals("Staff", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsManagerOrGM(User user)
        {
            if (user == null || string.IsNullOrEmpty(user.Title)) return false;
            var title = user.Title.Trim();
            return title.Equals("Quản lý sơ cấp", StringComparison.OrdinalIgnoreCase) ||
                   title.Equals("Quản lý trung cấp", StringComparison.OrdinalIgnoreCase) ||
                   title.Equals("G.M", StringComparison.OrdinalIgnoreCase);
        }
    }
}