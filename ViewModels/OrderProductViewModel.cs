using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using System;
using System.Reflection;
using System.Linq;
using System.Linq.Expressions;

namespace EXPEDIT.Transactions.ViewModels
{
    public class OrderProductViewModel : ProductViewModel
    {
        [Required, DisplayName("Purchase Units:")]
        public decimal Units { get; set; }

        //Derived
        public decimal? Cost { get { return (PricePerUnit.HasValue ? PricePerUnit.Value * Units : default(decimal?)); } }
        [Required, DisplayName("Subtotal:")]
        public string CostText { get { return string.Format("{0} {1} {2}", CurrencyPrefix, Cost, CurrencyPostfix); } }

        public OrderProductViewModel(ProductViewModel parent)
        {
            parent.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(f=>f.CanWrite).ToList().ForEach(f=>f.SetValue(this, f.GetValue(parent, null), null));
        }

    }
}