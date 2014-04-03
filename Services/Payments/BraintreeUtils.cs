using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Braintree;
using EXPEDIT.Transactions.ViewModels;
using NKD.Services;
using ImpromptuInterface;
using System.Collections.Concurrent;
using Orchard;

namespace EXPEDIT.Transactions.Services.Payments
{
    public class BraintreeUtils : IPayment
    {
        private static BraintreeGateway gateway = null;
        private IUsersService _users = null;
        private IMerchant _merchant = null;

        public BraintreeUtils(IUsersService users, IOrchardServices orchardServices)
        {
            _users = users;
            var merchant = new
            {
                Environment = Braintree.Environment.SANDBOX,
                MerchantId = "pfz8b9b2p286yp89",
                PublicKey = "59mqjnsyqypw6dpc",
                PrivateKey = "3478eedc910a0d4793a8d472571851df",
                ClientPublicKey = @"MIIBCgKCAQEAwvQwIlEDkcHNpNfeIDQHIMvhZ/zb6y01QgVRXXbitzxSra5M+5zffgp1fT4vdIseuj435+SuYRmIQU4cHzK18BvZahCuyaOGV6eZIaOhsNTUd2vcSaTud96mxDmbeKfW3Gd2HqugH3RiwL5DpickR3hM6dlaArQBgcZlnpI4qAIVKbPePXk9Nj1aJ7mZOJWuqdwtAY7TkC7zc0lmFQWxZXQsmNSSUf+SY7OpA9mZX0KNs7HN4W0eQyqhQzjrJrrWrlCEWKaJlURZaDQ8fVrIU2Km99O8/yW3/TurCQDHThXFeFPBrul2SQ6ejQHpv93BN1bAiEVqjnr+FqdEA99mgwIDAQAB",
                ServerReturnURL = string.Format("{0}://{1}/store/user/PaymentResult", orchardServices.WorkContext.HttpContext.Request.Url.Scheme, orchardServices.WorkContext.HttpContext.Request.Url.Host)

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

        

        public void PreparePayment(ref OrderViewModel order)
        {
            if (!order.OrderID.HasValue)
                throw new NotSupportedException("Can't make an order without an order ID");
            string token = null;
            if (!string.IsNullOrWhiteSpace(order.PaymentCustomerID))
            {
                try
                {
                    Customer customer = gateway.Customer.Find(order.PaymentCustomerID);
                    if (customer != null && customer.CreditCards != null && customer.CreditCards.Length > 0)
                        token = customer.CreditCards[0].Token;
                }
                catch { }
            }
            order.PaymentRedirectURL = gateway.TransparentRedirect.Url;
            order.PaymentData = gateway.TrData(
                new CustomerRequest
                {
                    CustomerId = order.PaymentCustomerID,
                    CreditCard = new CreditCardRequest()
                    {
                        Options = new CreditCardOptionsRequest()
                        {
                            MakeDefault = true,
                            VerifyCard = true,
                            UpdateExistingToken = token
                        },
                        BillingAddress = new CreditCardAddressRequest
                        {
                            Options = new CreditCardAddressOptionsRequest
                            {
                                UpdateExisting = true
                            }
                        }
                    }
                }
                , string.Format("{0}/{1}/{2}/", _merchant.ServerReturnURL, order.OrderID, order.PaymentAntiForgeryKey)
             );
        }

        public void PreparePaymentResult(ref OrderViewModel order)
        {
            try
            {
                Result<Customer> result = gateway.TransparentRedirect.ConfirmCustomer(order.PaymentQuery);
                if (result.IsSuccess())
                {
                    order.PaymentEmail = result.Target.Email;
                    order.PaymentCompany = result.Target.Company;
                    order.PaymentPhone = result.Target.Phone;
                    order.PaymentFirstname = result.Target.FirstName;
                    order.PaymentLastname = result.Target.LastName;
                    if (result.Target.Addresses != null)
                    {
                        var address = (from o in result.Target.Addresses orderby o.CreatedAt descending select o).FirstOrDefault();
                        if (address != null)
                        {
                            order.PaymentStreet = address.StreetAddress;
                            order.PaymentStreetExtended = address.ExtendedAddress;
                            order.PaymentLocality = address.Locality;
                            order.PaymentRegion = address.Region;
                            order.PaymentPostcode = address.PostalCode;
                            order.PaymentCountry = address.CountryName;
                        }
                    }
                    order.PaymentCustomerID = result.Target.Id;
                    order.PaymentStatus = (uint)PaymentUtils.PaymentStatus.ReceivedCustomer | (uint)PaymentUtils.PaymentStatus.Success;
                }
                else
                {
                    order.PaymentStatus = (uint)PaymentUtils.PaymentStatus.Error;
                    order.PaymentError = (uint)PaymentUtils.PaymentError.BadCustomerID;
                    order.PaymentQueryResponse = string.Join(", ", result.Errors.DeepAll());
                }
            }
            catch
            {
                order.PaymentStatus = (uint)PaymentUtils.PaymentStatus.Error;
                order.PaymentError = (uint)PaymentUtils.PaymentError.BadCustomerID;
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
            if (order.Products.Where(f => string.IsNullOrWhiteSpace(f.PaymentProviderProductName)).Count() > 0)
                throw new NotSupportedException("Only Subscription Products and Services Supported"); //TODO: Support once offs
            order.PaymentPaid = 0;
            order.PaymentReference = "";
            order.PaymentResponse = "";
            foreach (var p in order.Products)
            {                
                //TODO Support non-subscriptions
                var SubscriptionRequest = new SubscriptionRequest
                {
                    PaymentMethodToken = PaymentMethodToken,
                    PlanId = p.PaymentProviderProductName,
                    HasTrialPeriod = false,
                    Price = p.Subtotal                                
                };
                Result<Subscription> result = gateway.Subscription.Create(SubscriptionRequest);

                if (result.IsSuccess())
                {                    
                    order.PaymentStatus = (uint)PaymentUtils.PaymentStatus.Subscribed | (uint)PaymentUtils.PaymentStatus.Success;
                    order.PaymentResponse +=  string.Format("::{0}", result.Target.Status.ToString());
                    order.PaymentPaid += result.Target.Price;
                    order.PaymentReference += string.Format("::{0}", result.Target.Id);
                    order.PaymentResponseShort += string.Format("::{0}", result.Message);
                    p.Paid = true;
                }
                else
                {
                    order.PaymentStatus = (uint)PaymentUtils.PaymentStatus.Error;
                    order.PaymentError = (uint)PaymentUtils.PaymentError.BadPayment;
                    order.PaymentResponse = string.Format("{0},{1}::{2}", result.Message, string.Join(", ", result.Errors.DeepAll(), order.PaymentResponse));
                    order.PaymentResponseShort = result.Message;
                    break;
                }
            }
        }

        public void MakePaymentResult(ref OrderViewModel order)
        {
            
        }
    }
}