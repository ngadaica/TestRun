using System;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using TrueTestRun.Models;
using TrueTestRun.Services;
using System.Collections.Generic;

namespace TrueTestRun.Controllers
{
    [Authorize]
    public class ApprovalController : Controller
    {
        private readonly FileStorageService _fs;
        private readonly WorkflowService _wf;
        private readonly EmailService _email;
        private readonly ExcelService _ex;
        private readonly ImageService _imageService;

        public ApprovalController()
        {
            _fs = new FileStorageService();
            _wf = new WorkflowService();
            _email = new EmailService();
            _ex = new ExcelService();
            _imageService = new ImageService();
        }

        public ActionResult ProcessRequest(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["ErrorMessage"] = "Request ID không hợp lệ.";
                return RedirectToAction("TruocTestRun", "Request");
            }

            var request = _fs.LoadRequest(id);
            if (request == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy request.";
                return RedirectToAction("TruocTestRun", "Request");
            }

            var currentUser = Session["CurrentUser"] as User;
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var currentStep = _wf.GetCurrentStep(request);
            if (currentStep == null)
            {
                TempData["ErrorMessage"] = "Không xác định được bước hiện tại của request.";
                return RedirectToAction("TruocTestRun", "Request");
            }

            // Kiểm tra quyền xử lý/phê duyệt
            bool canApprove = CanApprove(currentUser);
            bool canEdit = CanEdit(currentUser, request);

            if (!canApprove && !canEdit)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền xử lý request này.";
                return RedirectToAction("TruocTestRun", "Request");
            }

            ViewBag.CurrentUser = currentUser;
            ViewBag.CurrentStep = currentStep;
            ViewBag.CanApprove = canApprove;
            ViewBag.CanEdit = canEdit;

            var requestFolder = Server.MapPath($"~/App_Data/Requests/{id}");
            var excelPath = Path.Combine(requestFolder, "request.xlsx");
            ViewBag.ExcelPath = excelPath;

            return View("ApprovalForm", request);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ProcessApproval(string RequestID, string action, string Comments)
        {
            if (string.IsNullOrEmpty(RequestID))
            {
                TempData["ErrorMessage"] = "Request ID không hợp lệ.";
                return RedirectToAction("TruocTestRun", "Request");
            }

            var request = _fs.LoadRequest(RequestID);
            if (request == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy request.";
                return RedirectToAction("TruocTestRun", "Request");
            }

            var currentUser = Session["CurrentUser"] as User;
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var currentStep = _wf.GetCurrentStep(request);

            if (currentStep == null || !CanApprove(currentUser))
            {
                TempData["ErrorMessage"] = "Bạn không có quyền phê duyệt request này.";
                return GetRedirectToPhase(request);
            }

            try
            {
                switch (action?.ToLower())
                {
                    case "approve":
                        ProcessApproveAction(request, currentUser, currentStep, Comments);
                        TempData["SuccessMessage"] = $"Đã phê duyệt thành công đơn {request.RequestID}!";
                        break;

                    case "reject":
                        if (string.IsNullOrWhiteSpace(Comments))
                        {
                            TempData["ErrorMessage"] = "Vui lòng nhập lý do từ chối.";
                            return View("ApprovalForm", request);
                        }
                        ProcessRejectAction(request, currentUser, currentStep, Comments);
                        TempData["SuccessMessage"] = $"Đã từ chối đơn {request.RequestID}.";
                        break;

                    case "complete":
                        // Chỉ cho Staff hoàn thành bước nhập liệu (không cho các chức vụ còn lại)
                        if (!CanEdit(currentUser, request))
                        {
                            TempData["ErrorMessage"] = "Bạn không có quyền hoàn thành bước này!";
                            return View("ApprovalForm", request);
                        }
                        ProcessCompleteAction(request, currentUser, currentStep, Comments);
                        TempData["SuccessMessage"] = $"Đã hoàn thành công việc cho đơn {request.RequestID}!";
                        break;

                    default:
                        TempData["ErrorMessage"] = "Hành động không hợp lệ.";
                        return View("ApprovalForm", request);
                }

                return GetRedirectToPhase(request);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Có lỗi xảy ra: {ex.Message}";
                return View("ApprovalForm", request);
            }
        }

        private void ProcessApproveAction(Request request, User currentUser, WorkflowStep currentStep, string comment)
        {
            var excelPath = Server.MapPath($"~/App_Data/Requests/{request.RequestID}/request.xlsx");
            byte[] sealImage = _imageService.GetOrCreateSealImage(currentUser.DeptCode, currentUser.Name, currentUser.Role);
            if (sealImage != null)
            {
                _ex.AddSealImage(excelPath, currentStep.Index, sealImage);
            }

            _wf.AdvanceStep(request, currentUser.ADID, comment);
            _fs.SaveRequest(request);
            _ex.FillFields(excelPath, request);

            SendEmailForNextStep(request);
        }

