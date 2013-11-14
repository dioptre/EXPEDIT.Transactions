using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using System;
namespace EXPEDIT.Transactions.ViewModels
{
    public class OrderProductViewModel : ProductViewModel
    {
        [DefaultValue(0)]
        public decimal Units { get; set; }

        //Derived
        public decimal? Cost { get { return (PricePerUnit.HasValue ? PricePerUnit.Value * Units : default(decimal?)); } }
        public string CostText { get { return string.Format("{0} {1} {2}", CurrencyPrefix, Cost, CurrencyPostfix); } }
        

    }
}