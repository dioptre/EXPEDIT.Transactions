using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using System;
namespace EXPEDIT.Transactions.ViewModels
{
    public class ContractConditionViewModel
    {
        public Guid? ContractID { get; set; }
        public Guid? ContractConditionID { get; set; }
        public string ContractText { get; set; }
        public bool? IsForDistributors { get; set; }
        public bool? IsForEndUsers { get; set; }
        public bool? IsForSuppliers { get; set; }     

    }
}