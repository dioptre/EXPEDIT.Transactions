using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using System;
using System.Collections.Generic;
using XODB.Models;
using EXPEDIT.Share.Helpers;
using System.Web;
namespace EXPEDIT.Transactions.ViewModels
{
    public class SoftwareSubmissionViewModel 
    {
        public Guid SoftwareSubmissionID { get; set; }
        public string Description { get; set; }
        public bool ForSale { get; set; }
        public bool ForManagement { get; set; }
        public bool ForDevelopment { get; set; }

    }
}