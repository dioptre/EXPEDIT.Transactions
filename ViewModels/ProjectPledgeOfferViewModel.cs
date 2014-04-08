using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
namespace EXPEDIT.Transactions.ViewModels
{
    public class ProjectPledgeOfferViewModel
    {
        public Guid? ProjectOfferID { get; set; }
        public Guid? ProjectID { get; set; }
        [Required, DisplayName("Offer Description")]
        public string OfferDescription { get; set; }
        public Guid? OfferContactID { get; set; }
        public DateTime? Offered { get; set; }
        public decimal? Amount { get; set; }
        public DateTime? AmountPaid { get; set; }

    }
}