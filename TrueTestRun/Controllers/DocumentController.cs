using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using TrueTestRun.Models;
using TrueTestRun.Services;

namespace TrueTestRun.Controllers
{
    [Authorize]
    public class DocumentController : Controller
    {
        private readonly DocumentService _documentService;
        private readonly FileStorageService _fileStorageService;

        public DocumentController()
        {
            _documentService = new DocumentService();
            _fileStorageService = new FileStorageService();
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

        /// <summary>
        /// Upload tài liệu
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Upload(string requestId, HttpPostedFileBase file, string description)
        {
            var currentUser = Session["CurrentUser"] as User;
            if (currentUser == null)
            {
                return Json(new { success = false, message = GetResourceString("PleaseLoginAgain") });
            }

            // Kiểm tra request có tồn tại không
            var request = _fileStorageService.LoadRequest(requestId);
            if (request == null)
            {
                return Json(new { success = false, message = GetResourceString("RequestNotFound") });
            }

            if (file == null || file.ContentLength == 0)
            {
                return Json(new { success = false, message = GetResourceString("PleaseSelectFile") });
            }

            // Kiểm tra kích thước file (max 10MB)
            if (file.ContentLength > 10 * 1024 * 1024)
            {
                return Json(new { success = false, message = GetResourceString("FileTooLargeSpecific") });
            }

            var result = _documentService.UploadDocument(requestId, file, currentUser.ADID, description);

            if (result)
            {
                return Json(new { success = true, message = GetResourceString("UploadDocumentSuccess") });
            }
            else
            {
                return Json(new { success = false, message = GetResourceString("UploadDocumentError") });
            }
        }

        /// <summary>
        /// Lấy danh sách tài liệu
        /// </summary>
        public ActionResult GetDocuments(string requestId)
        {
            try
            {
                if (string.IsNullOrEmpty(requestId))
                {
                    return Json(new { success = false, message = GetResourceString("InvalidRequestID") }, JsonRequestBehavior.AllowGet);
                }

                // Kiểm tra request có tồn tại không
                var request = _fileStorageService.LoadRequest(requestId);
                if (request == null)
                {
                    return Json(new { success = false, message = GetResourceString("RequestNotFoundWithID").Replace("{0}", requestId) }, JsonRequestBehavior.AllowGet);
                }

                var documents = _documentService.GetDocuments(requestId);
                var result = documents.Select(d => new
                {
                    documentId = d.DocumentID,
                    fileName = d.OriginalFileName,
                    fileSize = _documentService.FormatFileSize(d.FileSize),
                    uploadedBy = d.UploadedByADID,
                    uploadedAt = d.UploadedAt.ToString("dd/MM/yyyy HH:mm"),
                    description = d.Description,
                    contentType = d.ContentType
                }).ToList();

                return Json(new { success = true, documents = result }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = GetResourceString("ServerError").Replace("{0}", ex.Message) }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Xem/Download tài liệu
        /// </summary>
        public ActionResult View(int id)
        {
            try
            {
                var document = _documentService.GetDocument(id);
                if (document == null)
                {
                    return HttpNotFound(GetResourceString("DocumentNotFound"));
                }

                var fileStream = _documentService.GetFileStream(id);
                if (fileStream == null)
                {
                    return HttpNotFound(GetResourceString("FileNotExists"));
                }

                // Xác định xem có thể hiển thị inline không
                var inlineTypes = new[] { "application/pdf", "image/jpeg", "image/png", "image/gif", "image/webp", "text/plain", "text/html" };
                var isInlineViewable = inlineTypes.Contains(document.ContentType.ToLower());
                
                // Set Content-Disposition header
                var disposition = isInlineViewable ? "inline" : "attachment";
                Response.Headers.Add("Content-Disposition", $"{disposition}; filename=\"{document.OriginalFileName}\"");
                
                return File(fileStream, document.ContentType);
            }
            catch (Exception)
            {
                return HttpNotFound(GetResourceString("ErrorLoadingFile"));
            }
        }

        /// <summary>
        /// Xóa tài liệu
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var currentUser = Session["CurrentUser"] as User;
            if (currentUser == null)
            {
                return Json(new { success = false, message = GetResourceString("PleaseLoginAgain") });
            }

            var result = _documentService.DeleteDocument(id, currentUser.ADID);

            if (result)
            {
                return Json(new { success = true, message = GetResourceString("DeleteDocumentSuccess") });
            }
            else
            {
                return Json(new { success = false, message = GetResourceString("DeleteDocumentError") });
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _documentService?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}