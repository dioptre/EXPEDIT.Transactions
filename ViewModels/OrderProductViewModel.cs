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
        //Derived
        public decimal? Cost
        {
            get
            {
                var modelCost = PricePerModelUnit.HasValue && ModelUnits.HasValue ? PricePerModelUnit.Value * ModelUnits.Value : default(decimal?);
                var partCost = PricePerPartUnit.HasValue && PartUnits.HasValue ? PricePerPartUnit.Value * PartUnits.Value : default(decimal?);
                var labourCost = PricePerLabourUnit.HasValue && LabourUnits.HasValue ? PricePerLabourUnit.Value * LabourUnits.Value : default(decimal?);
                if (modelCost.HasValue || partCost.HasValue || labourCost.HasValue)
                    return ((modelCost.HasValue) ? modelCost.Value : 0m) + ((partCost.HasValue) ? partCost.Value : 0m) + ((labourCost.HasValue) ? labourCost.Value : 0m);
                else 
                     return default(decimal?);
            }
        }
        [Required, DisplayName("Subtotal:*")]
        public string CostText { get { return string.Format("{0} {1:f2} {2}", CurrencyPrefix, Cost, CurrencyPostfix); } }
        public bool Paid { get; set; }

        public Guid? RecipientID { get; set; }

        public OrderProductViewModel() : base()
        { }

        public OrderProductViewModel(ProductViewModel parent)
        {
            parent.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(f=>f.CanWrite).ToList().ForEach(f=>f.SetValue(this, f.GetValue(parent, null), null));
        }

    }
}