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
        public Guid ContactID { get; set; }
        public HttpPostedFileBase MyFile { get; set; }

    }
}