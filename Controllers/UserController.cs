using System.Web.Mvc;
using Orchard.Localization;
using Orchard;
using Orchard.Themes;
using EXPEDIT.Transactions.ViewModels;
using System;
using System.Linq;
using Braintree;
using EXPEDIT.Transactions.ViewModels;
using EXPEDIT.Transactions.Services;

namespace EXPEDIT.Transactions.Controllers
{
    [Themed]
    public class UserController : Controller
    {
        public IOrchardServices Services { get; set; }
        public ITransactionsService Transactions { get; set; }     
        
        public ActionResult Result()
        {
         
            return View();
        }
    

        public ActionResult Subscriptions()
        {
            return View();
        }
    

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
            return View();
        }

        public ActionResult Test()
        {
            return View();
        }
    }

}
