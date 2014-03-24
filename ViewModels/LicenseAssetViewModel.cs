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
        public decimal? ProRataCost { get; set; }
        public Guid? ModelID { get; set; }
        public string ModelName { get; set; }
        public string Restrictions { get; set; }


        public IEnumerable<LicenseAssetModelPartViewModel> LicenseAssetModelParts { get; set; }
        //Could connect with Asset one day

    }
 
}