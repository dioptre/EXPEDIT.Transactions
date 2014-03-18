using System.Web.Mvc;
using Orchard.Localization;
using Orchard;
using Orchard.Themes;
using System;
using System.Linq;
using EXPEDIT.Transactions.Services;
using EXPEDIT.Transactions.ViewModels;
using Newtonsoft.Json;
using NKD.Models;
using System.Dynamic;
using ImpromptuInterface;
using ImpromptuInterface.Dynamic;
using NKD.Helpers;
using System.Web;
using System.Collections.Generic;
using EXPEDIT.Share.Services;


namespace EXPEDIT.Transactions.Controllers
{
    [Themed]
    public class UserController : Controller
    {
        private IOrchardServices _services { get; set; }
        private ITransactionsService _transactions { get; set; }
        private IContentService _content { get; set; }

        public UserController(IOrchardServices services, ITransactionsService transactions, IContentService content)
        {
            _services = services;
            T = NullLocalizer.Instance;
            _transactions = transactions;
            _content = content;
        }

        public Localizer T { get; set; }

        [ValidateInput(false)]
        public ActionResult Index()
        {
            var m = new ProductsViewModel { Products = _transactions.GetProducts() };
            return View(m);
        }

        [Authorize]
        public ActionResult Download(string id, string @ref)
        {
            try
            {
                _transactions.IncrementDownloadCounter(new Guid(@ref));
                return new RedirectResult(System.Web.VirtualPathUtility.ToAbsolute(string.Format("~/share/download/{0}", id)));
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
            Guid modelID = default(Guid);
            var p = _transactions.GetProduct(supplierModelID);
            if (!string.IsNullOrWhiteSpace(@ref))
                modelID = new Guid(@ref);
            else
                modelID = p.ModelID.Value;
            _transactions.IncrementBuyCounter(supplierModelID, modelID);
            var op = new OrderProductViewModel(p) { Units = 1, ContractConditions = _transactions.GetContractConditions(new Guid[] { supplierModelID, modelID }) };
            var m = new OrderViewModel() { OrderID = Guid.NewGuid(), Products = new OrderProductViewModel[] { op } }; //TODO Update existing order before creating a new one
            _transactions.UpdateOrder(m);
            return View(m);
        }

        [Authorize]
        public ActionResult Confirm(string id)
        {
            Guid orderID = new Guid(id);
            if (_transactions.GetOrderProcessed(orderID))
                return RedirectToAction("Paid", new { area = "EXPEDIT.Transactions", controller = "User", id = id });
            var m = _transactions.GetOrder(orderID);
            _transactions.GetOrderOwner(ref m);
            _transactions.PreparePayment(ref m);      
            return View(m);
        }

        [Authorize]
        public ActionResult PaymentResult(string id, string @ref)
        {
            Guid orderID = new Guid(id);
            if (_transactions.GetOrderProcessed(orderID))
                return RedirectToAction("Paid", new { area = "EXPEDIT.Transactions", controller = "User", id = id });
            var m = _transactions.GetOrder(orderID);
            m.PaymentAntiForgeryKey = new Guid(@ref);
            m.PaymentQuery = Request.Url.Query;
            _transactions.PreparePaymentResult(ref m);
            if (m.PaymentStatus > 0)
            {
                _transactions.UpdateOrderOwner(m);
                _transactions.MakePayment(ref m);
                if ((m.PaymentStatus & 1) == 1) //Success
                {
                    _transactions.UpdateOrderPaid(m);
                    m.Downloads = _transactions.GetDownloads(orderID);
                }
            }
            _content.UpdateAffiliate();
            return View(m);
        }

        [Authorize]
        public ActionResult Paid(string id)
        {
            Guid orderID = new Guid(id);
            if (!_transactions.GetOrderPaid(orderID))
                return new HttpUnauthorizedResult("Unauthorized access to unpaid order.");
            var m = _transactions.GetOrder(orderID);
            m.Downloads = _transactions.GetDownloads(orderID);
            return View(m);
        }

        [Authorize]
        public ActionResult PartnerAgreement()
        {
            //Show agreement            
            return View(_transactions.GetPartnership());
        }
        
        
        [Authorize]
        [HttpPost]
        public ActionResult PartnerAgreement(PartnerViewModel m)
        {
            //Save agreement
            if (_transactions.UpdatePartnership(m, Request.GetIPAddress()))
                return new RedirectResult(System.Web.VirtualPathUtility.ToAbsolute("~/PartnerAgreementConfirmed"));
            return View(m);
        }


        [HttpPost]
        [Authorize]
        public ActionResult Verify(string id, string jsonRequest)
        {
            var verify = JsonConvert.DeserializeObject<VerifyMobileModel>(jsonRequest);
            verify.Sent = DateTime.Now;
            verify.VerificationID = new Guid(id);
            if (!_transactions.SendTwoStepAuthentication(ref verify))
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.ExpectationFailed);
            return new JsonHelper.JsonNetResult(verify, JsonRequestBehavior.AllowGet);            
        }


        [Authorize]
        [Themed(Enabled=false)]
        public virtual ActionResult UploadFile()
        {
            var id = Request.Params["SoftwareSubmissionID"];
            if (string.IsNullOrWhiteSpace(id))
                return null;
            SoftwareSubmissionViewModel s = new SoftwareSubmissionViewModel { SoftwareSubmissionID = new Guid(id) , FileLengths = new Dictionary<Guid,int>()};
            if (s.Files == null)
                s.Files = new Dictionary<Guid, HttpPostedFileBase>();
            for (int i = 0; i < Request.Files.Count; i++ )
                s.Files.Add(Guid.NewGuid(), Request.Files[i]);
            _transactions.SubmitSoftware(s);
            var list = new List<dynamic>();
            foreach (var f in s.Files)
                list.Add(Build<ExpandoObject>.NewObject(name: f.Value.FileName, type: "application/octet", size: s.FileLengths[f.Key], url: VirtualPathUtility.ToAbsolute(string.Format("~/share/file/{0}", f.Key))));
            return new JsonHelper.JsonNetResult(new { files = list.ToArray() }, JsonRequestBehavior.AllowGet);
        }


        [Authorize]
        public ActionResult SubmitSoftware()
        {
            var m = new SoftwareSubmissionViewModel{ SoftwareSubmissionID = Guid.NewGuid()};
            //Show form
            return View(m);
        }

        [Authorize]
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult SubmitSoftware(SoftwareSubmissionViewModel s)
        {
            //Save form
            if (_transactions.SubmitSoftware(s))
                return new RedirectResult(System.Web.VirtualPathUtility.ToAbsolute("~/SubmitSoftwareConfirmed"));
            return View(s);
        }


        [Authorize]
        public ActionResult MyAccount(string id)
        {
            //Show form [purchased sw, licenses, services, products, partner status, parnter sw, tickets, contracts, affiliates]
            return View();
        }

      
    }

}
