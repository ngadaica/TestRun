using System;

namespace TrueTestRun.Models
{
    public enum StepActor
    {
        DataEntry,
        Approver
    }

    public enum StepActionType
    {
        CreateForm,
        ApproveOnly,
        FillCheckboxesAndNotes,
        FillNotesTemplate,
        FillComplexForm
    }

    public class WorkflowStep
    {
        public int Index { get; set; }
        public string StepName { get; set; }
        public StepActor Actor { get; set; }
        public StepActionType ActionType { get; set; }
        public string DeptCode { get; set; }
        public string Role { get; set; }
        public string NextApproverDept { get; set; }
        public string NextApproverRole { get; set; }
        public string Status { get; set; }
        public string ApproverADID { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string Comment { get; set; }
        public string NextApproverADID { get; set; }
    }
}