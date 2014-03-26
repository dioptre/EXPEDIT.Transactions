using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
namespace EXPEDIT.Transactions.ViewModels
{
    public class ProductViewModel
    {
        [HiddenInput, Required, DisplayName("SKU:")]
        public Guid? SupplierModelID { get; set; }
        public Guid? SupplierPartID { get; set; }
        public Guid? ModelID { get; set; } //ProductID
        public Guid? PartID { get; set; } //Also...ProductID
        public Guid? CompanyID { get; set; }
        public Guid? SupplierID { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string Manufacturer { get; set; }
        public string UrlInternal { get; set; }
        public string UrlExternal { get; set; }
        public decimal? Rating { get; set; }
        public decimal? RatingScale { get; set; }
        public string HTML { get; set; }
        public string Thumbnail { get { return string.Format(@"{0}/Companies/{1}.png", MediaDirectory, CompanyID); } }
        public decimal? PricePerModelUnit { get; set; }
        public decimal? PricePerLabourUnit { get; set; }
        public decimal? PricePerPartUnit { get; set; }
        public Guid? CurrencyID { get; set; }
        public string CurrencyPrefix { get; set; }
        public string CurrencyPostfix { get; set; }
        public Guid? PriceModelUnitID { get; set; }
        public Guid? PricePartUnitID { get; set; }
        public Guid? PriceLabourUnitID { get; set; }
        public string CostLabourUnit { get; set; }
        public string CostPartUnit { get; set; }
        public string CostModelUnit { get; set; }        
        public Guid? FreeDownloadID { get; set; }
        public DateTime LastUpdated { get; set; }
        public int? Downloads { get; set; }
        public string MediaDirectory { get; set; }
        public Guid? PaymentProviderID { get; set; }
        public Guid? PaymentProviderProductID { get; set; }
        public string PaymentProviderProductName { get; set; }
        public Guid? ProductUnitID { get; set; }
        public string ProductUnitName { get; set; }
        public string ProductUnitNamePaymentProvider { get; set; }
        public bool? IsRecurring { get; set; }
        public Guid? ProductID { get; set; } //ModelID or PartID
        public decimal? KitUnitDefault { get; set; }
        public decimal? KitUnitMaximum { get; set; }
        public decimal? KitUnitMinimum { get; set; }
        public decimal? UnitDefault { get; set; }
        public decimal? UnitMaximum { get; set; }
        public decimal? UnitMinimum { get; set; }
        public decimal? Subtotal { get; set; }
        public string ModelName { get; set; }
        [DisplayName("Labour Units:")]
        public decimal? LabourUnits { get; set; }
        [DisplayName("Part Units:")]
        public decimal? PartUnits { get; set; }
        [DisplayName("Model Units:")]
        public decimal? ModelUnits { get; set; }
        public decimal? Tax { get; set; }

        public IEnumerable<ContractConditionViewModel> ContractConditions { get; set; }

        [JsonIgnore]
        [DisplayName("Product Terms:")]
        public string ProductTerms
        {
            
            get { return string.Join("\r\n", (from o in ContractConditions select o.ContractText).ToArray()); }
        }
      

    }
}