using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using System;
using System.Collections.Generic;

namespace EXPEDIT.Transactions.ViewModels
{
    public class AccountViewModel
    {
        [HiddenInput, Required, DisplayName("Store ID:")]
        public Guid? ApplicationID { get; set; }

        public int? AffiliateCount { get; set; }

        public int? AffiliatePoints { get; set; }

        public IEnumerable<InvoiceViewModel> Invoices { get; set; }
        public IEnumerable<LicenseViewModel> Licenses { get; set; }
        public int? Offset { get; set; }
        public int? PageSize { get; set; }

        //LicensedSessions (Active & Historic)
        //Machines
        //Bids (Old, Current, Won)
        //-Pledges
        //-Delivery Commitment
        //public IEnumerable<LicenseViewModel> Licenses { get; set; }
        //Tickets
        //Reviews
        //Referrals
        //software uploaded
        //PartnershipStatus
        //Affiliate Referrals #, points        
    }
}