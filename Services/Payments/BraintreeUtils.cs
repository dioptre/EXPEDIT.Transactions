using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Braintree;
using EXPEDIT.Transactions.ViewModels;
using XODB.Services;
using ImpromptuInterface;

namespace EXPEDIT.Transactions.Services.Payments
{
    public class BraintreeUtils : IPayment
    {

        private static BraintreeGateway gateway = null;
        private IUsersService _users;
        private IMerchant _merchant;

        public BraintreeUtils(IUsersService users)
        {
            _users = users;
            var merchant = new
            {
                Environment = Braintree.Environment.SANDBOX,
                MerchantId = "pfz8b9b2p286yp89",
                PublicKey = "59mqjnsyqypw6dpc",
                PrivateKey = "3478eedc910a0d4793a8d472571851df",
                ClientPublicKey = @"MIIBCgKCAQEAwvQwIlEDkcHNpNfeIDQHIMvhZ/zb6y01QgVRXXbitzxSra5M+5zffgp1fT4vdIseuj435+SuYRmIQU4cHzK18BvZahCuyaOGV6eZIaOhsNTUd2vcSaTud96mxDmbeKfW3Gd2HqugH3RiwL5DpickR3hM6dlaArQBgcZlnpI4qAIVKbPePXk9Nj1aJ7mZOJWuqdwtAY7TkC7zc0lmFQWxZXQsmNSSUf+SY7OpA9mZX0KNs7HN4W0eQyqhQzjrJrrWrlCEWKaJlURZaDQ8fVrIU2Km99O8/yW3/TurCQDHThXFeFPBrul2SQ6ejQHpv93BN1bAiEVqjnr+FqdEA99mgwIDAQAB",
                ServerReturnURL = "http://eodb/store/user/PaymentResult/"

            };
            _merchant = merchant.ActLike<IMerchant>();
            if (gateway == null)
                gateway = new BraintreeGateway
                {
                    Environment = (Braintree.Environment)_merchant.Environment,
                    MerchantId = _merchant.MerchantId,
                    PublicKey = _merchant.PublicKey,
                    PrivateKey = _merchant.PrivateKey
                };
        }

        Braintree.Environment EnvironmentObject {get;set;}

        public void PreparePayment(ref OrderViewModel order)
        {          
            order.PaymentRedirectURL = gateway.TransparentRedirect.Url;
            order.PaymentData = gateway.TrData(
                new CustomerRequest 
                {
                    //FirstName = order.PaymentFirstname,
                    //LastName = order.PaymentLastname,
                    //CreditCard = new CreditCardRequest
                    //{
                    //    BillingAddress = new CreditCardAddressRequest
                    //    {
                    //        PostalCode = order.PaymentPostcode
                    //    },
                    //    Number = order.PaymentNumber,
                    //    ExpirationMonth = order.PaymentExpirationMonth,
                    //    ExpirationYear = order.PaymentExpirationYear,
                    //    CVV = order.PaymentVerification
                    //}
                }
                , string.Format("{0}{1}", _merchant.ServerReturnURL, order.OrderID)
             );
        }

        public void PreparePaymentResult(ref OrderViewModel order)
        {

            Result<Customer> result = gateway.TransparentRedirect.ConfirmCustomer(order.PaymentQuery);
            if (result.IsSuccess())
            {
                order.PaymentQueryResponse = result.Target.Email;
                order.PaymentCustomerID = result.Target.Id;
                order.PaymentStatus = (uint)PaymentUtils.PaymentStatus.ReceivedCustomer;
            }
            else
            {
                order.PaymentStatus = (uint)PaymentUtils.PaymentStatus.Error;
                order.PaymentError = (uint)PaymentUtils.PaymentError.BadCustomerID;
                order.PaymentQueryResponse = string.Join(", ", result.Errors.DeepAll());
            }

        }

        public void MakePayment(ref OrderViewModel order)
        {
            //var customerRequest = new CustomerSearchRequest().Id.Is(order.PaymentCustomerID);
            //ResourceCollection<Customer> customers = gateway.Customer.Search(customerRequest);
            //// There should only ever be one customer with the given ID
            //Customer customer = customers.FirstItem;
            Customer customer = gateway.Customer.Find(order.PaymentCustomerID);
            string PaymentMethodToken = customer.CreditCards[0].Token;
            foreach (var p in order.Products)
            {
                var SubscriptionRequest = new SubscriptionRequest
                {
                    PaymentMethodToken = PaymentMethodToken,
                    PlanId = p.PaymentProviderProductName
                };
                Result<Subscription> result = gateway.Subscription.Create(SubscriptionRequest);

                if (result.IsSuccess())
                {
                    order.PaymentStatus = (uint)PaymentUtils.PaymentStatus.Subscribed;
                    order.PaymentResponse = result.Target.Status.ToString();
                    p.Paid = true;
                }
                else
                {
                    order.PaymentStatus = (uint)PaymentUtils.PaymentStatus.Error;
                    order.PaymentError = (uint)PaymentUtils.PaymentError.BadPayment;
                    order.PaymentResponse = string.Join(", ", result.Errors.DeepAll());
                    break;
                }
            }
        }

        public void MakePaymentResult(ref OrderViewModel order)
        {
            
        }
    }
}