using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace TrueTestRun.ViewModels
{
    public class ContractViewModel
    {
        public int Id { get; set; }
        public string ContractCode { get; set; }
        public string ContactName { get; set; }
        public DateTime Date { get; set; }
        public DateTime ValidityStart { get; set; }
        public DateTime ValidityEnd { get; set; }
        public decimal Paid { get; set; }
        public decimal Remain { get; set; }
        public string PersonInCharge { get; set; }
    }
}