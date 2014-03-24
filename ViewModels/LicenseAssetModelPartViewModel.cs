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
        public Guid? AssetID { get; set; }
        public Guid? ModelID { get; set; }
        public Guid? ModelPartID { get; set; }
        public Guid? Restrictions { get; set; }

    }
 
}