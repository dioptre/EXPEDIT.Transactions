using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using System;
using System.Collections.Generic;
using NKD.Models;
using EXPEDIT.Share.Helpers;
namespace EXPEDIT.Transactions.ViewModels
{
    public class PartnerViewModel : IVerify
    {

        public Guid ParentContractID { get { return ConstantsHelper.CONTRACT_PARTNER; } }
        public string ContractText { get; set; }
        public Guid? ContractID { get; set; }
        public Guid ContactID { get; set; }
        [Required]
        public string Firstname { get; set; }
        [Required]
        public string Lastname { get; set; }
        [Required]
        [DisplayName("Mobile Phone:")]
        public string Mobile { get; set; }
        public Guid? TwoStepID { get; set; }

        public Guid VerificationID { get { return TwoStepID.Value; } set { TwoStepID = value; } }
        public string TableType { get { return ConstantsHelper.REFERENCE_TYPE_CONTRACT; } set { } }
        public Guid ReferenceID { get { return ContractID.Value; } set { ContractID = value; } }
        public string ReferenceName { get { return string.Format("{0}", Mobile); } set { } }
        public DateTime? Sent { get; set; }
        public DateTime? Verified { get; set; }
        [DisplayName("SMS Code:")]
        public string VerificationCode { get; set; }

    }
}