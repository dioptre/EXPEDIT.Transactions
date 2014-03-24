using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using System;
using System.Collections.Generic;

namespace EXPEDIT.Transactions.ViewModels
{
    public class InvoiceLineViewModel
    {
        [HiddenInput, Required, DisplayName("Invoice ID:")]
        public Guid? InvoiceID { get; set; }
        [HiddenInput, Required, DisplayName("Invoice Line ID:")]
        public Guid? InvoiceLineID { get; set; }
        public Guid? SupplyItemID { get; set; }
        public string ReferenceType { get; set; }
        public Guid? ReferenceID { get; set; }
        public string Description { get; set; }
        public decimal? Quantity { get; set; }
        public Guid? TaxID { get; set; }
        public decimal? Tax { get; set; }
        public string TaxName { get; set; }
        public decimal? DiscountAmount { get; set; }
        public decimal? OriginalSubtotal { get; set; }
        public decimal? Subtotal { get; set; }
    }
 
}