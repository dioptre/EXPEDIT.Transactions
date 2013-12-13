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

        [Authorize]
        public ActionResult Download(string id, string @ref)
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

        [Authorize]
        public ActionResult Buy(string id, string @ref)
        {
            var supplierModelID = new Guid(id);
            var modelID = new Guid(@ref);
            Transactions.IncrementBuyCounter(supplierModelID, modelID);
            var p = new OrderProductViewModel(Transactions.GetProduct(supplierModelID)) { Units = 1, ContractConditions = Transactions.GetContractConditions(new Guid[] { supplierModelID, modelID }) };
            var m = new OrderViewModel() { OrderID = Guid.NewGuid(), Products = new OrderProductViewModel[] { p } };
            Transactions.UpdateOrder(m);
            return View(m);
        }

        [Authorize]
        public ActionResult Confirm(string id)
        {
            var m = Transactions.GetOrder(new Guid(id));
            Transactions.GetOrderOwner(ref m);
            Transactions.PreparePayment(ref m);      
            return View(m);
        }

        [Authorize]
        public ActionResult PaymentResult(string id, string @ref)
        {
            var m = Transactions.GetOrder(new Guid(id));
            m.PaymentAntiForgeryKey = new Guid(@ref);
            m.PaymentQuery = Request.Url.Query;
            Transactions.PreparePaymentResult(ref m);
            if (m.PaymentStatus > 0)
            {                
                Transactions.MakePayment(ref m);
            }
            return View(m);
        }
      
    }

}
