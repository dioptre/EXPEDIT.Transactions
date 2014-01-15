using System.Web.Mvc;
using Orchard.Localization;
using Orchard;
using Orchard.Themes;
using System;
using System.Linq;
using EXPEDIT.Transactions.Services;
using EXPEDIT.Transactions.ViewModels;
using Newtonsoft.Json;
using XODB.Models;
using System.Dynamic;
using ImpromptuInterface;
using ImpromptuInterface.Dynamic;
using XODB.Helpers;

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
            var m = new OrderViewModel() { OrderID = Guid.NewGuid(), Products = new OrderProductViewModel[] { p } }; //TODO Update existing order before creating a new one
            Transactions.UpdateOrder(m);
            return View(m);
        }

        [Authorize]
        public ActionResult Confirm(string id)
        {
            Guid orderID = new Guid(id);
            if (Transactions.GetOrderProcessed(orderID))
                return RedirectToAction("Paid", new { area = "EXPEDIT.Transactions", controller = "User", id = id });
            var m = Transactions.GetOrder(orderID);
            Transactions.GetOrderOwner(ref m);
            Transactions.PreparePayment(ref m);      
            return View(m);
        }

        [Authorize]
        public ActionResult PaymentResult(string id, string @ref)
        {
            Guid orderID = new Guid(id);
            if (Transactions.GetOrderProcessed(orderID))
                return RedirectToAction("Paid", new { area = "EXPEDIT.Transactions", controller = "User", id = id });
            var m = Transactions.GetOrder(orderID);
            m.PaymentAntiForgeryKey = new Guid(@ref);
            m.PaymentQuery = Request.Url.Query;
            Transactions.PreparePaymentResult(ref m);
            if (m.PaymentStatus > 0)
            {
                Transactions.UpdateOrderOwner(m);
                Transactions.MakePayment(ref m);
                if ((m.PaymentStatus & 1) == 1) //Success
                {
                    Transactions.UpdateOrderPaid(m);
                    m.Downloads = Transactions.GetDownloads(orderID);
                }
            }
            return View(m);
        }

        [Authorize]
        public ActionResult Paid(string id)
        {
            Guid orderID = new Guid(id);
            if (!Transactions.GetOrderPaid(orderID))
                return new HttpUnauthorizedResult("Unauthorized access to unpaid order.");
            var m = Transactions.GetOrder(orderID);
            m.Downloads = Transactions.GetDownloads(orderID);
            return View(m);
        }

        [Authorize]
        public ActionResult PartnerAgreement()
        {
            //Show agreement            
            return View(Transactions.GetPartnership());
        }


        [HttpPost]
        public ActionResult PartnerAgreementReceipt(string id)
        {
            //Verify "Accept" to agreement
            //msgid  Unique SMSGlobal Message ID  
            //dlrstatus  The status of the delivery for SMS. 
            //dlr_err  The error code. 
            //donedate  

            return View();
        }
       

        [Authorize]
        [HttpPost]
        public ActionResult PartnerAgreement(string id)
        {
            //Save agreement
            return View();
        }



        [Authorize]
        public ActionResult SubmitSoftware()
        {
            //Show form
            return View();
        }

        [Authorize]
        [HttpPost]
        public ActionResult SubmitSoftware(string id)
        {
            //Save form
            return View();
        }


        [Authorize]
        public ActionResult MyAccount(string id)
        {
            //Show form [purchased sw, services, products, partner status, tickets, contracts]
            return View();
        }

        [HttpPost]
        [Authorize]
        public ActionResult Verify(string id, string jsonRequest)
        {
            var verify = JsonConvert.DeserializeObject<ExpandoObject>(jsonRequest).ActLike<IVerifyMobile>();
            verify.Sent = DateTime.Now;
            Transactions.VerifyTwoStepAuthentication(ref verify);
            return new JsonHelper.JsonNetResult(verify, JsonRequestBehavior.AllowGet);            
        }

        

      
    }

}