        private void ProcessRejectAction(Request request, User currentUser, WorkflowStep currentStep, string comment)
        {
            request.IsRejected = true;
            currentStep.Status = "Rejected";
            currentStep.ApproverADID = currentUser.ADID;
            currentStep.Comment = comment;
            currentStep.ApprovedAt = DateTime.Now;
            _fs.SaveRequest(request);

            var creator = _fs.LoadUsers().FirstOrDefault(u => u.ADID == request.CreatedByADID);
            if (creator != null)
            {
                var editUrl = Url.Action("Edit", "Request", new { id = request.RequestID }, protocol: Request.Url.Scheme);
                // _email.SendRejectNotificationToCreator(request, creator, currentUser, comment, editUrl);
            }
        }

        private void ProcessCompleteAction(Request request, User currentUser, WorkflowStep currentStep, string comment)
        {
            _wf.AdvanceStep(request, currentUser.ADID, comment);
            _fs.SaveRequest(request);

            var excelPath = Server.MapPath($"~/App_Data/Requests/{request.RequestID}/request.xlsx");
            _ex.FillFields(excelPath, request);

            SendEmailForNextStep(request);
        }

        private ActionResult GetRedirectToPhase(Request request)
        {
            if (request == null)
            {
                return RedirectToAction("TruocTestRun", "Request");
            }

            var phase = GetPhaseByCurrentStep(request.CurrentStepIndex);
            switch (phase)
            {
                case TestRunPhase.TruocTestRun:
                    return RedirectToAction("TruocTestRun", "Request");
                case TestRunPhase.GiuaTestRun:
                    return RedirectToAction("GiuaTestRun", "Request");
                case TestRunPhase.SauTestRun:
                    return RedirectToAction("SauTestRun", "Request");
                default:
                    return RedirectToAction("TruocTestRun", "Request");
            }
        }

        private TestRunPhase GetPhaseByCurrentStep(int stepIndex)
        {
            if (stepIndex <= 1) return TestRunPhase.TruocTestRun;
            if (stepIndex <= 7) return TestRunPhase.GiuaTestRun;
            return TestRunPhase.SauTestRun;
        }

        public ActionResult Index(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                return RedirectToAction("ProcessRequest", new { id = id });
            }

            return RedirectToAction("TruocTestRun", "Request");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubmitData(string id, FormCollection form)
        {
            return ProcessApproval(id, "complete", form["Comment"]);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Approve(string id, string comment, bool shouldStamp = false)
        {
            return ProcessApproval(id, "approve", comment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Reject(string id, string comment)
        {
            return ProcessApproval(id, "reject", comment);
        }

        // ===== HELPER METHODS =====

        /// <summary>
        /// Chỉ các chức vụ quản lý sơ cấp, quản lý trung cấp, G.M mới được phê duyệt
        /// </summary>
        private bool CanApprove(User user)
        {
            if (user == null || string.IsNullOrEmpty(user.Title)) return false;
            var title = user.Title.Trim();
            return title.Equals("Quản lý sơ cấp", StringComparison.OrdinalIgnoreCase) ||
                   title.Equals("Quản lý trung cấp", StringComparison.OrdinalIgnoreCase) ||
                   title.Equals("G.M", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Chỉ Staff được chỉnh sửa/hoàn thành đơn của mình
        /// </summary>
        private bool CanEdit(User user, Request request)
        {
            return user != null
                && user.Title != null
                && user.Title.Trim().Equals("Staff", StringComparison.OrdinalIgnoreCase)
                && request.CreatedByADID == user.ADID;
        }

        private void SendEmailForNextStep(Request request)
        {
            var nextStep = _wf.GetCurrentStep(request);
            if (nextStep == null) return;

            if (!string.IsNullOrEmpty(nextStep.NextApproverADID))
            {
                var specificUser = _fs.LoadUsers().FirstOrDefault(u => u.ADID == nextStep.NextApproverADID);
                if (specificUser != null)
                {
                    var url = Url.Action("ProcessRequest", "Approval", new { id = request.RequestID }, protocol: Request.Url.Scheme);
                    _email.SendApprovalRequest(request, nextStep, url, false);
                    return;
                }
            }

            var recipients = _fs.LoadUsers().Where(u =>
                u.DeptCode == nextStep.DeptCode && u.Title == nextStep.Role
            ).ToList();

            if (recipients.Any())
            {
                var url = Url.Action("ProcessRequest", "Approval", new { id = request.RequestID }, protocol: Request.Url.Scheme);
                _email.SendApprovalRequest(request, nextStep, url, false);
            }
        }
    }
}