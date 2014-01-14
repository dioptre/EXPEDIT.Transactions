using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using System;
using System.Collections.Generic;
using XODB.Models;
namespace EXPEDIT.Transactions.ViewModels
{
    public class PartnerViewModel : IVerifyMobile
    {

        public Guid ContractID { get; set; }
        public Guid ContactID { get; set; }
        public string Firstname { get; set; }
        public string Lastname { get; set; }
        public string Mobile { get; set; }
        public Guid TwoStepID { get; set; }

        public Guid VerificationID { get { return TwoStepID; } set { TwoStepID = value; } }
        public string TableType { get { return "X_Contract"; } set { } }
        public Guid ReferenceID { get { return ContractID; } set { } }
        public string ReferenceName { get { return string.Format("{0}", Mobile); } set { } }
        public DateTime Sent { get; set; }
        public DateTime Verified { get; set; }

    }
}