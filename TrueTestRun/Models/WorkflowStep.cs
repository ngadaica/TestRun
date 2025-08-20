using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TrueTestRun.Models
{
    public enum StepActor
    {
        DataEntry,
        Approver
    }

    public class WorkflowStep
    {
        [Key]
        public int Id { get; set; }

        public int Index { get; set; }

        [MaxLength(255)]
        public string StepName { get; set; }

        public StepActor Actor { get; set; }

        [MaxLength(50)]
        public string DeptCode { get; set; }

        [MaxLength(100)]
        public string Role { get; set; }

        [MaxLength(50)]
        public string NextApproverDept { get; set; }

        [MaxLength(100)]
        public string NextApproverRole { get; set; }

        [MaxLength(50)]
        public string Status { get; set; } = "Processing";

        [MaxLength(50)]
        public string ApproverADID { get; set; }

        public DateTime? ApprovedAt { get; set; }

        [MaxLength(4000)]
        public string Comment { get; set; }

        [MaxLength(50)]
        public string NextApproverADID { get; set; }

        [ForeignKey("Request")]
        [MaxLength(50)]
        public string RequestID { get; set; }
        public virtual Request Request { get; set; }
    }
}