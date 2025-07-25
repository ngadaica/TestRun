using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace TrueTestRun.Models
{
    public enum UserRole
    {
        Admin,
        DataEntry,
        Approver
    }
    public class User
    {
        public string ADID { get; set; }
        public string Name { get; set; }
        public string DeptCode { get; set; }
        public string Group { get; set; }
        public string Title { get; set; }    // "Quản lý sơ cấp" hoặc "Quản lý trung cấp"
        public string Email { get; set; }
        public string Factory { get; set; }
        public string AvatarUrl { get; set; }
        public UserRole Role
        {
            get; set;
        }

        // Sinh ra role dùng cho Approval
        public ApprovalRole ApprovalRole
        {
            get
            {
                return ApprovalRole.Approver;
            }
        }
    }



}