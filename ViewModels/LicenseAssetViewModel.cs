using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using System;
using System.Collections.Generic;

namespace EXPEDIT.Transactions.ViewModels
{
    public class LicenseAssetViewModel
    {
        [HiddenInput, Required, DisplayName("License Asset ID:")]
        public Guid? LicenseAssetID { get; set; }        
        public Guid? LicenseID { get; set; }
        public Guid? AssetID { get; set; }
        public Guid? ProRataCost { get; set; }
        public Guid? ModelID { get; set; }
        public Guid? Restrictions { get; set; }


        public IEnumerable<LicenseAssetModelPartViewModel> LicenseAssets { get; set; }
        //Could connect with Asset one day

    }
 
}