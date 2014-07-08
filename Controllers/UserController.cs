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
using Orchard.Mvc;
using Orchard.Logging;

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
            Logger = NullLogger.Instance;        
            T = NullLocalizer.Instance;
            _transactions = transactions;
            _content = content;
        }

        public ILogger Logger { get; set; }
        public Localizer T { get; set; }

        [ValidateInput(false)]
        public ActionResult Index()
        {
            var m = new ProductsViewModel { Products = _transactions.GetProducts(null,null,null,null, EXPEDIT.Share.Helpers.ConstantsHelper.ProductCategories) };
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

        [HttpPost]
        [Authorize]
        public ActionResult Buy(Guid orderID)
        {
            _transactions.ConfirmContractConditions(orderID);
            return RedirectToAction("Confirm", new { id = orderID });
        }

        [HttpGet]
        [Authorize]
        public ActionResult Buy(string id, string @ref)
        {
            Action<Guid> sc = (oid) =>
            {
                var cookie = new HttpCookie("OrderID", oid.ToString());
                cookie.Expires = DateTime.UtcNow.AddYears(5);
                Response.SetCookie(cookie);
            };
            var supplierModelID = new Guid(id);
            Guid modelID = default(Guid);
            var newProduct = _transactions.GetProduct(supplierModelID);
            if (!string.IsNullOrWhiteSpace(@ref))
                modelID = new Guid(@ref);
            else
                modelID = newProduct.ModelID.Value;
            _transactions.IncrementBuyCounter(supplierModelID, modelID);

            var order = _transactions.GetOrderCurrent();
            var checkedContract = false;
            //If this is a +1, go to confirm
            OrderProductViewModel oldProduct = null;
            var oldModels = new List<Guid>();
            if (order != null && order.Products != null)
            {
                oldProduct = order.Products.FirstOrDefault(f => f.SupplierModelID == supplierModelID);
                if (order.OrderID.HasValue && _transactions.CheckContractConditions(order.OrderID.Value))
                    checkedContract = true;
                oldModels.AddRange(order.Products.Where(f => f.ModelID.HasValue).Select(f => f.ModelID.Value).Union(order.Products.Where(f => f.SupplierModelID.HasValue).Select(f => f.SupplierModelID.Value)));
            }
            if (oldProduct != null && oldProduct.ModelUnits.HasValue && oldProduct.ModelUnits.Value > 0 && checkedContract)
            {
                oldProduct.ModelUnits++;
                sc(order.OrderID.Value);
                _transactions.UpdateOrder(order);
                return RedirectToAction("Confirm", new { id = order.OrderID });
            }
            else
            {
                //var cc = _transactions.GetContractConditions(new Guid[] { supplierModelID, modelID });
                var op = new OrderProductViewModel(newProduct) { ModelUnits = 1 };
                var allConditions = _transactions.GetContractConditions(oldModels.Union(new Guid[] { supplierModelID, modelID }).ToArray());
                order = new OrderViewModel() {
                    OrderID = (order == null || !order.OrderID.HasValue) ? Guid.NewGuid() : order.OrderID, 
                    CompleteContractConditions = allConditions, 
                    Products = (order == null || order.Products == null) ? new OrderProductViewModel[] { op } : order.Products.Where(f=>f.ModelID!=modelID).Concat(new OrderProductViewModel[] { op }) };
                sc(order.OrderID.Value);
                _transactions.UpdateOrder(order);
                return View(order);
            }
        }

        [RequireHttps]
        [Authorize]
        public ActionResult Confirm(string id)
        {
            Guid orderID = new Guid(id);
            if (_transactions.GetOrderProcessed(orderID))
                return RedirectToAction("Paid", new { area = "EXPEDIT.Transactions", controller = "User", id = id });
            var m = _transactions.GetOrder(orderID);
            if (!_transactions.CheckContractConditions(orderID))
            {
                var p = m.Products.Where(f=>f.ModelID.HasValue && f.SupplierModelID.HasValue).FirstOrDefault();
                if (p != null)
                    return RedirectToAction("Buy", new { id = p.SupplierModelID, @ref = p.ModelID });
                m.Products = null;
                return View(m);
            }
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
                if ((m.PaymentStatus & 1) == 1 && m.Products.All(f=>f.Paid)) //Success
                {
                    _transactions.UpdateOrderPaid(m);
                    m.Downloads = _transactions.GetDownloads(orderID);
                    var clearCookie = new HttpCookie("OrderID");
                    clearCookie.Expires = DateTime.UtcNow.AddDays(-1000);
                    Response.SetCookie(clearCookie);
                }
                else
                {
                    Logger.Warning(string.Format("******Bad Transaction for Order:{0}. Response:{1}", m.OrderID, m.PaymentResponse));
                }
            }
            _content.UpdateAffiliate();
            return View(m);
        }

        [Authorize]
        public ActionResult Paid(string id)
        {
            var clearCookie = new HttpCookie("OrderID");
            clearCookie.Expires = DateTime.UtcNow.AddDays(-1000);
            Response.SetCookie(clearCookie);
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
            PartnerViewModel p = new PartnerViewModel { };
            return View(_transactions.GetPartnership(ref p));
        }
        
        
        [Authorize]
        [HttpPost]
        public ActionResult PartnerAgreement(PartnerViewModel m)
        {
            //Save agreement
            if (m.TwoStepID.HasValue && _transactions.UpdatePartnership(m, Request.GetIPAddress()))
                return new RedirectResult(System.Web.VirtualPathUtility.ToAbsolute("~/PartnerAgreementConfirmed"));
            var p = _transactions.GetPartnership(ref m);
            return View(p);
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

        [Themed(Enabled = false)]
        public ActionResult Verified(string id)
        {
            return new EmptyResult();
        }

        [HttpGet, HttpPost]
        [Themed(Enabled = false)]
        public ActionResult VerifyFeedback(string id)
        {
            return new EmptyResult();
        }


        [Authorize]
        [Themed(Enabled=false)]
        public virtual ActionResult UploadFile()
        {
            var softwareID = Request.Params["SoftwareSubmissionID"];
            var projectPledgeID = Request.Params["ProjectPledgeID"];
            var rFiles = new Dictionary<Guid, HttpPostedFileBase>();
            var rFileLengths = new Dictionary<Guid, int>();
            if (!string.IsNullOrWhiteSpace(softwareID))
            {
                SoftwareSubmissionViewModel s = new SoftwareSubmissionViewModel { SoftwareSubmissionID = new Guid(softwareID), FileLengths = new Dictionary<Guid, int>() };
                if (s.Files == null)
                    s.Files = new Dictionary<Guid, HttpPostedFileBase>();
                for (int i = 0; i < Request.Files.Count; i++)
                    s.Files.Add(Guid.NewGuid(), Request.Files[i]);
                _transactions.SubmitSoftware(s);
                rFiles = s.Files;
                rFileLengths = s.FileLengths;
            }
            else if (!string.IsNullOrWhiteSpace(projectPledgeID))
            {
                var s = new VMarketPledgeViewModel { ProjectPledgeID = new Guid(projectPledgeID), FileLengths = new Dictionary<Guid, int>() };
                if (s.Files == null)
                    s.Files = new Dictionary<Guid, HttpPostedFileBase>();
                for (int i = 0; i < Request.Files.Count; i++)
                    s.Files.Add(Guid.NewGuid(), Request.Files[i]);
                _transactions.SubmitProjectPledge(s);
                rFiles = s.Files;
                rFileLengths = s.FileLengths;
            }
            var list = new List<dynamic>();
            foreach (var f in rFiles)
                list.Add(Build<ExpandoObject>.NewObject(name: f.Value.FileName, type: "application/octet", size: rFileLengths[f.Key], url: VirtualPathUtility.ToAbsolute(string.Format("~/share/file/{0}", f.Key))));
            return new JsonHelper.JsonNetResult(new { files = list.ToArray() }, JsonRequestBehavior.AllowGet);
        }


        [Authorize]
        public ActionResult SubmitSoftware()
        {
            if (!_services.Authorizer.Authorize(Permissions.PartnerSoftware, T("Can't submit software")))
                return new HttpUnauthorizedResult();
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
        public ActionResult SubmitProjectPledge()
        {
            if (!_services.Authorizer.Authorize(Permissions.PartnerSoftware, T("Can't submit project pledge")))
                return new HttpUnauthorizedResult();
            var m = new VMarketPledgeViewModel { ProjectPledgeID = Guid.NewGuid() };
            //Show form
            return View(m);
        }

        [Authorize]
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult SubmitProjectPledge(VMarketPledgeViewModel s)
        {
            //Save form
            if (_transactions.SubmitProjectPledge(s))
                return new RedirectResult(System.Web.VirtualPathUtility.ToAbsolute("~/SubmitProjectPledgeConfirmed"));
            return View(s);
        }


        [Authorize]
        public ActionResult MyAccount(string id)
        {
            //Show form [purchased sw, licenses, services, products, partner status, parnter sw, tickets, contracts, affiliates]
            return View();
        }


        [ValidateInput(false)]
        [Authorize]
        //[ValidateAntiForgeryToken]
        [Themed(true)]
        public ActionResult GetInvoice(string id)
        {
            
            return new RedirectResult(VirtualPathUtility.ToAbsolute(string.Format("~/share/download/{0}", _transactions.GetInvoice(new Guid(id), Request.GetIPAddress())))); 
        }

        [ValidateInput(false)]
        [Authorize]
        //[ValidateAntiForgeryToken]
        [Themed(true)]
        public ActionResult GetOrderInvoice(string id)
        {
            return new RedirectResult(VirtualPathUtility.ToAbsolute(string.Format("~/share/download/{0}", _transactions.GetOrderInvoice(new Guid(id), Request.GetIPAddress()))));
        }

        [Authorize]
        [Themed(Enabled = false)]
        public ActionResult PurchasesPartial(AccountViewModel m)
        {
            if (m == null)
                m = new AccountViewModel();
            return View(m);
        }

        [Authorize]
        [Themed(Enabled = false)]
        public ActionResult PledgesPartial(AccountViewModel m)
        {
            if (m == null)
                m = new AccountViewModel();
            return View(m);
        }

        [Authorize]
        [Themed(Enabled = false)]
        public ActionResult ProfilePartial(AccountViewModel m)
        {
            if (m == null)
                m = new AccountViewModel();
            m.Contact = _transactions.GetContact();
            return View(m);
        }

        [Authorize]
        [Themed(Enabled = false)]
        public ActionResult MessagesPartial(AccountViewModel m)
        {
            if (m == null)
                m = new AccountViewModel();
            return View(m);
        }

        [Authorize]
        [Themed(Enabled = false)]
        public ActionResult ReferralsPartial(AccountViewModel m)
        {
            if (m == null)
                m = new AccountViewModel { AffiliateCount = _content.GetAffiliateCount(), AffiliatePoints = _content.GetAffiliatePoints() };
            return View(m);
        }


        [Themed(Enabled = false)]
        [Authorize]
        public ActionResult MyInvoicesPartial(AccountViewModel m)
        {
            if (m == null)
                m = new AccountViewModel();
            if (!m.PageSize.HasValue || m.PageSize > 20)
                m.PageSize = 20;
            if (!m.Offset.HasValue || m.Offset < 1)
                m.Offset = 1;
            if (m.Invoices == null)
                m.Invoices = _transactions.GetInvoices( m.Offset, m.PageSize);
            return View(m);

        }

        [Themed(Enabled = false)]
        [Authorize]
        public ActionResult MyInvoicesPartialPager(AccountViewModel m)
        {
            return View(m);
        }

        [Authorize]
        [Themed(Enabled = false)]
        public ActionResult LicensesPartial(AccountViewModel m)
        {
            if (m == null)
                m = new AccountViewModel();
            return View(m);
        }


        [Themed(Enabled = false)]
        [Authorize]
        public ActionResult MyLicensesPartial(AccountViewModel m)
        {
            if (m == null)
                m = new AccountViewModel();
            if (!m.PageSize.HasValue || m.PageSize > 20)
                m.PageSize = 20;
            if (!m.Offset.HasValue || m.Offset < 1)
                m.Offset = 1;
            if (m.Licenses == null)
                m.Licenses = _transactions.GetLicenses(m.Offset, m.PageSize);
            return View(m);

        }

        [Themed(Enabled = false)]
        [Authorize]
        public ActionResult MyLicensesPartialPager(AccountViewModel m)
        {
            return View(m);
        }

        [Authorize]
        [Themed(Enabled = false)]
        public ActionResult SubmissionsPartial(AccountViewModel m)
        {
            if (m == null)
                m = new AccountViewModel();
            return View(m);
        }


        [Themed(Enabled = false)]
        [Authorize]
        public ActionResult MySoftwareSubmissionsPartial(AccountViewModel m)
        {
            if (m == null)
                m = new AccountViewModel();
            if (!m.PageSize.HasValue || m.PageSize > 20)
                m.PageSize = 20;
            if (!m.Offset.HasValue || m.Offset < 1)
                m.Offset = 1;
            if (m.Licenses == null)
                m.SoftwareSubmissions = _transactions.GetSoftware(m.Offset, m.PageSize);
            return View(m);

        }

        [Themed(Enabled = false)]
        [Authorize]
        public ActionResult MySoftwareSubmissionsPartialPager(AccountViewModel m)
        {
            return View(m);
        }



        [Authorize]
        [HttpGet]
        [Themed(Enabled = false)]
        public ActionResult UpdateProfilePartial()
        {
            var m = new AccountViewModel();
            m.Contact = _transactions.GetContact();
            return View(m);
        }


        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Themed(Enabled = false)]
        public ActionResult UpdateProfilePartial(AccountViewModel m)
        {
            if (m == null || !_transactions.UpdateAccount(m))
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.ExpectationFailed);
            else
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK);
        }

        [Themed(Enabled = false)]
        public ActionResult CartMini()
        {
            dynamic packageDisplay = _services.New.CartMini(
                    Order: _transactions.GetOrderCurrent()
                );
            return new ShapeResult(this, packageDisplay);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Themed(Enabled = false)]
        public ActionResult UpdateCart()
        {
            if (_transactions.UpdateCart(Request.Params))
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK);
            else
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.ExpectationFailed);
        }

        [HttpGet]
        [Themed(Enabled = false)]
        public ActionResult ValidModel(string id)
        {
            if (!User.Identity.IsAuthenticated)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            Guid temp;
            if (!Guid.TryParse(id, out temp))
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.ExpectationFailed);
            return new JsonHelper.JsonNetResult(_transactions.IsUserModelLicenseValid(temp), JsonRequestBehavior.AllowGet);
        }


    }

}
