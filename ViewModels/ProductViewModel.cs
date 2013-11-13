using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using System;
namespace EXPEDIT.Transactions.ViewModels
{
    public class ProductViewModel
    {
        [HiddenInput, Required, DisplayName("Product ID:")]
        public Guid? ModelID { get; set; }
        public Guid? CompanyID { get; set; }
        public Guid? SupplierID { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string Manufacturer { get; set; }
        public string UrlInternal { get; set; }
        public string UrlExternal { get; set; }
        public float Rating { get; set; }
        public string HTML { get; set; }
        public string Thumbnail { get { return string.Format(@"{0}/Companies/{1}.jpg", MediaDirectory, CompanyID); } }
        public decimal? PricePerUnit { get; set; }
        public Guid? PriceUnitID { get; set; }
        public string CostUnit { get; set; }
        public decimal CostText { get; set; }
        public Guid? FreeDownloadID { get; set; }
        public DateTime LastUpdated { get; set; }
        public int Downloads { get; set; }
        public string MediaDirectory { get; set; }

    }
}