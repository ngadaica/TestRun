using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Hosting;
using TrueTestRun.Models;

namespace TrueTestRun.Services
{
    public class FileStorageService
    {
        private readonly string usersPath = HostingEnvironment.MapPath("~/App_Data/users.json");
        private readonly string templatePath = HostingEnvironment.MapPath("~/App_Data/Data.xlsx");
        private readonly string requestRoot = HostingEnvironment.MapPath("~/App_Data/Requests");

        public List<User> LoadUsers()
        {
            if (!File.Exists(usersPath))
                return new List<User>();

            var json = File.ReadAllText(usersPath);
            return JsonConvert.DeserializeObject<List<User>>(json)
                   ?? new List<User>();
        }

        public void SaveUsers(List<User> users)
        {
            var json = JsonConvert.SerializeObject(users, Formatting.Indented);
            File.WriteAllText(usersPath, json);
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
            var jsonPath = Path.Combine(requestRoot, requestId, "request.json");
            if (!File.Exists(jsonPath))
                return null;

            var req = JsonConvert
                .DeserializeObject<Request>(File.ReadAllText(jsonPath));
            if (req == null)
                return null;

            // Đảm bảo không bị null
            req.Fields = req.Fields ?? new Dictionary<string, string>();
            req.History = req.History ?? new List<WorkflowStep>();

            return req;
        }

        public void SaveRequest(Request req)
        {
            var jsonPath = Path.Combine(requestRoot, req.RequestID, "request.json");
            var json = JsonConvert.SerializeObject(req, Formatting.Indented);
            File.WriteAllText(jsonPath, json);
        }

        public List<Request> LoadAllRequests()
        {
            var requests = new List<Request>();
            if (!Directory.Exists(requestRoot)) return requests;

            foreach (var dir in Directory.GetDirectories(requestRoot))
            {
                var id = Path.GetFileName(dir);
                var req = LoadRequest(id);
                if (req != null) requests.Add(req);
            }

            // Dọn dẹp các request đã hoàn thành quá 30 ngày
            CleanupOldCompletedRequests(requests);

            // Trả về danh sách đã lọc
            return requests;
        }

        /// <summary>
        /// Xóa các request đã hoàn thành quá 30 ngày khỏi ổ đĩa
        /// </summary>
        private void CleanupOldCompletedRequests(List<Request> requests)
        {
            var now = DateTime.Now;
            foreach (var req in requests.Where(r => r.IsCompleted && (now - r.CreatedAt).TotalDays > 30).ToList())
            {
                var folder = Path.Combine(requestRoot, req.RequestID);
                try
                {
                    if (Directory.Exists(folder))
                        Directory.Delete(folder, true);
                }
                catch { /* Có thể ghi log nếu cần */ }
            }
        }
    }
}