using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using System;
using System.Collections.Generic;

namespace EXPEDIT.Transactions.ViewModels
{
    public class ContactViewModel
    {
        [HiddenInput, Required, DisplayName("Contact ID:")]
        public Guid? ContactID { get; set; }
        public string ContactName { get; set; }
        public string Title { get; set; }
        public string Surname { get; set; }
        public string Firstname { get; set; }
        public string Username { get; set; }
        public string Hash { get; set; }
        [DisplayName("Default Email")]
        public string DefaultEmail { get; set; }
        public DateTime? DefaultEmailValidated { get; set; }
        [DisplayName("Default Mobile")]
        public string DefaultMobile { get; set; }
        public DateTime? DefaultMobileValidated { get; set; }
        public string MiddleNames { get; set; }
        public string Initials { get; set; }
        public DateTime? DOB { get; set; }
        public string BirthCountryID { get; set; }
        public string BirthCity { get; set; }
        public Guid? AspNetUserID { get; set; }
        public Guid? XafUserID { get; set; }
        public string OAuthID { get; set; }
        public byte[] Photo { get; set; }
        public string ShortBiography { get; set; }
        public ContactAddressViewModel PrimaryAddress { get; set; }
        public IEnumerable<ContactAddressViewModel> ContactAddresses { get; set; }
    }
 
}