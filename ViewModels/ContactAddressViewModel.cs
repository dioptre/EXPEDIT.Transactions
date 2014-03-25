using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using System;
using System.Collections.Generic;

namespace EXPEDIT.Transactions.ViewModels
{
    public class ContactAddressViewModel
    {
        [HiddenInput, Required, DisplayName("Contact ID:")]
        public Guid? ContactID { get; set; }
        //public Guid? ContactAddressID { get; set; }
        public Guid? AddressID { get; set; }
        public Guid? AddressTypeID { get; set; }
        [DisplayName("Company")]
        public string AddressName { get; set; }
        public int Sequence { get; set; }
        public string Street { get; set; }
        [DisplayName("Extended Address")]
        public string Extended { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Country { get; set; }
        public string Postcode { get; set; }
        public bool? IsHQ { get; set; }
        public bool? IsPostBox { get; set; }
        public bool? IsBusiness { get; set; }
        public bool? IsHome { get; set; }
        public string Phone { get; set; }
        public string Fax { get; set; }
        public string Email { get; set; }
        public string Mobile { get; set; }
        public Guid? LocationID { get; set; }

    }
 
}