using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Braintree;
using EXPEDIT.Transactions.ViewModels;

namespace EXPEDIT.Transactions.Services.Payments
{
    public class BraintreeUtils : IPayment
    {

        public static BraintreeGateway Gateway = new BraintreeGateway
        {
            Environment = Braintree.Environment.SANDBOX,
            MerchantId = "pfz8b9b2p286yp89",
            PublicKey = "59mqjnsyqypw6dpc",
            PrivateKey = "3478eedc910a0d4793a8d472571851df"
        };

        public static string ClientSideEncryptionKey = @"MIIBCgKCAQEAwvQwIlEDkcHNpNfeIDQHIMvhZ/zb6y01QgVRXXbitzxSra5M+5zffgp1fT4vdIseuj435+SuYRmIQU4cHzK18BvZahCuyaOGV6eZIaOhsNTUd2vcSaTud96mxDmbeKfW3Gd2HqugH3RiwL5DpickR3hM6dlaArQBgcZlnpI4qAIVKbPePXk9Nj1aJ7mZOJWuqdwtAY7TkC7zc0lmFQWxZXQsmNSSUf+SY7OpA9mZX0KNs7HN4W0eQyqhQzjrJrrWrlCEWKaJlURZaDQ8fVrIU2Km99O8/yW3/TurCQDHThXFeFPBrul2SQ6ejQHpv93BN1bAiEVqjnr+FqdEA99mgwIDAQAB";

        public bool MakePayment(OrderViewModel order)
        {
            throw new NotImplementedException();
            //m = new TransactionsViewModel { TransactionsID = Guid.NewGuid() };
            //using (new System.Transactions.TransactionScope(System.Transactions.TransactionScopeOption.Suppress))
            //{
            //    using (var c = new EXPEDIT.Utils.DAL.Models.EODBC(EXPEDIT.Utils.DAL.DefaultConnectionString.DefaultConfigString, null))
            //    {
            //        m.TransactionsID = (from o in c.Companies select o).FirstOrDefault().CompanyID;
            //    }
            //}

            //ViewData["TrData"] = BraintreeUtils.Gateway.TrData(
            //    new CustomerRequest { },
            //    "http://localhost/Transactions/User/Result"
            //);
            //ViewData["TransparentRedirectURL"] = BraintreeUtils.Gateway.TransparentRedirect.Url;




            //var customerRequest = new CustomerSearchRequest().
            // Id.Is(Request.QueryString["id"]);
            //ResourceCollection<Customer> customers = BraintreeUtils.Gateway.Customer.Search(customerRequest);
            //// There should only ever be one customer with the given ID
            //Customer customer = customers.FirstItem;
            //string PaymentMethodToken = customer.CreditCards[0].Token;
            //var SubscriptionRequest = new SubscriptionRequest
            //{
            //    PaymentMethodToken = PaymentMethodToken,
            //    PlanId = "test_plan_1"
            //};
            //Result<Subscription> result = BraintreeUtils.Gateway.Subscription.Create(SubscriptionRequest);

            //if (result.IsSuccess())
            //{
            //    ViewData["Message"] = result.Target.Status;
            //}
            //else
            //{
            //    ViewData["Message"] = string.Join(", ", result.Errors.DeepAll());
            //}


            //Result<Customer> result = BraintreeUtils.Gateway.TransparentRedirect.ConfirmCustomer(Request.Url.Query);
            //if (result.IsSuccess())
            //{
            //    ViewData["Message"] = result.Target.Email;
            //    ViewData["CustomerId"] = result.Target.Id;
            //}
            //else
            //{
            //    ViewData["Message"] = string.Join(", ", result.Errors.DeepAll());
            //}




        }
    }
}