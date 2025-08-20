using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TrueTestRun.Models
{
    public enum TestRunPhase
    {
        TruocTestRun = 1,
        GiuaTestRun = 2,
        SauTestRun = 3
    }

    public class Request
    {
        [Key]
        [MaxLength(50)]
        public string RequestID { get; set; }

        public DateTime CreatedAt { get; set; }

        [MaxLength(50)]  
        public string CreatedByADID { get; set; }

        public virtual ICollection<RequestField> Fields { get; set; } = new List<RequestField>();

        // THÊM property này
        public TestRunPhase CurrentPhase { get; set; } = TestRunPhase.TruocTestRun;

        public int CurrentStepIndex { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsRejected { get; set; }

        // Collection cho History
        public virtual ICollection<WorkflowStep> History { get; set; } = new List<WorkflowStep>();
        
        // XÓA các collections riêng lẻ này vì chúng gây confusion
        // public virtual ICollection<WorkflowStep> TruocTestRunSteps { get; set; }
        // public virtual ICollection<WorkflowStep> GiuaTestRunSteps { get; set; }
        // public virtual ICollection<WorkflowStep> SauTestRunSteps { get; set; }
    }

    public class RequestField
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string Key { get; set; }

        [MaxLength(4000)]
        public string Value { get; set; }

        [ForeignKey("Request")]
        [MaxLength(50)]
        public string RequestID { get; set; }
        public virtual Request Request { get; set; }
    }
}