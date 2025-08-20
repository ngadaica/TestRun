using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TrueTestRun.Models
{
    [Flags]
    public enum ApprovalRole
    {
        None = 0,
        Reviewer = 1,
        Stampler = 2,
        Approver = Reviewer | Stampler
    }

    public class ApprovalStep
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(50)]
        public string DeptCode { get; set; }

        [MaxLength(50)]
        public string ADID { get; set; }

        public ApprovalRole Role { get; set; }
        public DateTime? Timestamp { get; set; }

        [MaxLength(4000)]
        public string Comment { get; set; }

        [ForeignKey("Request")]
        public string RequestID { get; set; }
        public virtual Request Request { get; set; }
    }
}