using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Hosting;
using TrueTestRun.Models;
using System.Data.Entity;

namespace TrueTestRun.Services
{
    public class FileStorageService
    {
        private readonly string templatePath = HostingEnvironment.MapPath("~/App_Data/Data.xlsx");
        private readonly string requestRoot = HostingEnvironment.MapPath("~/App_Data/Requests");

        // Add DbContext property
        private TrueTestRunDbContext _context;

        public FileStorageService()
        {
            _context = new TrueTestRunDbContext();
        }

        public List<User> LoadUsers()
        {
            // Use Entity Framework instead of JSON file
            return _context.Users.ToList();
        }

        public void CreateRequestFolder(string requestId)
        {
            var dir = Path.Combine(requestRoot, requestId);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // Copy template.xlsx → request.xlsx
            File.Copy(templatePath, Path.Combine(dir, "request.xlsx"), true);
        }

        public Request LoadRequest(string requestId)
        {
            // Use Entity Framework instead of JSON
            var req = _context.Requests
                .Include(r => r.Fields)
                .Include(r => r.History)
                .FirstOrDefault(r => r.RequestID == requestId);

            // SẮP XẾP LẠI HISTORY THEO INDEX - QUAN TRỌNG!
            if (req != null && req.History != null)
            {
                // Sắp xếp history theo Index và đảm bảo nó là List để tương thích với View
                var sortedHistory = req.History.OrderBy(h => h.Index).ToList();
                req.History.Clear();
                foreach (var step in sortedHistory)
                {
                    req.History.Add(step);
                }
            }
            return req;
        }

        public void SaveRequest(Request req)
        {
            // Đảm bảo tất cả RequestField có RequestID
            if (req.Fields != null)
            {
                foreach (var field in req.Fields)
                {
                    if (string.IsNullOrEmpty(field.RequestID))
                    {
                        field.RequestID = req.RequestID;
                    }
                }
            }

            // Đảm bảo tất cả WorkflowStep có RequestID
            if (req.History != null)
            {
                foreach (var step in req.History)
                {
                    if (string.IsNullOrEmpty(step.RequestID))
                    {
                        step.RequestID = req.RequestID;
                    }
                }
            }

            // Use Entity Framework instead of JSON
            var existing = _context.Requests
                .Include(r => r.Fields)
                .Include(r => r.History)
                .FirstOrDefault(r => r.RequestID == req.RequestID);

            if (existing == null)
            {
                // Thêm request mới
                _context.Requests.Add(req);
            }
            else
            {
                // Cập nhật request hiện có
                _context.Entry(existing).CurrentValues.SetValues(req);

                // SỬA LỖI: Cập nhật Fields collection đúng cách
                // Xóa các field cũ
                var existingFields = existing.Fields.ToList();
                foreach (var field in existingFields)
                {
                    _context.RequestFields.Remove(field);
                }

                // Thêm các field mới
                foreach (var field in req.Fields)
                {
                    field.RequestID = req.RequestID; // Đảm bảo RequestID được set
                    _context.RequestFields.Add(field);
                }

                // SỬA LỖI: Cập nhật History collection THÔNG MINH HƠN
                // Thay vì xóa và tạo mới, hãy update existing steps
                var existingSteps = existing.History.OrderBy(h => h.Index).ToList();
                var newSteps = req.History.OrderBy(h => h.Index).ToList();

                // Cập nhật các step hiện có
                for (int i = 0; i < Math.Max(existingSteps.Count, newSteps.Count); i++)
                {
                    if (i < existingSteps.Count && i < newSteps.Count)
                    {
                        // Update existing step
                        var existingStep = existingSteps[i];
                        var newStep = newSteps[i];
                        
                        _context.Entry(existingStep).CurrentValues.SetValues(newStep);
                        existingStep.RequestID = req.RequestID; // Đảm bảo RequestID không bị thay đổi
                    }
                    else if (i >= existingSteps.Count)
                    {
                        // Add new step
                        var newStep = newSteps[i];
                        newStep.RequestID = req.RequestID;
                        _context.WorkflowSteps.Add(newStep);
                    }
                    else if (i >= newSteps.Count)
                    {
                        // Remove extra step
                        _context.WorkflowSteps.Remove(existingSteps[i]);
                    }
                }
            }

            try
            {
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Lỗi khi lưu request {req.RequestID}: {ex.Message}", ex);
            }
        }

        public List<Request> LoadAllRequests()
        {
            // Use Entity Framework instead of JSON
            var requests = _context.Requests
                .Include(r => r.Fields)
                .Include(r => r.History)
                .ToList();

            // Đảm bảo History được sắp xếp đúng cho tất cả requests
            foreach (var req in requests)
            {
                if (req.History != null)
                {
                    var sortedHistory = req.History.OrderBy(h => h.Index).ToList();
                    req.History.Clear();
                    foreach (var step in sortedHistory)
                    {
                        req.History.Add(step);
                    }
                }
            }

            // Clean up old completed requests (optional)
            CleanupOldCompletedRequests(requests);

            return requests;
        }

        /// <summary>
        /// Clean up old completed requests from database and file system
        /// </summary>
        private void CleanupOldCompletedRequests(List<Request> requests)
        {
            var now = DateTime.Now;
            var oldRequests = requests.Where(r => r.IsCompleted && (now - r.CreatedAt).TotalDays > 30).ToList();

            foreach (var req in oldRequests)
            {
                // Remove from database
                var dbRequest = _context.Requests.Find(req.RequestID);
                if (dbRequest != null)
                {
                    _context.Requests.Remove(dbRequest);
                }

                // Remove folder from file system
                var folder = Path.Combine(requestRoot, req.RequestID);
                try
                {
                    if (Directory.Exists(folder))
                        Directory.Delete(folder, true);
                }
                catch { /* Log if needed */ }
            }

            _context.SaveChanges();
        }

        // Dispose pattern for DbContext
        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}