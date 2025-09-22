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
    public class ApprovalController : BaseController
    {
        private readonly FileStorageService _fs;
        private readonly WorkflowService _wf;
        private readonly EmailService _email;
        private readonly ExcelService _ex;
        private readonly ImageService _imageService;

        public ApprovalController()
        {
            // SỬA: Don't create new context, use inherited one from BaseController
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

            // SỬA: Use Session directly since BaseController handles authentication
            var currentUser = Session["CurrentUser"] as User;
            if (currentUser == null)
            {
                // SỬA: This shouldn't happen since BaseController handles auth, but just in case
                System.Diagnostics.Debug.WriteLine("[ApprovalController] Session user is null, redirecting to login");
                return RedirectToAction("Login", "Account");
            }

            var currentStep = _wf.GetCurrentStep(request);

            if (currentStep == null)
            {
                TempData["ErrorMessage"] = $"Không xác định được bước hiện tại của request.";
                return RedirectToAction("TruocTestRun", "Request");
            }

            // ========== SỬA: LOGIC KIỂM TRA QUYỀN ĐƠN GIẢN HƠN ==========
            // Thay vì kiểm tra ActionType phức tạp, chỉ kiểm tra:
            // 1. Staff có thể điền form (các step DataEntry)
            // 2. Manager có thể phê duyệt (các step Approval)

            bool hasPermission = true; // Mặc định cho phép xem
            bool canApprove = CanApprove(currentUser);
            bool canEdit = CanEdit(currentUser, request);

            // Kiểm tra quyền dựa trên Actor
            if (currentStep.Actor == StepActor.Approver && !canApprove)
            {
                TempData["ErrorMessage"] = $"Bạn không có quyền phê duyệt. Cần: Manager thuộc phòng {currentStep.DeptCode}";
                return RedirectToAction("TruocTestRun", "Request");
            }

            if (currentStep.Actor == StepActor.DataEntry && !canEdit)
            {
                TempData["ErrorMessage"] = $"Bạn không có quyền điền form. Cần: Staff thuộc phòng {currentStep.DeptCode}";
                return RedirectToAction("TruocTestRun", "Request");
            }

            ViewBag.CurrentUser = currentUser;
            ViewBag.CurrentStep = currentStep;
            ViewBag.CanApprove = canApprove && currentStep.Actor == StepActor.Approver;
            ViewBag.CanEdit = canEdit && currentStep.Actor == StepActor.DataEntry;

            // Chọn view dựa trên step index
            string viewName = "ApprovalForm"; // Default

            if (currentStep.Index == 2) // Step 2 - PCB Staff điền form
            {
                viewName = "DataEntryStep2";
            }
            else if (currentStep.Index == 4) // Step 4 - PCB Staff điền form
            {
                viewName = "DataEntryStep4";
            }
            else if (currentStep.Index == 5) // Step 5 - QLTC PCB phê duyệt (Approval)
            {
                viewName = "ApprovalFormStep5";
            }
            else if (currentStep.Index == 6) // Step 6 - PCB Staff điền kết quả
            {
                viewName = "DataEntryStep6";
            }
            else if (currentStep.Index == 8) // THÊM: Step 8 - EE Staff điền form
            {
                viewName = "DataEntryStep8";
            }

            return View(viewName, request);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ProcessApproval(string RequestID, string action, string Comments)
        {
            try
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

                // SỬA: Enhanced user authentication check with better error handling
                var currentUser = GetCurrentUser();
                if (currentUser == null)
                {
                    System.Diagnostics.Debug.WriteLine("[ApprovalController] Current user is null, attempting re-authentication");

                    // Try to re-authenticate
                    currentUser = Session["CurrentUser"] as User;
                    if (currentUser == null)
                    {
                        TempData["ErrorMessage"] = "Phiên đăng nhập đã hết hạn. Vui lòng đóng trình duyệt và click lại vào link trong email.";
                        return RedirectToAction("Index", "Home");
                    }
                }

                var currentStep = _wf.GetCurrentStep(request);
                if (currentStep == null)
                {
                    TempData["ErrorMessage"] = "Không xác định được bước hiện tại của request.";
                    return GetRedirectToPhase(request);
                }

                // ========== SỬA: LOGIC KIỂM TRA QUYỀN ĐƠN GIẢN ========== 
                bool canPerformAction = false;

                // SỬA: Logic đơn giản dựa trên Actor
                switch (action?.ToLower())
                {
                    case "approve":
                    case "reject":
                        canPerformAction = CanApprove(currentUser) && currentStep.Actor == StepActor.Approver;
                        break;

                    case "complete":
                        canPerformAction = CanEdit(currentUser, request) && currentStep.Actor == StepActor.DataEntry;
                        break;

                    default:
                        canPerformAction = false;
                        break;
                }

                if (!canPerformAction)
                {
                    var errorMsg = $"Bạn không có quyền thực hiện '{action}'. Cần: {(action == "approve" || action == "reject" ? "Manager" : "Staff")} thuộc phòng {currentStep.DeptCode}";
                    TempData["ErrorMessage"] = errorMsg;
                    System.Diagnostics.Debug.WriteLine($"[ApprovalController] Permission denied: {errorMsg}");
                    return GetRedirectToPhase(request);
                }

                // SỬA: Enhanced action processing with better error handling
                switch (action?.ToLower())
                {
                    case "approve":
                        ProcessApproveAction(request, currentUser, currentStep, Comments);
                        TempData["SuccessMessage"] = $"Đã phê duyệt thành công đơn {request.RequestID}!";
                        break;

                    case "reject":
                        ProcessRejectAction(request, currentUser, currentStep, Comments);
                        TempData["SuccessMessage"] = $"Đã từ chối đơn {request.RequestID}.";
                        break;

                    case "complete":
                        ProcessCompleteAction(request, currentUser, currentStep, Comments);
                        TempData["SuccessMessage"] = $"Đã hoàn thành công việc cho đơn {request.RequestID}!";
                        break;

                    default:
                        TempData["ErrorMessage"] = "Hành động không hợp lệ.";
                        return GetRedirectToPhase(request);
                }

                if (action?.ToLower() == "approve" && currentStep.Actor == StepActor.Approver)
                {
                    try
                    {
                        _email.SendApprovalCompletedNotification(request, currentStep, currentUser);
                    }
                    catch (Exception emailEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ApprovalController] Email notification failed: {emailEx.Message}");
                    }

                    // Clear any live preview session so the preview uses saved fields
                    try { Session.Remove("LivePreviewFields"); } catch { }

                    ViewBag.BackUrl = GetPhaseUrl(request);
                    ViewBag.CurrentUser = currentUser;
                    ViewBag.CurrentStep = currentStep;
                    ViewBag.CanApprove = false;
                    ViewBag.CanEdit = false;

                    return View("ApprovedRequest", request);
                }

                // other actions: keep original behaviour (redirect back to phase list)
                return GetRedirectToPhase(request);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ApprovalController] ProcessApproval error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ApprovalController] Stack trace: {ex.StackTrace}");

                var errorMessage = "Có lỗi xảy ra khi xử lý phê duyệt. ";
                if (ex.Message.Contains("authentication") || ex.Message.Contains("session"))
                {
                    errorMessage += "Vui lòng đóng trình duyệt và thử lại từ link email mới.";
                }
                else
                {
                    errorMessage += "Vui lòng thử lại sau.";
                }

                TempData["ErrorMessage"] = errorMessage;

                try
                {
                    var request = _fs.LoadRequest(RequestID);
                    return GetRedirectToPhase(request);
                }
                catch
                {
                    return RedirectToAction("Index", "Home");
                }
            }
        }

        /// <summary>
        /// Compute the URL for the phase list (TruocTestRun/GiuaTestRun/SauTestRun) based on request.CurrentStepIndex.
        /// Returned URL is absolute to be safe for views.
        /// </summary>
        private string GetPhaseUrl(Request request)
        {
            if (request == null)
            {
                return Url.Action("TruocTestRun", "Request");
            }

            var phase = GetPhaseByCurrentStep(request.CurrentStepIndex);
            switch (phase)
            {
                case TestRunPhase.TruocTestRun:
                    return Url.Action("TruocTestRun", "Request", null, Request.Url?.Scheme);
                case TestRunPhase.GiuaTestRun:
                    return Url.Action("GiuaTestRun", "Request", null, Request.Url?.Scheme);
                case TestRunPhase.SauTestRun:
                    return Url.Action("SauTestRun", "Request", null, Request.Url?.Scheme);
                default:
                    return Url.Action("TruocTestRun", "Request", null, Request.Url?.Scheme);
            }
        }

        private void ProcessApproveAction(Request request, User currentUser, WorkflowStep currentStep, string comment)
        {
            var excelPath = Server.MapPath($"~/App_Data/Requests/{request.RequestID}/request.xlsx");

            try
            {
                var step5Keys = new[] { "LapRapStep5", "TinhNangStep5", "NgoaiQuanStep5", "CommentStep5" };
                var formData = new List<RequestField>();

                // Only update Step 5 fields at Step 5 or when those inputs are posted
                var postedKeys = Request.Form.AllKeys ?? new string[0];
                bool hasStep5Inputs = step5Keys.Any(k => postedKeys.Contains(k, StringComparer.OrdinalIgnoreCase));
                bool shouldUpdateStep5 = currentStep.Index == 5 || hasStep5Inputs;

                if (shouldUpdateStep5)
                {
                    bool IsChecked(string key)
                    {
                        var vals = Request.Form.GetValues(key);
                        if (vals == null || vals.Length == 0) return false;
                        foreach (var v in vals)
                        {
                            var t = (v ?? "").Trim();
                            if (t.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                                t.Equals("on", StringComparison.OrdinalIgnoreCase) ||
                                t.Equals("1") ||
                                t.Equals("yes", StringComparison.OrdinalIgnoreCase))
                                return true;
                        }
                        return false;
                    }

                    foreach (var key in new[] { "LapRapStep5", "TinhNangStep5", "NgoaiQuanStep5" })
                    {
                        var normalized = IsChecked(key) ? "true" : "false";
                        formData.Add(new RequestField { RequestID = request.RequestID, Key = key, Value = normalized });
                    }

                    var c = Request.Form["CommentStep5"];
                    if (string.IsNullOrWhiteSpace(c)) c = Request.Form["Comments"];
                    if (string.IsNullOrWhiteSpace(c)) c = comment;
                    if (!string.IsNullOrWhiteSpace(c))
                    {
                        formData.Add(new RequestField { RequestID = request.RequestID, Key = "CommentStep5", Value = c });
                    }

                    // Merge into request.Fields
                    var allStep5Keys = new HashSet<string>(step5Keys, StringComparer.OrdinalIgnoreCase);
                    var remaining = request.Fields.Where(f => !allStep5Keys.Contains(f.Key)).ToList();
                    request.Fields.Clear();
                    foreach (var f in remaining.Concat(formData)) request.Fields.Add(f);

                    _fs.SaveRequest(request);
                    try { _ex.FillFields(excelPath, request); } catch { }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ApprovalController] Error capturing Step5 fields: {ex.Message}");
            }

            // Seal, advance, save, fill
            var sealImage = _imageService.GetOrCreateSealImage(currentUser.DeptCode, currentUser.Name, currentUser.Role);
            if (sealImage != null && sealImage.Length > 0) _ex.AddSealImage(excelPath, currentStep.Index, sealImage);

            _wf.AdvanceStep(request, currentUser.ADID, comment);
            _fs.SaveRequest(request);
            _ex.FillFields(excelPath, request);

            SendEmailForNextStep(request);
        }

        private void ProcessCompleteAction(Request request, User currentUser, WorkflowStep currentStep, string comment)
        {
            // Xử lý dữ liệu form cho Step 2
            if (currentStep.Index == 2) // Step 2 - PCB Staff điền form
            {
                // Lấy dữ liệu từ form
                var formData = new List<RequestField>();

                // Xử lý checkbox
                foreach (string key in Request.Form.AllKeys)
                {
                    if (key.StartsWith("EPE-PCB") || key == "DaNhanHangTestRun")
                    {
                        var value = Request.Form[key];
                        if (!string.IsNullOrEmpty(value))
                        {
                            formData.Add(new RequestField
                            {
                                RequestID = request.RequestID,
                                Key = key,
                                Value = value == "true" ? "true" : value
                            });
                        }
                    }
                }

                // SỬA: Xử lý comment - chỉ thêm nếu có nội dung
                if (!string.IsNullOrEmpty(comment))
                {
                    formData.Add(new RequestField
                    {
                        RequestID = request.RequestID,
                        Key = "CommentStep2",
                        Value = comment
                    });
                }

                // Đảm bảo checkbox không được check có giá trị false
                var checkboxKeys = new[] { "DaNhanHangTestRun", "EPE-PCB1", "EPE-PCB2", "EPE-PCB3" };
                var submittedKeys = formData.Select(f => f.Key).ToHashSet();

                foreach (var cbKey in checkboxKeys)
                {
                    if (!submittedKeys.Contains(cbKey))
                    {
                        formData.Add(new RequestField
                        {
                            RequestID = request.RequestID,
                            Key = cbKey,
                            Value = "false"
                        });
                    }
                }

                // Cập nhật fields
                var existingFields = request.Fields.Where(f => !checkboxKeys.Contains(f.Key) && f.Key != "CommentStep2").ToList();
                request.Fields.Clear();

                foreach (var field in existingFields.Concat(formData))
                {
                    request.Fields.Add(field);
                }
            }
            // Xử lý dữ liệu form cho Step 4
            else if (currentStep.Index == 4) // Step 4 - PCB Staff điền form
            {
                var formData = new List<RequestField>();

                // Keep only 7 info fields at Step 4
                var infoKeys = new[] { "ThongTin1Step4", "ThongTin2Step4", "ThongTin3Step4", "ThongTin4Step4", "ThongTin5Step4", "ThongTin6Step4", "ThongTin7Step4" };
                foreach (var key in infoKeys)
                {
                    var value = Request.Form[key];
                    if (!string.IsNullOrEmpty(value))
                    {
                        formData.Add(new RequestField
                        {
                            RequestID = request.RequestID,
                            Key = key,
                            Value = value
                        });
                    }
                }

                // Update fields - remove only Step 4 info fields
                var allStep4Keys = new HashSet<string>(infoKeys);
                var existingFields = request.Fields.Where(f => !allStep4Keys.Contains(f.Key)).ToList();
                request.Fields.Clear();

                foreach (var field in existingFields.Concat(formData))
                {
                    request.Fields.Add(field);
                }
            }
            // Xử lý dữ liệu form cho Step 6 - Step này không có comment tổng quát
            else if (currentStep.Index == 6) // Step 6 - PCB Staff điền kết quả
            {
                var formData = new List<RequestField>();

                // Xử lý kết quả OK/NG
                var ketQua = Request.Form["KetQuaStep6"];
                if (!string.IsNullOrEmpty(ketQua))
                {
                    formData.Add(new RequestField
                    {
                        RequestID = request.RequestID,
                        Key = "KetQuaStep6",
                        Value = ketQua
                    });
                }

                // SỬA: LUÔN xử lý NG số (không phụ thuộc vào kết quả)
                var ngR = Request.Form["NGSo_R_Step6"];
                var ngN = Request.Form["NGSo_N_Step6"];
                var noiDungNG = Request.Form["NoiDungNG_Step6"];

                if (!string.IsNullOrEmpty(ngR))
                {
                    formData.Add(new RequestField
                    {
                        RequestID = request.RequestID,
                        Key = "NGSo_R_Step6",
                        Value = ngR
                    });
                }

                if (!string.IsNullOrEmpty(ngN))
                {
                    formData.Add(new RequestField
                    {
                        RequestID = request.RequestID,
                        Key = "NGSo_N_Step6",
                        Value = ngN
                    });
                }

                if (!string.IsNullOrEmpty(noiDungNG))
                {
                    formData.Add(new RequestField
                    {
                        RequestID = request.RequestID,
                        Key = "NoiDungNG_Step6",
                        Value = noiDungNG
                    });
                }

                // Cập nhật fields - remove existing Step 6 fields first
                var allStep6Keys = new[] { "KetQuaStep6", "NGSo_R_Step6", "NGSo_N_Step6", "NoiDungNG_Step6" };
                var existingFields = request.Fields.Where(f => !allStep6Keys.Contains(f.Key)).ToList();
                request.Fields.Clear();

                foreach (var field in existingFields.Concat(formData))
                {
                    request.Fields.Add(field);
                }
            }
            // THÊM: Xử lý dữ liệu form cho Step 8
            else if (currentStep.Index == 8) // Step 8 - EE Staff điền form
            {
                var formData = new List<RequestField>();

                // Xử lý 2 checkbox chính
                var checkboxKeys = new[] { "TinhNangStep8", "NgoaiQuanStep8" };
                foreach (var key in checkboxKeys)
                {
                    var value = Request.Form[key];
                    formData.Add(new RequestField
                    {
                        RequestID = request.RequestID,
                        Key = key,
                        Value = value == "true" ? "true" : "false"
                    });
                }

                // SỬA: Xử lý 2 comment KTTB và KTLM - chỉ thêm nếu có nội dung
                var commentKeys = new[] { "CommentKTTB_Step8", "CommentKTLM_Step8" };
                foreach (var key in commentKeys)
                {
                    var value = Request.Form[key];
                    if (!string.IsNullOrEmpty(value))
                    {
                        formData.Add(new RequestField
                        {
                            RequestID = request.RequestID,
                            Key = key,
                            Value = value
                        });
                    }
                }

                // SỬA: Xử lý dữ liệu bảng 1: Kiểm tra toàn bộ - chỉ thêm nếu có nội dung
                var toanBoKeys = new[] { "NGSo_R_ToanBo_Step8", "OKSo_N_ToanBo_Step8", "NoiDungNG_ToanBo_Step8" };
                foreach (var key in toanBoKeys)
                {
                    var value = Request.Form[key];
                    if (!string.IsNullOrEmpty(value))
                    {
                        formData.Add(new RequestField
                        {
                            RequestID = request.RequestID,
                            Key = key,
                            Value = value
                        });
                    }
                }

                // SỬA: Xử lý dữ liệu bảng 2: Kiểm tra lấy mẫu - chỉ thêm nếu có nội dung
                var layMauKeys = new[] { "NGSo_R_LayMau_Step8", "OKSo_N_LayMau_Step8", "NoiDungNG_LayMau_Step8" };
                foreach (var key in layMauKeys)
                {
                    var value = Request.Form[key];
                    if (!string.IsNullOrEmpty(value))
                    {
                        formData.Add(new RequestField
                        {
                            RequestID = request.RequestID,
                            Key = key,
                            Value = value
                        });
                    }
                }

                // Cập nhật fields - remove existing Step 8 fields first (bỏ CommentStep8)
                var allStep8Keys = checkboxKeys.Concat(commentKeys).Concat(toanBoKeys).Concat(layMauKeys).ToHashSet();
                var existingFields = request.Fields.Where(f => !allStep8Keys.Contains(f.Key)).ToList();
                request.Fields.Clear();

                foreach (var field in existingFields.Concat(formData))
                {
                    request.Fields.Add(field);
                }
            }

            // Advance workflow
            var oldPhase = request.CurrentPhase;
            _wf.AdvanceStep(request, currentUser.ADID, comment ?? ""); // SỬA: Cho phép comment rỗng

            _fs.SaveRequest(request);

            var excelPath = Server.MapPath($"~/App_Data/Requests/{request.RequestID}/request.xlsx");
            _ex.FillFields(excelPath, request);

            SendEmailForNextStep(request);
        }

        private void ProcessRejectAction(Request request, User currentUser, WorkflowStep currentStep, string comment)
        {
            // Đánh dấu request bị từ chối
            request.IsRejected = true;

            // Cập nhật step hiện tại với thông tin từ chối
            currentStep.Status = "Rejected";
            currentStep.ApproverADID = currentUser.ADID;
            currentStep.Comment = comment ?? "";
            currentStep.ApprovedAt = DateTime.Now;

            // Lưu request với trạng thái bị từ chối
            _fs.SaveRequest(request);

            // SỬA: LUÔN GỬI CẢ 2 EMAIL - KHÔNG PHỤ THUỘC VÀO LOGIC PHỨC TẠP
            try
            {
                System.Diagnostics.Debug.WriteLine($"[ApprovalController] Starting reject notifications for request {request.RequestID}, step {currentStep.Index}");

                // 1. LUÔN gửi email thông báo từ chối (màu đỏ) cho người tạo đơn
                System.Diagnostics.Debug.WriteLine($"[ApprovalController] Sending reject notification to request creator");
                _email.SendRejectNotification(request, currentUser, comment ?? "Không có lý do cụ thể");

                // 2. Tìm người có thể sửa đơn và gửi email yêu cầu sửa đổi (màu vàng)
                User targetUser = null;

                // SỬA: Logic đơn giản hơn để tìm target user
                if (currentStep.Index > 0)
                {
                    // Tìm step trước step bị từ chối
                    var prevStep = request.History?.FirstOrDefault(h => h.Index == currentStep.Index - 1 && h.Status == "Completed");
                    if (prevStep != null && !string.IsNullOrEmpty(prevStep.ApproverADID))
                    {
                        targetUser = _context.Users.FirstOrDefault(u => u.ADID == prevStep.ApproverADID);
                        System.Diagnostics.Debug.WriteLine($"[ApprovalController] Found prev step approver: {targetUser?.Name} ({targetUser?.Email}) from step {prevStep.Index}");
                    }
                }

                // SỬA: Fallback cuối cùng - sử dụng hardcode email
                if (targetUser == null || string.IsNullOrEmpty(targetUser.Email))
                {
                    // Tạo fake user với email hardcode dựa trên step bị từ chối
                    var fallbackEmail = GetFallbackEmailForStep(currentStep.Index - 1);
                    targetUser = new User
                    {
                        ADID = "FALLBACK",
                        Name = $"Fallback User Step {currentStep.Index - 1}",
                        Email = fallbackEmail,
                        DeptCode = currentStep.DeptCode
                    };
                    System.Diagnostics.Debug.WriteLine($"[ApprovalController] Using fallback email: {fallbackEmail}");
                }

                // Gửi email yêu cầu sửa đổi
                if (targetUser != null && !string.IsNullOrEmpty(targetUser.Email))
                {
                    var editUrl = Url.Action("Edit", "Request", new { id = request.RequestID }, protocol: Request.Url.Scheme);
                    System.Diagnostics.Debug.WriteLine($"[ApprovalController] Sending edit notification to: {targetUser.Email}");

                    _email.SendRejectNotificationToPrevApprover(request, targetUser, currentUser, comment ?? "Không có lý do cụ thể", editUrl);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ApprovalController] ERROR: No target user found for edit notification");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ApprovalController] Error sending reject notifications: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ApprovalController] Stack trace: {ex.StackTrace}");
            }
        }

        // SỬA: Thêm helper method để lấy fallback email
        private string GetFallbackEmailForStep(int stepIndex)
        {
            // Sử dụng hardcode email giống như trong EmailService
            switch (stepIndex)
            {
                case 0: return "Hoangthi.Minh@brother-bivn.com.vn"; // Step 0 creator
                case 1: return "Doan.PhamCong@brother-bivn.com.vn"; // Step 1
                case 2: return "Ly.NguyenThi@brother-bivn.com.vn"; // Step 2
                case 3: return "jun.sato@brother-bivn.com.vn"; // Step 3
                case 4: return "Ly.NguyenThi@brother-bivn.com.vn"; // Step 4
                case 5: return "jun.sato@brother-bivn.com.vn"; // Step 5
                case 6: return "Ly.NguyenThi@brother-bivn.com.vn"; // Step 6
                case 7: return "jun.sato@brother-bivn.com.vn"; // Step 7
                case 8: return "nguyenthi.duyen5@brother-bivn.com.vn"; // Step 8
                case 9: return "Doan.PhamCong@brother-bivn.com.vn"; // Step 9
                default: return "phamduc.anh@brother-bivn.com.vn";
            }
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
            return _wf.GetPhaseByStepIndex(stepIndex);
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

        [HttpPost]
        public ActionResult UpdateStep2Preview(string RequestID, string DaNhanHangTestRun, string Comments)
        {
            try
            {
                var request = _fs.LoadRequest(RequestID);
                if (request == null)
                {
                    return Json(new { success = false, message = "Request not found" });
                }

                // Tạo temporary request với dữ liệu step 2 mới
                var tempRequest = new Request
                {
                    RequestID = request.RequestID,
                    Fields = new List<RequestField>(request.Fields),
                    History = request.History
                };

                // Cập nhật hoặc thêm field cho Step 2
                var step2Fields = tempRequest.Fields.Where(f => f.Key == "DaNhanHangTestRun" || f.Key == "CommentStep2").ToList();
                foreach (var field in step2Fields)
                {
                    tempRequest.Fields.Remove(field);
                }

                // Thêm dữ liệu mới
                tempRequest.Fields.Add(new RequestField
                {
                    RequestID = RequestID,
                    Key = "DaNhanHangTestRun",
                    Value = DaNhanHangTestRun == "true" ? "true" : "false"
                });

                if (!string.IsNullOrEmpty(Comments))
                {
                    tempRequest.Fields.Add(new RequestField
                    {
                        RequestID = RequestID,
                        Key = "CommentStep2",
                        Value = Comments
                    });
                }

                // Cập nhật Excel với dữ liệu tạm thời
                var excelPath = Server.MapPath($"~/App_Data/Requests/{RequestID}/request.xlsx");
                _ex.FillFields(excelPath, tempRequest);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public ActionResult UpdateStep4Preview(string RequestID, FormCollection form)
        {
            try
            {
                var request = _fs.LoadRequest(RequestID);
                if (request == null)
                {
                    return Json(new { success = false, message = "Request not found" });
                }

                // Tạo temporary request với dữ liệu step 4 mới
                var tempRequest = new Request
                {
                    RequestID = request.RequestID,
                    Fields = new List<RequestField>(request.Fields),
                    History = request.History
                };

                // Remove existing Step 4 fields
                var step4Keys = new[] {
                    "ThongTin1Step4", "ThongTin2Step4", "ThongTin3Step4", "ThongTin4Step4",
                    "ThongTin5Step4", "ThongTin6Step4", "ThongTin7Step4",
                    "LapRapStep4", "TinhNangStep4", "NgoaiQuanStep4", "CommentStep4"
                };

                var step4Fields = tempRequest.Fields.Where(f => step4Keys.Contains(f.Key)).ToList();
                foreach (var field in step4Fields)
                {
                    tempRequest.Fields.Remove(field);
                }

                // SỬA: Thêm tất cả fields, kể cả empty values để đảm bảo override
                foreach (var key in step4Keys)
                {
                    var value = form[key] ?? ""; // Lấy value từ form, default là empty string

                    // Đặc biệt xử lý checkbox
                    if (key.Contains("LapRap") || key.Contains("TinhNang") || key.Contains("NgoaiQuan"))
                    {
                        if (key.EndsWith("Step4") && !key.Contains("Comment"))
                        {
                            value = value == "true" ? "true" : "false";
                        }
                    }

                    tempRequest.Fields.Add(new RequestField
                    {
                        RequestID = RequestID,
                        Key = key,
                        Value = value
                    });
                }

                // Cập nhật Excel với dữ liệu tạm thời
                var excelPath = Server.MapPath($"~/App_Data/Requests/{RequestID}/request.xlsx");
                _ex.FillFields(excelPath, tempRequest);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public ActionResult UpdateStep5Preview(string RequestID, FormCollection form)
        {
            try
            {
                var request = _fs.LoadRequest(RequestID);
                if (request == null)
                {
                    return Json(new { success = false, message = "Request not found" });
                }

                // Tạo temporary request với dữ liệu Step 5 mới
                var tempRequest = new Request
                {
                    RequestID = request.RequestID,
                    Fields = new List<RequestField>(request.Fields),
                    History = request.History
                };

                // Xóa các field Step 5 cũ
                var step5Keys = new[] { "LapRapStep5", "TinhNangStep5", "NgoaiQuanStep5", "CommentStep5" };
                var step5Fields = tempRequest.Fields.Where(f => step5Keys.Contains(f.Key)).ToList();
                foreach (var field in step5Fields) tempRequest.Fields.Remove(field);

                // Chuẩn hóa checkbox và comment từ form
                Func<string, string> cb = k =>
                {
                    var v = form[k];
                    return (v == "true" || v == "on" || v == "1") ? "true" : "false";
                };

                tempRequest.Fields.Add(new RequestField { RequestID = RequestID, Key = "LapRapStep5", Value = cb("LapRapStep5") });
                tempRequest.Fields.Add(new RequestField { RequestID = RequestID, Key = "TinhNangStep5", Value = cb("TinhNangStep5") });
                tempRequest.Fields.Add(new RequestField { RequestID = RequestID, Key = "NgoaiQuanStep5", Value = cb("NgoaiQuanStep5") });

                var cmt = form["CommentStep5"] ?? "";
                if (!string.IsNullOrWhiteSpace(cmt))
                {
                    tempRequest.Fields.Add(new RequestField { RequestID = RequestID, Key = "CommentStep5", Value = cmt });
                }

                // Cập nhật Excel với dữ liệu tạm thời (không lưu vào request)
                var excelPath = Server.MapPath($"~/App_Data/Requests/{RequestID}/request.xlsx");
                _ex.FillFields(excelPath, tempRequest);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ApprovalController] UpdateStep5Preview error: {ex.Message}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // THÊM: Method cho Step 6 preview
        [HttpPost]
        public ActionResult UpdateStep6Preview(string RequestID, FormCollection form)
        {
            try
            {
                var request = _fs.LoadRequest(RequestID);
                if (request == null)
                {
                    return Json(new { success = false, message = "Request not found" });
                }

                // Tạo temporary request với dữ liệu step 6 mới
                var tempRequest = new Request
                {
                    RequestID = request.RequestID,
                    Fields = new List<RequestField>(request.Fields),
                    History = request.History
                };

                // Remove existing Step 6 fields
                var step6Keys = new[] { "KetQuaStep6", "NGSo_R_Step6", "NGSo_N_Step6", "NoiDungNG_Step6" }; // Bỏ CommentStep6

                var step6Fields = tempRequest.Fields.Where(f => step6Keys.Contains(f.Key)).ToList();
                foreach (var field in step6Fields)
                {
                    tempRequest.Fields.Remove(field);
                }

                // Thêm tất cả fields mới
                foreach (var key in step6Keys)
                {
                    var value = form[key] ?? "";

                    tempRequest.Fields.Add(new RequestField
                    {
                        RequestID = RequestID,
                        Key = key,
                        Value = value
                    });
                }

                // Cập nhật Excel với dữ liệu tạm thời
                var excelPath = Server.MapPath($"~/App_Data/Requests/{RequestID}/request.xlsx");
                _ex.FillFields(excelPath, tempRequest);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // THÊM: Method cho Step 8 preview
        [HttpPost]
        public ActionResult UpdateStep8Preview(string RequestID, FormCollection form)
        {
            try
            {
                var request = _fs.LoadRequest(RequestID);
                if (request == null)
                {
                    return Json(new { success = false, message = "Request not found" });
                }

                // Tạo temporary request với dữ liệu step 8 mới
                var tempRequest = new Request
                {
                    RequestID = request.RequestID,
                    Fields = new List<RequestField>(request.Fields),
                    History = request.History
                };

                var step8Keys = new[] {
                    "TinhNangStep8", "NgoaiQuanStep8",
                    "CommentKTTB_Step8", "CommentKTLM_Step8",
                    "NGSo_R_ToanBo_Step8", "OKSo_N_ToanBo_Step8", "NoiDungNG_ToanBo_Step8",
                    "NGSo_R_LayMau_Step8", "OKSo_N_LayMau_Step8", "NoiDungNG_LayMau_Step8"
                };

                var step8Fields = tempRequest.Fields.Where(f => step8Keys.Contains(f.Key)).ToList();
                foreach (var field in step8Fields)
                {
                    tempRequest.Fields.Remove(field);
                }

                // Thêm tất cả fields mới
                foreach (var key in step8Keys)
                {
                    var value = form[key] ?? "";

                    // Đặc biệt xử lý checkbox
                    if (key.Contains("TinhNang") || key.Contains("NgoaiQuan"))
                    {
                        if (key.EndsWith("Step8"))
                        {
                            value = value == "true" ? "true" : "false";
                        }
                    }

                    tempRequest.Fields.Add(new RequestField
                    {
                        RequestID = RequestID,
                        Key = key,
                        Value = value
                    });
                }

                // Cập nhật Excel với dữ liệu tạm thời
                var excelPath = Server.MapPath($"~/App_Data/Requests/{RequestID}/request.xlsx");
                _ex.FillFields(excelPath, tempRequest);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ===== HELPER METHODS =====

        /// <summary>
        /// Chỉ các chức vụ quản lý sơ cấp, quản lý trung cấp, G.M mới được phê duyệt
        /// </summary>
        private bool CanApprove(User user)
        {
            if (user == null || string.IsNullOrEmpty(user.Title))
            {
                return false;
            }

            var title = user.Title.Trim();

            bool result = title.Equals("Quản lý sơ cấp", StringComparison.OrdinalIgnoreCase) ||
                         title.Equals("Quản lý trung cấp", StringComparison.OrdinalIgnoreCase) ||
                         title.Equals("G.M", StringComparison.OrdinalIgnoreCase);

            return result;
        }

        private bool CanEdit(User user, Request request)
        {
            if (user == null || string.IsNullOrEmpty(user.Title) || request == null)
            {
                return false;
            }

            var title = user.Title.Trim();
            bool isStaff = title.Equals("Staff", StringComparison.OrdinalIgnoreCase);

            // Staff có thể edit nếu:
            // 1. Là owner của request, HOẶC
            // 2. Thuộc cùng phòng ban với step hiện tại (để xử lý cross-department workflow)
            var currentStep = _wf.GetCurrentStep(request);
            bool isOwner = request.CreatedByADID == user.ADID;
            bool isSameDept = currentStep != null && currentStep.DeptCode == user.DeptCode;

            return isStaff && (isOwner || isSameDept);
        }

        private void SendEmailForNextStep(Request request)
        {
            var nextStep = _wf.GetCurrentStep(request);
            if (nextStep == null) return;

            if (!string.IsNullOrEmpty(nextStep.NextApproverADID))
            {
                // Sử dụng SQL Database thay vì JSON
                var specificUser = _context.Users.FirstOrDefault(u => u.ADID == nextStep.NextApproverADID);
                if (specificUser != null)
                {
                    var url = Url.Action("ProcessRequest", "Approval", new { id = request.RequestID }, protocol: Request.Url.Scheme);
                    _email.SendApprovalRequest(request, nextStep, url, false);
                    return;
                }
            }

            // Sử dụng SQL Database thay vì JSON
            var recipients = _context.Users.Where(u =>
                u.DeptCode == nextStep.DeptCode && u.Title == nextStep.Role
            ).ToList();

            if (recipients.Any())
            {
                var url = Url.Action("ProcessRequest", "Approval", new { id = request.RequestID }, protocol: Request.Url.Scheme);
                _email.SendApprovalRequest(request, nextStep, url, false);
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