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
    public class SoftwareSubmissionViewModel
    {
        [JsonIgnore]
        public Guid SoftwareSubmissionID { get; set; }
        public string Description { get; set; }
        public bool ForSale { get; set; }
        public bool ForManagement { get; set; }
        public bool ForDevelopment { get; set; }
        [JsonIgnore]
        public Dictionary<Guid, HttpPostedFileBase> Files { get; set; }
        [JsonIgnore]
        public Dictionary<Guid, int> FileLengths { get; set; }
    }
}