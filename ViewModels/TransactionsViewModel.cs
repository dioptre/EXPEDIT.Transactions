using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using System;
namespace EXPEDIT.Transactions.ViewModels
{
    public class TransactionsViewModel
    {
        [HiddenInput, Required, DisplayName("Transactions ID:")]
        public Guid? TransactionsID { get; set; }
        public SelectList TransactionsType { get; set; }

    }
}