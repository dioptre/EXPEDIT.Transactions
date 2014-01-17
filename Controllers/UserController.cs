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
            var verify = JsonConvert.DeserializeObject<ExpandoObject>(jsonRequest).ActLike<IVerifyMobile>();
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
            HttpPostedFileBase myFile = Request.Files["Files"];

            if (myFile != null && myFile.ContentLength != 0)
            {
                //if (this.CreateFolderIfNeeded(pathForSaving))
                //{
                //    try
                //    {
                //        myFile.SaveAs(Path.Combine(pathForSaving, myFile.FileName));
                //        isUploaded = true;
                //        message = "File uploaded successfully!";
                //    }
                //    catch (Exception ex)
                //    {
                //        message = string.Format("File upload failed: {0}", ex.Message);
                //    }
                //}
            }
            dynamic file = Build<ExpandoObject>.NewObject(name:"test",type:"application/octet",size:14,url:"/ast");
            dynamic file2 = Build<ExpandoObject>.NewObject(name: "test", type: "application/octet", size: 14, url: "/ast");
            var list = new List<dynamic>();
            list.Add(file);
            list.Add(file2);
            
            return new JsonHelper.JsonNetResult(new { files = list.ToArray() }, JsonRequestBehavior.AllowGet);
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

      
    }

}
