using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using System;
using System.Reflection;
namespace EXPEDIT.Transactions.ViewModels
{
    public class OrderProductViewModel : ProductViewModel
    {
        [DefaultValue(1.0)]
        public decimal Units { get; set; }

        //Derived
        public decimal? Cost { get { return (PricePerUnit.HasValue ? PricePerUnit.Value * Units : default(decimal?)); } }
        public string CostText { get { return string.Format("{0} {1} {2}", CurrencyPrefix, Cost, CurrencyPostfix); } }


        public OrderProductViewModel(ProductViewModel parent)
        {            
            foreach (PropertyInfo prop in parent.GetType().GetProperties())
                GetType().GetProperty(prop.Name).SetValue(this, prop.GetValue(parent, null), null);    
        }

    }
}