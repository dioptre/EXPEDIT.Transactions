using System.Web.Mvc;
using Orchard.Localization;
using Orchard;
using Orchard.Themes;
using EXPEDIT.Transactions.ViewModels;
using System;
using System.Linq;
using Braintree;
using EXPEDIT.Transactions.Services.Payments;

namespace EXPEDIT.Transactions.Controllers
{
    //[Themed]
    public class UserController : Controller
    {
        public IOrchardServices Services { get; set; }
             
        
        public ActionResult Result()
        {
            Result<Customer> result = BraintreeUtils.Gateway.TransparentRedirect.ConfirmCustomer(Request.Url.Query);
            if (result.IsSuccess())
            {
                ViewData["Message"] = result.Target.Email;
                ViewData["CustomerId"] = result.Target.Id;
            }
            else
            {
                ViewData["Message"] = string.Join(", ", result.Errors.DeepAll());
            }

            return View();
        }
    

        public ActionResult Subscriptions()
        {
            var customerRequest = new CustomerSearchRequest().
                Id.Is(Request.QueryString["id"]);
            ResourceCollection<Customer> customers = BraintreeUtils.Gateway.Customer.Search(customerRequest);
            // There should only ever be one customer with the given ID
            Customer customer = customers.FirstItem;
            string PaymentMethodToken = customer.CreditCards[0].Token;
            var SubscriptionRequest = new SubscriptionRequest
            {
                PaymentMethodToken = PaymentMethodToken,
                PlanId = "test_plan_1"
            };
            Result<Subscription> result = BraintreeUtils.Gateway.Subscription.Create(SubscriptionRequest);

            if (result.IsSuccess())
            {
                ViewData["Message"] = result.Target.Status;
            }
            else
            {
                ViewData["Message"] = string.Join(", ", result.Errors.DeepAll());
            }

            return View();
        }
    

        public UserController(IOrchardServices services)
        {
            Services = services;
            T = NullLocalizer.Instance;
        }

        public Localizer T { get; set; }

        [ValidateInput(false)]
        public ActionResult Index()
        {
            var m = new TransactionsViewModel { TransactionsID = Guid.NewGuid() };
            using (new System.Transactions.TransactionScope(System.Transactions.TransactionScopeOption.Suppress))
            {
                using (var c = new EXPEDIT.Utils.DAL.Models.EODBC(EXPEDIT.Utils.DAL.DefaultConnectionString.DefaultConfigString, null))
                {
                    m.TransactionsID = (from o in c.Companies select o).FirstOrDefault().CompanyID;
                }
            }

            ViewData["TrData"] = BraintreeUtils.Gateway.TrData(
                new CustomerRequest { },
                "http://localhost/Transactions/User/Result"
            );
            ViewData["TransparentRedirectURL"] = BraintreeUtils.Gateway.TransparentRedirect.Url;

            return View("Index", m);
        }

        public ActionResult Test()
        {
            return View("Test");
        }
    }

}
