using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using System;
using System.Collections.Generic;
using NKD.Models;
using EXPEDIT.Share.Helpers;
using Newtonsoft.Json;
namespace EXPEDIT.Transactions.ViewModels
{
    public class PartnerViewModel : IVerify
    {

        public Guid ParentContractID { get { return ConstantsHelper.CONTRACT_PARTNER; } }
        [JsonIgnore]
        public string ContractText { get; set; }
        public Guid? ContractID { get; set; }
        public Guid ContactID { get; set; }
        [Required]
        public string Company { get; set; }
        [Required]
        public string Firstname { get; set; }
        [Required]
        public string Lastname { get; set; }
        [Required]
        [DisplayName("Mobile Phone:")]
        public string Mobile { get; set; }
        public Guid? TwoStepID { get; set; }

        [JsonIgnore]
        public bool IsContractValid { get { return (!string.IsNullOrWhiteSpace(Firstname) && !string.IsNullOrWhiteSpace(Lastname) && !string.IsNullOrWhiteSpace(Company)); } }        
        [JsonIgnore]
        public Guid VerificationID {
            get { return TwoStepID.HasValue ? TwoStepID.Value : default(Guid); } 
            set { TwoStepID = value; } 
        }
        [JsonIgnore]
        public string TableType { get { return ConstantsHelper.REFERENCE_TYPE_CONTRACT; } set { } }
        [JsonIgnore]
        public Guid ReferenceID { get { return ContractID.Value; } set { ContractID = value; } }
        [JsonIgnore]
        public string ReferenceName { get { return string.Format("{0}", Mobile); } set { } }
        [JsonIgnore]
        public DateTime? Sent { get; set; }
        [JsonIgnore]
        public DateTime? Verified { get; set; }
        [JsonIgnore]
        [DisplayName("SMS Code:")]
        public string VerificationCode { get; set; }

    }
}