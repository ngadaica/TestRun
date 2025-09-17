using System;
using System.Collections.Generic;
using System.Linq;
using TrueTestRun.Models;
using System.Web;

namespace TrueTestRun.Services
{
    public class WorkflowService
    {
        // Helper method to safely get resource with fallback
        private static string GetResource(string key, string fallback = "")
        {
            try
            {
                var resource = HttpContext.GetGlobalResourceObject("Resources", key) as string;
                return !string.IsNullOrEmpty(resource) ? resource : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private readonly List<WorkflowStep> _workflowTemplate = new List<WorkflowStep>
        {
            // ======= GIAI ĐOẠN TRƯỚC TEST RUN =======
            new WorkflowStep {
                Index = 0, StepName = "Staff (EE)",
                Actor = StepActor.DataEntry,
                DeptCode = "EPE-EE", Role = "Staff",
                NextApproverDept = "EPE-EE", NextApproverRole = "Quản lý trung cấp"
            },

            new WorkflowStep {
                Index = 1, StepName = "QLTC (EE) - " + GetResource("MiddleManager_Ja", "中間管理職"),
                Actor = StepActor.Approver,
                DeptCode = "EPE-EE", Role = "Quản lý trung cấp",
                NextApproverDept = "EPE-PCB", NextApproverRole = "Staff"
            },
            
            // ======= GIAI ĐOẠN GIỮA TEST RUN =======
            new WorkflowStep {
                Index = 2, StepName = "Staff (PCB)",
                Actor = StepActor.DataEntry,
                DeptCode = "EPE-PCB", Role = "Staff",
                NextApproverDept = "EPE-PCB", NextApproverRole = "Quản lý trung cấp"
            },

            new WorkflowStep {
                Index = 3, StepName = "QLTC (PCB) - " + GetResource("MiddleManager_Ja", "中間管理職"),
                Actor = StepActor.Approver,
                DeptCode = "EPE-PCB", Role = "Quản lý trung cấp",
                NextApproverDept = "EPE-PCB", NextApproverRole = "Staff"
            },

            new WorkflowStep {
                Index = 4, StepName = "Staff (PCB)", //
                Actor = StepActor.DataEntry,
                DeptCode = "EPE-PCB", Role = "Staff",
                NextApproverDept = "EPE-PCB", NextApproverRole = "Quản lý trung cấp"
            },

            new WorkflowStep {
                Index = 5, StepName = "QLTC (PCB) - " + GetResource("MiddleManager_Ja", "中間管理職"),
                Actor = StepActor.Approver,
                DeptCode = "EPE-PCB", Role = "Quản lý trung cấp",
                NextApproverDept = "EPE-PCB", NextApproverRole = "Staff"
            },
            
            // ======= GIAI ĐOẠN CUỐI TEST RUN - BẮT ĐẦU TỪ INDEX 6 =======
            new WorkflowStep {
                Index = 6, StepName = "Staff (PCB)",
                Actor = StepActor.DataEntry,
                DeptCode = "EPE-PCB", Role = "Staff",
                NextApproverDept = "EPE-PCB", NextApproverRole = "Quản lý trung cấp"
            },

            new WorkflowStep {
                Index = 7, StepName = "QLTC (PCB) - " + GetResource("MiddleManager_Ja", "中間管理職"),
                Actor = StepActor.Approver,
                DeptCode = "EPE-PCB", Role = "Quản lý trung cấp",
                NextApproverDept = "EPE-EE", NextApproverRole = "Staff"
            },

            new WorkflowStep {
                Index = 8, StepName = "Staff (EE)",
                Actor = StepActor.DataEntry,
                DeptCode = "EPE-EE", Role = "Staff",
                NextApproverDept = "EPE-EE", NextApproverRole = "Quản lý trung cấp"
            },

            new WorkflowStep {
                Index = 9, StepName = "QLTC (EE) - " + GetResource("MiddleManager_Ja", "中間管理職"),
                Actor = StepActor.Approver,
                DeptCode = "EPE-EE", Role = "Quản lý trung cấp",
                NextApproverDept = "EPE-G.M", NextApproverRole = "G.M"
            },

            new WorkflowStep {
                Index = 10, StepName = "G.M phê duyệt",
                Actor = StepActor.Approver,
                DeptCode = "EPE-G.M", Role = "G.M",
                NextApproverDept = null, NextApproverRole = null
            }
        };

        public List<WorkflowStep> GetStepsByPhase(TestRunPhase phase)
        {
            switch (phase)
            {
                case TestRunPhase.TruocTestRun:
                    return _workflowTemplate.Where(s => s.Index <= 1).ToList();
                case TestRunPhase.GiuaTestRun:
                    return _workflowTemplate.Where(s => s.Index >= 2 && s.Index <= 5).ToList();
                case TestRunPhase.SauTestRun:
                    return _workflowTemplate.Where(s => s.Index >= 6).ToList();
                default:
                    return new List<WorkflowStep>();
            }
        }

        public TestRunPhase GetPhaseByStepIndex(int stepIndex)
        {
            if (stepIndex <= 1) return TestRunPhase.TruocTestRun;
            if (stepIndex <= 5) return TestRunPhase.GiuaTestRun;
            return TestRunPhase.SauTestRun;
        }

        public void UpdateRequestPhase(Request request)
        {
            if (request == null) return;

            var newPhase = GetPhaseByStepIndex(request.CurrentStepIndex);

            if (request.CurrentPhase != newPhase)
            {
                request.CurrentPhase = newPhase;
                System.Diagnostics.Debug.WriteLine($"[WorkflowService] Request {request.RequestID} phase updated to {newPhase} at step {request.CurrentStepIndex}");
            }
        }

        public List<WorkflowStep> InitHistory()
        {
            var history = new List<WorkflowStep>();

            foreach (var template in _workflowTemplate)
            {
                var step = new WorkflowStep
                {
                    Index = template.Index,
                    StepName = template.StepName,
                    Actor = template.Actor,
                    DeptCode = template.DeptCode,
                    Role = template.Role,
                    NextApproverDept = template.NextApproverDept,
                    NextApproverRole = template.NextApproverRole,
                    Status = "Pending"
                };

                history.Add(step);
            }

            return history;
        }

        public WorkflowStep GetCurrentStep(Request request)
        {
            if (request == null || request.IsCompleted || request.IsRejected)
                return null;

            if (request.CurrentStepIndex < 0 || request.CurrentStepIndex >= request.History.Count)
                return null;

            return request.History.ElementAt(request.CurrentStepIndex);
        }

        public void AdvanceStep(Request request, string approverADID, string comment)
        {
            if (request == null) return;

            var currentStep = GetCurrentStep(request);
            if (currentStep == null) return;

            currentStep.Status = "Approved";
            currentStep.ApproverADID = approverADID;
            currentStep.Comment = comment;
            currentStep.ApprovedAt = DateTime.Now;

            request.CurrentStepIndex++;

            UpdateRequestPhase(request);

            if (request.CurrentStepIndex >= request.History.Count)
            {
                request.IsCompleted = true;
            }
            else
            {
                var nextStep = request.History.ElementAt(request.CurrentStepIndex);
                nextStep.Status = "Processing";
            }
        }

        public WorkflowStep GetNextStep(Request request)
        {
            if (request == null || request.IsCompleted || request.IsRejected)
                return null;

            int nextIndex = request.CurrentStepIndex + 1;
            if (nextIndex >= request.History.Count)
                return null;

            return request.History.ElementAt(nextIndex);
        }
    }
}