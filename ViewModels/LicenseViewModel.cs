using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using System;
using System.Collections.Generic;

namespace EXPEDIT.Transactions.ViewModels
{
    public class LicenseViewModel
    {
        [HiddenInput, Required, DisplayName("License ID:")]
        public Guid? LicenseID { get; set; }
        public Guid? CompanyID { get; set; }
        public Guid? ContactID { get; set; }
        public Guid? LicenseeGUID { get; set; }
        public string LicenseeName { get; set; }
        public string LicenseeUsername { get; set; }
        public string LicenseeUniqueMachineCode1 { get; set; }
        public string LicenseeUniqueMachineCode2 { get; set; }
        public Guid? LicenseeGroupID { get; set; }
        public string LicensorIP { get; set; }
        public string LicensorName { get; set; }
        public Guid? LicenseTypeID { get; set; }
        public string LicenseType { get; set; }
        public string LicenseURL { get; set; }
        public string RootServerName { get; set; }
        public Guid? RootServerID { get; set; }
        public string ServerName { get; set; }
        public Guid? ServerID { get; set; }
        public Guid? ApplicationID { get; set; }
        public string ServiceAuthenticationMethod { get; set; }
        public string ServiceAuthorisationMethod { get; set; }
        public DateTime? ValidFrom { get; set; }
        public DateTime? Expiry { get; set; }
        public DateTime? SupportExpiry { get; set; }
        public decimal? ValidForDuration { get; set; }
        public Guid? ValidForUnitID { get; set; }
        public string ValidForUnitName { get; set; }

        public IEnumerable<LicenseAssetViewModel> LicenseAssets { get; set; }
        public IEnumerable<DownloadViewModel> LicenseDownloads { get; set; }

    }
 
}