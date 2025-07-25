using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace TrueTestRun.Models
{
    [Flags] // 1. Thêm attribute [Flags]
    public enum ApprovalRole
    {
        None = 0,      // 00: Không có quyền gì
        Reviewer = 1,  // 01: Quyền comment (giá trị 2^0)
        Stampler = 2,  // 10: Quyền đóng dấu (giá trị 2^1)

        // 3. Tạo các vai trò kết hợp bằng toán tử OR (|)
        // Kết hợp cả hai quyền trên
        Approver = Reviewer | Stampler // 11: Vừa comment vừa đóng dấu (giá trị 1 | 2 = 3)
    }

    public class ApprovalStep
    {
        public string DeptCode { get; set; }
        public string ADID { get; set; }
        public ApprovalRole Role { get; set; }
        public DateTime? Timestamp { get; set; }
        public string Comment { get; set; }
    }


}