using System;
using System.Collections.Generic;
using System.Linq;
using TrueTestRun.Models;

namespace TrueTestRun.Services
{
    public class WorkflowService
    {
        private readonly List<WorkflowStep> _workflowTemplate = new List<WorkflowStep>
{
    // Bước 1 (Index 0): Staff phòng EE tạo đơn, sau đó gửi cho QLTC cùng phòng.
    new WorkflowStep {
        Index = 0, StepName = "Staff tạo đơn (EE)",
        Actor = StepActor.DataEntry, ActionType = StepActionType.CreateForm,
        DeptCode = "EPE-EE", Role = "Staff",
        NextApproverDept = "EPE-EE", NextApproverRole = "Quản lý trung cấp"
    },

    // Bước 2 (Index 1): QLTC phòng EE phê duyệt, sau đó gửi lại cho Staff PCB.
    new WorkflowStep {
        Index = 1, StepName = "QLTC phê duyệt lần 1 (EE)",
        Actor = StepActor.Approver, ActionType = StepActionType.ApproveOnly,
        DeptCode = "EPE-EE", Role = "Quản lý trung cấp",
        NextApproverDept = "EPE-PCB", NextApproverRole = "Staff"
    },

    // Bước 3 (Index 2): Staff phòng PCB điền form, sau đó gửi cho QLTC phòng PCB.
    new WorkflowStep {
        Index = 2, StepName = "Staff điền form (gửi cho PCB)",
        Actor = StepActor.DataEntry, ActionType = StepActionType.FillCheckboxesAndNotes,
        DeptCode = "EPE-PCB", Role = "Staff",
        NextApproverDept = "EPE-PCB", NextApproverRole = "Quản lý trung cấp"
    },

    // Bước 4 (Index 3): QLTC phòng PCB phê duyệt, sau đó gửi cho Staff cùng phòng PCB.
    new WorkflowStep {
        Index = 3, StepName = "QLTC phê duyệt lần 2 (PCB)",
        Actor = StepActor.Approver, ActionType = StepActionType.ApproveOnly,
        DeptCode = "EPE-PCB", Role = "Quản lý trung cấp",
        NextApproverDept = "EPE-PCB", NextApproverRole = "Staff"
    },

    // Bước 5 (Index 4): Staff phòng PCB điền form, sau đó gửi cho QLTC cùng phòng PCB.
    new WorkflowStep {
        Index = 4, StepName = "Staff điền form lần 2 (PCB)",
        Actor = StepActor.DataEntry, ActionType = StepActionType.FillCheckboxesAndNotes,
        DeptCode = "EPE-PCB", Role = "Staff",
        NextApproverDept = "EPE-PCB", NextApproverRole = "Quản lý trung cấp"
    },

    // Bước 6 (Index 5): QLTC phòng PCB phê duyệt, sau đó gửi cho Staff phòng EE.
    new WorkflowStep {
        Index = 5, StepName = "QLTC phê duyệt lần 3 (PCB)",
        Actor = StepActor.Approver, ActionType = StepActionType.ApproveOnly,
        DeptCode = "EPE-PCB", Role = "Quản lý trung cấp",
        NextApproverDept = "EPE-PCB", NextApproverRole = "Staff"
    },

    // Bước 7 (Index 6): Staff phòng PCB điền ghi chú theo mẫu, sau đó gửi cho QLTC PCB
    new WorkflowStep {
        Index = 6, StepName = "Staff điền ghi chú mẫu (EE)",
        Actor = StepActor.DataEntry, ActionType = StepActionType.FillNotesTemplate,
        DeptCode = "EPE-PCB", Role = "Staff",
        NextApproverDept = "EPE-PCB", NextApproverRole = "Quản lý trung cấp"
    },

    // Bước 8 (Index 7): QLTC phòng PCB phê duyệt, sau đó gửi lại cho Staff EE
    new WorkflowStep {
        Index = 7, StepName = "QLTC phê duyệt lần 4 (EE)",
        Actor = StepActor.Approver, ActionType = StepActionType.ApproveOnly,
        DeptCode = "EPE-PCB", Role = "Quản lý trung cấp",
        NextApproverDept = "EPE-EE", NextApproverRole = "Staff"
    },

    // Bước 9 (Index 8): Staff phòng EE điền form phức tạp, sau đó gửi cho QLTC cùng phòng.
    new WorkflowStep {
        Index = 8, StepName = "Staff điền form phức tạp (EE)",
        Actor = StepActor.DataEntry, ActionType = StepActionType.FillComplexForm,
        DeptCode = "EPE-EE", Role = "Staff",
        NextApproverDept = "EPE-EE", NextApproverRole = "Quản lý trung cấp"
    },

    // Bước 10 (Index 9): QLTC phòng EE phê duyệt, sau đó gửi cho G.M.
    new WorkflowStep {
        Index = 9, StepName = "QLTC phê duyệt lần 5 (gửi G.M)",
        Actor = StepActor.Approver, ActionType = StepActionType.ApproveOnly,
        DeptCode = "EPE-EE", Role = "Quản lý trung cấp",
        NextApproverDept = "EPE-G.M", NextApproverRole = "G.M"
    },

    // Bước 11 (Index 10): G.M phê duyệt cuối cùng. Quy trình kết thúc.
    new WorkflowStep {
        Index = 10, StepName = "G.M phê duyệt cuối cùng",
        Actor = StepActor.Approver, ActionType = StepActionType.ApproveOnly,
        DeptCode = "EPE-G.M", Role = "G.M",
        NextApproverDept = null, NextApproverRole = null // Không có bước tiếp theo
    }
};
        public List<WorkflowStep> GetStepsByPhase(TestRunPhase phase)
        {
            switch (phase)
            {
                case TestRunPhase.TruocTestRun:
                // return danh sách các bước trước test run
                case TestRunPhase.GiuaTestRun:
                // return danh sách các bước giữa test run
                case TestRunPhase.SauTestRun:
                // return danh sách các bước sau test run
                default:
                    return new List<WorkflowStep>();
            }
        }

        public List<WorkflowStep> InitHistory()
        {
            return _workflowTemplate.Select(s => new WorkflowStep
            {
                Index = s.Index,
                StepName = s.StepName,
                Actor = s.Actor,
                ActionType = s.ActionType,
                DeptCode = s.DeptCode,
                Role = s.Role,
                NextApproverDept = s.NextApproverDept,
                NextApproverRole = s.NextApproverRole,
                Status = s.Index == 0 ? "Processing" : "Pending"
            }).ToList();
        }

        public WorkflowStep GetCurrentStep(Request request)
        {
            if (request == null || request.IsCompleted || request.IsRejected || request.CurrentStepIndex >= request.History.Count)
            {
                return null;
            }
            return request.History[request.CurrentStepIndex];
        }

        public void AdvanceStep(Request request, string approverADID, string comment)
        {
            var currentStep = GetCurrentStep(request);
            if (currentStep == null) return;

            currentStep.Status = "Approved";
            currentStep.ApproverADID = approverADID;
            currentStep.Comment = comment;
            currentStep.ApprovedAt = DateTime.Now;

            request.CurrentStepIndex++;

            if (request.CurrentStepIndex >= request.History.Count)
            {
                request.IsCompleted = true;
            }
            else
            {
                request.History[request.CurrentStepIndex].Status = "Processing";
            }
        }
    }
}