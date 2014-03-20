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

        public IEnumerable<ProductViewModel> Products { get; set; }

    }
}