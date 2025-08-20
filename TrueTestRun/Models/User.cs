using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
        [Key]
        [MaxLength(50)]
        public string ADID { get; set; }

        [MaxLength(255)]
        public string Name { get; set; }

        [MaxLength(50)]
        public string DeptCode { get; set; }

        [MaxLength(100)]
        public string Group { get; set; }

        [MaxLength(100)]
        public string Title { get; set; }

        [MaxLength(255)]
        public string Email { get; set; }

        [MaxLength(100)]
        public string Factory { get; set; }

        [MaxLength(500)]
        public string AvatarUrl { get; set; }

        public UserRole Role { get; set; }

        // Sinh ra role dùng cho Approval
        [NotMapped]
        public ApprovalRole ApprovalRole
        {
            get
            {
                return ApprovalRole.Approver;
            }
        }
    }
}