using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace TrueTestRun.Models
{
        public class Request
        {
            public string RequestID { get; set; }                // vd: EE-24-0098
            public DateTime CreatedAt { get; set; }
            public string CreatedByADID { get; set; }
            public Dictionary<string, string> Fields { get; set; }
                = new Dictionary<string, string>();
        //public List<ApprovalStep> History { get; set; }
        //    = new List<ApprovalStep>();
        public int CurrentStepIndex { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsRejected { get; set; }
        public List<WorkflowStep> History { get; set; }
        public TestRunPhase CurrentPhase { get; set; }

        // Có thể lưu riêng từng danh sách step nếu muốn mở rộng sau này
        public List<WorkflowStep> TruocTestRunSteps { get; set; }
        public List<WorkflowStep> GiuaTestRunSteps { get; set; }
        public List<WorkflowStep> SauTestRunSteps { get; set; }


    }
    }

    