using System.Web.Mvc;
using Orchard.Localization;
using Orchard;
using Orchard.Themes;
using System;
using System.Linq;
using EXPEDIT.Transactions.Services;
using EXPEDIT.Transactions.ViewModels;

namespace EXPEDIT.Transactions.Controllers
{
    [Themed]
    public class UserController : Controller
    {
        public IOrchardServices Services { get; set; }
        public ITransactionsService Transactions { get; set; }     
        
        public UserController(IOrchardServices services, ITransactionsService transactions)
        {
            Services = services;
            T = NullLocalizer.Instance;
            Transactions = transactions;
        }

        public Localizer T { get; set; }

        [ValidateInput(false)]
        public ActionResult Index()
        {
            var m = new ProductsViewModel { Products = Transactions.GetProducts() };
            return View(m);
        }

        public ActionResult download(string id, string @ref)
        {
            try
            {
                Transactions.IncrementDownloadCounter(new Guid(@ref));
                Response.Redirect(System.Web.VirtualPathUtility.ToAbsolute(string.Format("~/share/download/{0}", id)));
            }
            catch
            {
               
            }
            return new HttpNotFoundResult();
        }

        public ActionResult Buy(string id, string @ref)
        {
            var supplierModelID = new Guid(id);
            var modelID = new Guid(@ref);
            Transactions.IncrementBuyCounter(supplierModelID, modelID);
            var m = new OrderProductViewModel(Transactions.GetProduct(supplierModelID)) { Units = 1, ContractConditions = Transactions.GetContractConditions(new Guid[] { supplierModelID, modelID }) };
            return View(m);
        }


        public ActionResult Confirm(string id, string @ref)
        {
            var supplierModelID = new Guid(id);
            var modelID = new Guid(@ref);
            Transactions.IncrementConfirmCounter(supplierModelID, modelID);
            var p = new OrderProductViewModel(Transactions.GetProduct(supplierModelID)) { Units = 1, ContractConditions = Transactions.GetContractConditions(new Guid[] { supplierModelID, modelID }) };
            var m = new OrderViewModel() { OrderID = Guid.NewGuid(), Products = new OrderProductViewModel[] { p } };
            return View(m);
        }
      
    }

}
