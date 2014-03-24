using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using System;
using System.Collections.Generic;

namespace EXPEDIT.Transactions.ViewModels
{
    public class InvoiceViewModel
    {
        [HiddenInput, Required, DisplayName("Invoice ID:")]
        public Guid? InvoiceID { get; set; }
        public decimal? FreightTax { get; set; }
        public decimal? FreightAmount { get; set; }
        public bool DiscountIncludesFreight { get; set; }
        public bool DiscountAllFreight { get; set; }
        public decimal? DiscountAmount { get; set; }
        public decimal? TaxAmount { get; set; }
        public decimal? OriginalTotal { get; set; }
        public Guid? CurrencyID { get; set; }
        public string CurrencyPrefix { get; set; }
        public string CurrencyPostfix { get; set; }
        public decimal? Total { get; set; }
        public DateTime Dated { get; set; }
        public DateTime Communicated { get; set; }

        public IEnumerable<InvoiceLineViewModel> InvoiceLines { get; set; }
    }

 
}