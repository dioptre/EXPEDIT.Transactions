using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using System;
using System.Collections.Generic;
using NKD.Models;
using EXPEDIT.Share.Helpers;
using System.Web;
using Newtonsoft.Json;
using NKD.Module.BusinessObjects;

namespace EXPEDIT.Transactions.ViewModels
{
    [JsonObject]
    public class ProjectPledgeViewModel
    {
        [JsonIgnore]
        public Guid? ProjectPledgeID { get; set; }
        [Required, DisplayName("Project Name")]
        public string ProjectName { get; set; }
        public string ProjectCode { get; set; }
        [Required, DisplayName("Project Description")]
        public string Description { get; set; }
        public decimal? Deposit { get; set; }
        public decimal? TargetAmount { get { return EstimatedCost; } set { EstimatedCost = value; } }
        [Required, DisplayName("Pledge")]
        public decimal? Amount { get; set; }
        public Guid? CurrencyID { get; set; }
        public string CurrencyPrefix { get; set; }
        public string CurrencyPostfix { get; set; }
        public Guid? AcceptedProjectOfferID { get; set; }
        public decimal? AcceptedOfferAmount { get; set; }
        public string AcceptedOfferDescription { get; set; }
        [DisplayName("Worker")]
        public string AcceptedOfferUsername { get; set; }
        public DateTime? AcceptedOffer { get; set; }
        public DateTime? QuorumNegotiated { get; set; }
        public Guid? QuorumOverseer { get; set; }
        public string QuorumOverseerName { get; set; }
        public DateTime? Paid { get; set; }
        public DateTime? OfferPaid { get; set; }
        [DisplayName("Pledged By")]
        public string Username { get; set; }
        public DateTime? Realises { get; set; }
        public DateTime? Realised { get; set; }
        [Required, DisplayName("Pledge Expires")]
        public DateTime? Expires { get; set; }
        public DateTime? Expired { get; set; }

        public Guid? ProjectID { get; set; }
        public Guid? ProjectTypeID { get; set; }
        public Guid? ProjectCompanyID { get; set; }
        public Guid? ProjectManagerID { get; set; }
        public Guid? ProjectPlanID { get; set; }
        
        public Guid? ProjectDeliverableID { get; set; }
        public Guid? WorkTypeID { get; set; }
        [Required, DisplayName("Project Due")]
        public DateTime? Due { get; set; }
        public Guid? InitiatedBy { get; set; }        
        public Guid? ApprovedBy { get; set; }
        [Required, DisplayName("Estimated Cost")]
        public decimal? EstimatedCost { get; set; }
        [Required, DisplayName("Estimated Value To The Industry")]
        public decimal? EstimatedValue { get; set; }
        
        public Guid? ProjectPlanMilestoneTaskID { get; set; }
        public DateTime? TaskStartedApproved { get; set; }
        public DateTime? TaskCompletedApproved { get; set; }
        [Required, DisplayName("Project Progress")]
        public decimal? TaskProgress { get; set; }
        public DateTime? TaskBegan { get; set; }
        public DateTime? TaskCompleted { get; set; }
        [Required, DisplayName("Duration Worked On")]
        public decimal? TaskDurationHours { get; set; }
        [Required, DisplayName("Worker Rating")]
        public decimal? TaskPerformanceMetric { get; set; }
        [Required, DisplayName("Comments about worker")]
        public string TaskComments { get; set; }

        public Guid? SupplyID { get; set; }
        public Guid? CustomerApprovedBy { get; set; }
        public Guid? SupplierApprovedBy { get; set; }
        public Guid? CustomerPurchaseOrderID {get;set;}
        public Guid? SupplierPurchaseOrderID {get;set;}
        public bool? IsDraft {get;set;}
        public bool? IsUnapproved {get;set;}
        public bool? IsDenied {get;set;}
        public bool? IsSupplied {get;set;}
        public bool? IsReorderSent {get;set;}
        public bool? IsPaid {get;set;}
        public bool? IsFinalised {get;set;}
        public DateTime? Ordered { get; set; }
        public string PurchasingNotes { get; set; }

        public IEnumerable<ProjectPledgeOfferViewModel> Offers { get; set; }

        [JsonIgnore]
        public Dictionary<Guid, HttpPostedFileBase> Files { get; set; }
        [JsonIgnore]
        public Dictionary<Guid, int> FileLengths { get; set; }
        [JsonIgnore]
        public Dictionary<Guid, string> ShareFiles { get; set; }
        [JsonIgnore]
        public DateTime? Uploaded { get; set; }
        [JsonIgnore]
        public long? RowNumber { get; set; }
    }
}