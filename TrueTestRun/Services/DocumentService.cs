using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Hosting;
using TrueTestRun.Models;

namespace TrueTestRun.Services
{
    public class DocumentService
    {
        private readonly TrueTestRunDbContext _context;
        private readonly string _documentsPath;

        public DocumentService()
        {
            _context = new TrueTestRunDbContext();
            _documentsPath = HostingEnvironment.MapPath("~/App_Data/Documents");

            // Đảm bảo thư mục Documents tồn tại
            if (!Directory.Exists(_documentsPath))
            {
                Directory.CreateDirectory(_documentsPath);
            }
        }

        /// <summary>
        /// Upload tài liệu cho request
        /// </summary>
        public bool UploadDocument(string requestId, HttpPostedFileBase file, string uploadedByADID, string description = "")
        {
            try
            {
                if (file == null || file.ContentLength == 0)
                {
                    return false;
                }

                // Kiểm tra request tồn tại trước khi upload
                var existingRequest = _context.Requests.FirstOrDefault(r => r.RequestID == requestId);
                if (existingRequest == null)
                {
                    return false;
                }

                // Kiểm tra định dạng file được phép
                var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg", ".txt", ".zip", ".rar" };
                var fileExtension = Path.GetExtension(file.FileName).ToLower();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    return false;
                }

                // Tạo thư mục con cho request
                var requestDocPath = Path.Combine(_documentsPath, requestId);
                if (!Directory.Exists(requestDocPath))
                {
                    Directory.CreateDirectory(requestDocPath);
                }

                // Tạo tên file unique
                var fileName = $"{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(requestDocPath, fileName);

                // Kiểm tra không ghi đè file existing
                while (File.Exists(filePath))
                {
                    fileName = $"{Guid.NewGuid()}{fileExtension}";
                    filePath = Path.Combine(requestDocPath, fileName);
                }

                // Lưu file
                file.SaveAs(filePath);
                
                // Verify file was saved successfully
                if (!File.Exists(filePath))
                {
                    return false;
                }

                // Lưu thông tin vào database
                var document = new RequestDocument
                {
                    RequestID = requestId,
                    FileName = fileName,
                    OriginalFileName = file.FileName,
                    ContentType = file.ContentType,
                    FileSize = file.ContentLength,
                    FilePath = filePath,
                    UploadedByADID = uploadedByADID,
                    UploadedAt = DateTime.Now,
                    Description = description ?? ""
                };

                _context.RequestDocuments.Add(document);
                
                // Improved error handling for database save
                try
                {
                    _context.SaveChanges();
                }
                catch (Exception dbEx)
                {
                    // Clean up file if database save failed
                    try
                    {
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }
                    }
                    catch (Exception)
                    {
                        // Ignore cleanup errors
                    }
                    
                    throw; // Re-throw để caller biết lỗi
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Lấy danh sách tài liệu của request
        /// </summary>
        public List<RequestDocument> GetDocuments(string requestId)
        {
            try
            {
                var documents = _context.RequestDocuments
                    .Where(d => d.RequestID == requestId)
                    .OrderByDescending(d => d.UploadedAt)
                    .ToList();

                return documents;
            }
            catch (Exception)
            {
                return new List<RequestDocument>();
            }
        }

        /// <summary>
        /// Lấy thông tin một tài liệu
        /// </summary>
        public RequestDocument GetDocument(int documentId)
        {
            try
            {
                return _context.RequestDocuments.FirstOrDefault(d => d.DocumentID == documentId);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Xóa tài liệu
        /// </summary>
        public bool DeleteDocument(int documentId, string userADID)
        {
            try
            {
                var document = _context.RequestDocuments.FirstOrDefault(d => d.DocumentID == documentId);
                if (document == null) return false;

                // Chỉ cho phép người upload hoặc admin xóa
                // (Có thể thêm logic phân quyền phức tạp hơn ở đây)

                // Xóa file vật lý
                if (File.Exists(document.FilePath))
                {
                    File.Delete(document.FilePath);
                }

                // Xóa record trong database
                _context.RequestDocuments.Remove(document);
                _context.SaveChanges();

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Lấy file stream để xem/download
        /// </summary>
        public FileStream GetFileStream(int documentId)
        {
            try
            {
                var document = GetDocument(documentId);
                if (document == null || !File.Exists(document.FilePath))
                    return null;

                return new FileStream(document.FilePath, FileMode.Open, FileAccess.Read);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Format file size cho hiển thị
        /// </summary>
        public string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}