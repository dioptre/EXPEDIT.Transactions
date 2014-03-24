using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using System;
using System.Collections.Generic;

namespace EXPEDIT.Transactions.ViewModels
{
    public class LicenseAssetModelPartViewModel
    {
        [HiddenInput, Required, DisplayName("License Asset Model Part ID:")]
        public Guid? LicenseAssetModelPartID { get; set; }        
        public Guid? LicenseID { get; set; }
        public Guid? LicenseAssetID { get; set; }
        public Guid? ModelID { get; set; }
        public Guid? ModelPartID { get; set; }
        public string PartName { get; set; }
        public string Restrictions { get; set; }

    }
 
}