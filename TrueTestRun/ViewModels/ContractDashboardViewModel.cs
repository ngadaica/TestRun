

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace TrueTestRun.ViewModels
{
    public class ContractDashboardViewModel
    {
        public int TotalContracts { get; set; }
        public decimal PaidTotal { get; set; }
        public decimal UnpaidTotal { get; set; }
        public int CanceledTotal { get; set; }

        public IList<ContractViewModel> Contracts { get; set; }
    }
}