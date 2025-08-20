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
            // ======= GIAI ĐOẠN TRƯỚC TEST RUN =======
            // Bước 1 (Index 0): Staff phòng EE tạo đơn, gồm ghi nhập thông tin ở bên trên và tích ô checkbox, điền comment nếu cần thiết
            new WorkflowStep {
                Index = 0, StepName = "Staff tạo đơn và điền form (EE)",
                Actor = StepActor.DataEntry,
                DeptCode = "EPE-EE", Role = "Staff",
                NextApproverDept = "EPE-EE", NextApproverRole = "Quản lý trung cấp"
            },
            
            // Bước 2 (Index 1): QLTC phòng EE phê duyệt, sau đó gửi lại cho Staff PCB.
            new WorkflowStep {
                Index = 1, StepName = "QLTC phê duyệt lần 1 (EE)",
                Actor = StepActor.Approver,
                DeptCode = "EPE-EE", Role = "Quản lý trung cấp",
                NextApproverDept = "EPE-PCB", NextApproverRole = "Staff"
            },
            
            // ======= GIAI ĐOẠN GIỮA TEST RUN =======
            // Bước 3 (Index 2): Staff phòng PCB điền form, sau đó gửi cho QLSC cùng phòng
            new WorkflowStep {
                Index = 2, StepName = "Staff điền form (PCB)",
                Actor = StepActor.DataEntry,
                DeptCode = "EPE-PCB", Role = "Staff",
                NextApproverDept = "EPE-PCB", NextApproverRole = "Quản lý sơ cấp"
            },
            
            // Bước 4 (Index 3): QLSC phòng PCB phê duyệt, sau đó gửi cho Staff cùng phòng PCB.
            new WorkflowStep {
                Index = 3, StepName = "QLSC phê duyệt lần 1 (PCB)",
                Actor = StepActor.Approver,
                DeptCode = "EPE-PCB", Role = "Quản lý sơ cấp",
                NextApproverDept = "EPE-PCB", NextApproverRole = "Staff"
            },
            
            // Bước 5 (Index 4): Giống bước 1, Staff nhận được đơn qua gmail sẽ ghi nhập ở bên trên và tích ô checkbox, điền comment nếu cần thiết, sau đó gửi cho QLTC
            new WorkflowStep {
                Index = 4, StepName = "Staff tạo đơn và điền form (PCB)",
                Actor = StepActor.DataEntry,
                DeptCode = "EPE-PCB", Role = "Staff",
                NextApproverDept = "EPE-PCB", NextApproverRole = "Quản lý trung cấp"
            },
            
            // Bước 6 (Index 5): QLTC phòng PCB phê duyệt, sau đó gửi cho Staff phòng PCB - CUỐI GIAI ĐOẠN GIỮA
            new WorkflowStep {
                Index = 5, StepName = "QLTC phê duyệt lần 2 (PCB)",
                Actor = StepActor.Approver,
                DeptCode = "EPE-PCB", Role = "Quản lý trung cấp",
                NextApproverDept = "EPE-PCB", NextApproverRole = "Staff"
            },
            
            // ======= GIAI ĐOẠN CUỐI TEST RUN - BẮT ĐẦU TỪ INDEX 6 =======
            // Bước 7 (Index 6): Staff phòng PCB điền ghi chú theo mẫu, sau đó gửi cho QLTC PCB
            new WorkflowStep {
                Index = 6, StepName = "Staff điền form (PCB)",
                Actor = StepActor.DataEntry,
                DeptCode = "EPE-PCB", Role = "Staff",
                NextApproverDept = "EPE-PCB", NextApproverRole = "Quản lý trung cấp"
            },
            
            // Bước 8 (Index 7): QLTC phòng PCB phê duyệt, sau đó gửi lại cho Staff EE
            new WorkflowStep {
                Index = 7, StepName = "QLTC phê duyệt lần 3 (PCB)",
                Actor = StepActor.Approver,
                DeptCode = "EPE-PCB", Role = "Quản lý trung cấp",
                NextApproverDept = "EPE-EE", NextApproverRole = "Staff"
            },
            
            // Bước 9 (Index 8): Staff phòng EE điền cả hai bảng, sau đó gửi cho QLTC EE
            new WorkflowStep {
                Index = 8, StepName = "Staff điền form (EE)",
                Actor = StepActor.DataEntry,
                DeptCode = "EPE-EE", Role = "Staff",
                NextApproverDept = "EPE-EE", NextApproverRole = "Quản lý trung cấp"
            },
            
            // Bước 10 (Index 9): QLTC phòng EE phê duyệt, sau đó gửi cho G.M.
            new WorkflowStep {
                Index = 9, StepName = "QLTC phê duyệt lần 4 (gửi G.M)",
                Actor = StepActor.Approver,
                DeptCode = "EPE-EE", Role = "Quản lý trung cấp",
                NextApproverDept = "EPE-G.M", NextApproverRole = "G.M"
            },
            
            // Bước 11 (Index 10): G.M comment nếu muốn và phê duyệt cuối cùng. Quy trình kết thúc.
            new WorkflowStep {
                Index = 10, StepName = "G.M phê duyệt cuối cùng",
                Actor = StepActor.Approver,
                DeptCode = "EPE-G.M", Role = "G.M",
                NextApproverDept = null, NextApproverRole = null // Không có bước tiếp theo
            }
        };

        public List<WorkflowStep> GetStepsByPhase(TestRunPhase phase)
        {
            switch (phase)
            {
                case TestRunPhase.TruocTestRun:
                    return _workflowTemplate.Where(s => s.Index <= 1).ToList();
                case TestRunPhase.GiuaTestRun:
                    return _workflowTemplate.Where(s => s.Index >= 2 && s.Index <= 5).ToList(); // SỬA: Chỉ đến Index 5
                case TestRunPhase.SauTestRun:
                    return _workflowTemplate.Where(s => s.Index >= 6).ToList(); // SỬA: Bắt đầu từ Index 6
                default:
                    return new List<WorkflowStep>();
            }
        }

        /// <summary>
        /// Xác định phase dựa trên step index
        /// </summary>
        public TestRunPhase GetPhaseByStepIndex(int stepIndex)
        {
            if (stepIndex <= 1) return TestRunPhase.TruocTestRun;
            if (stepIndex <= 5) return TestRunPhase.GiuaTestRun; // Giữa Test Run đến Index 5
            return TestRunPhase.SauTestRun; // Sau Test Run bắt đầu từ Index 6
        }

        /// <summary>
        /// Method để cập nhật phase của request dựa trên current step
        /// </summary>
        public void UpdateRequestPhase(Request request)
        {
            if (request == null) return;
            
            var newPhase = GetPhaseByStepIndex(request.CurrentStepIndex);
            
            // Chỉ cập nhật nếu phase thay đổi
            if (request.CurrentPhase != newPhase)
            {
                request.CurrentPhase = newPhase;
                System.Diagnostics.Debug.WriteLine($"[WorkflowService] Request {request.RequestID} phase updated to {newPhase} at step {request.CurrentStepIndex}");
            }
        }

        // Khởi tạo lịch sử phê duyệt cho một request mới
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

        // Lấy bước hiện tại của một request
        public WorkflowStep GetCurrentStep(Request request)
        {
            if (request == null || request.IsCompleted || request.IsRejected)
                return null;

            if (request.CurrentStepIndex < 0 || request.CurrentStepIndex >= request.History.Count)
                return null;

            return request.History.ElementAt(request.CurrentStepIndex);
        }

        // SỬA: Chuyển request sang bước tiếp theo và cập nhật phase
        public void AdvanceStep(Request request, string approverADID, string comment)
        {
            if (request == null) return;

            var currentStep = GetCurrentStep(request);
            if (currentStep == null) return;

            // Cập nhật step hiện tại
            currentStep.Status = "Approved";
            currentStep.ApproverADID = approverADID;
            currentStep.Comment = comment;
            currentStep.ApprovedAt = DateTime.Now;

            // Chuyển sang step tiếp theo
            request.CurrentStepIndex++;

            // THÊM: Cập nhật phase ngay sau khi chuyển step
            UpdateRequestPhase(request);

            // Kiểm tra xem đã hoàn thành chưa
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

        // Lấy bước tiếp theo của một request
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