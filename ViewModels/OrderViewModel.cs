using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using System;
namespace EXPEDIT.Transactions.ViewModels
{
    public class OrderViewModel
    {
        [HiddenInput, Required, DisplayName("Product ID:")]
        public Guid? OrderID { get; set; }


        public SelectList TransactionType { get; set; }

    }
}