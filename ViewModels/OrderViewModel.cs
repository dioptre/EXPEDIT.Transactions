using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using System;
using System.Collections.Generic;

namespace EXPEDIT.Transactions.ViewModels
{
    public class OrderViewModel
    {
        [HiddenInput, Required, DisplayName("Order ID:")]
        public Guid? OrderID { get; set; }

        public IEnumerable<OrderProductViewModel> Products { get; set; }

        public SelectList TransactionType { get; set; }

    }
}