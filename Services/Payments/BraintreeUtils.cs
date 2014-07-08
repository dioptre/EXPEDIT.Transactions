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
using EXPEDIT.Share.Helpers;


namespace EXPEDIT.Transactions.Services.Payments
{
    public class BraintreeUtils : IPayment
    {
        private static BraintreeGateway gateway = null;
        private IUsersService _users = null;
        private IOrchardServices _orchardServices = null;
        private IMerchant _merchant = null;


        private static Braintree.Environment _environment = null;
        private static Braintree.Environment environment
        {
            get
            {
                if (_environment == null)
                {
                    string env = string.Format("{0}", System.Configuration.ConfigurationManager.AppSettings["PaymentGateway"]);
                    if (env == "Braintree.Environment.PRODUCTION")
                        _environment = Braintree.Environment.PRODUCTION;
                    else 
                        _environment = Braintree.Environment.SANDBOX;
                }
                return _environment;
            }
        }
        
        
        private static string _merchantID = null;
        private static string merchantID
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_merchantID))
                {
                    _merchantID = string.Format("{0}", System.Configuration.ConfigurationManager.AppSettings["PaymentMerchantID"]);
                    if (string.IsNullOrWhiteSpace(_merchantID))
                        _merchantID = "pfz8b9b2p286yp89";                    
                }
                return _merchantID;
            }
        }

        private static string _publicKey = null;
        private static string publicKey
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_publicKey))
                {
                    _publicKey = string.Format("{0}", System.Configuration.ConfigurationManager.AppSettings["PaymentPublicKey"]);
                    if (string.IsNullOrWhiteSpace(_publicKey))
                        _publicKey = "59mqjnsyqypw6dpc";
                }
                return _publicKey;
            }
        }

        private static string _privateKey = null;
        private static string privateKey
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_privateKey))
                {
                    _privateKey = string.Format("{0}", System.Configuration.ConfigurationManager.AppSettings["PaymentPrivateKey"]);
                    if (string.IsNullOrWhiteSpace(_privateKey))
                        _privateKey = "3478eedc910a0d4793a8d472571851df";
                }
                return _privateKey;
            }
        }

        private static string _clientPublicKey = null;
        private static string clientPublicKey
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_clientPublicKey))
                {
                    _clientPublicKey = string.Format("{0}", System.Configuration.ConfigurationManager.AppSettings["PaymentClientPublicKey"]);
                    if (string.IsNullOrWhiteSpace(_clientPublicKey))
                        _clientPublicKey = @"MIIBCgKCAQEAwvQwIlEDkcHNpNfeIDQHIMvhZ/zb6y01QgVRXXbitzxSra5M+5zffgp1fT4vdIseuj435+SuYRmIQU4cHzK18BvZahCuyaOGV6eZIaOhsNTUd2vcSaTud96mxDmbeKfW3Gd2HqugH3RiwL5DpickR3hM6dlaArQBgcZlnpI4qAIVKbPePXk9Nj1aJ7mZOJWuqdwtAY7TkC7zc0lmFQWxZXQsmNSSUf+SY7OpA9mZX0KNs7HN4W0eQyqhQzjrJrrWrlCEWKaJlURZaDQ8fVrIU2Km99O8/yW3/TurCQDHThXFeFPBrul2SQ6ejQHpv93BN1bAiEVqjnr+FqdEA99mgwIDAQAB";
                }
                return _clientPublicKey;
            }
        }

        public BraintreeUtils(IUsersService users, IOrchardServices orchardServices)
        {
            if (orchardServices.WorkContext.HttpContext == null)
                return;
            _users = users;
            _orchardServices = orchardServices;
            var merchant = new
            {
                Environment = environment,
                MerchantId = merchantID,
                PublicKey = publicKey,
                PrivateKey = privateKey,
                ClientPublicKey = clientPublicKey,
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
            if (gateway == null || _merchant == null)
                throw new NotSupportedException("Require a web context to prepare payment");
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
            if (gateway == null)
                throw new NotSupportedException("Require a web context to prepare payment result");
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
            if (gateway == null)
                throw new NotSupportedException("Require a web context to make payment");
            //var customerRequest = new CustomerSearchRequest().Id.Is(order.PaymentCustomerID);
            //ResourceCollection<Customer> customers = gateway.Customer.Search(customerRequest);
            //// There should only ever be one customer with the given ID
            //Customer customer = customers.FirstItem;
            Customer customer = gateway.Customer.Find(order.PaymentCustomerID);
            string PaymentMethodToken = customer.CreditCards[0].Token;
            if (order.Products.Where(f => !(string.IsNullOrWhiteSpace(f.PaymentProviderProductName) ^ (f.ProductUnitID.HasValue && f.ProductUnitID != ConstantsHelper.UNIT_SI_UNARY))).Count() > 0)
                throw new NotSupportedException("Unsupported product type. Either subscription (Scheduled product/service) or unary product supported but not both.");
            decimal productsCharge = 0m;
            order.PaymentPaid = 0m;
            order.PaymentReference = "";
            order.PaymentResponse = "";
            var isAdmin = _orchardServices.Authorizer.Authorize(Orchard.Security.StandardPermissions.SiteOwner); 
            foreach (var p in order.Products)
            {
                if (!p.ModelUnits.HasValue || p.ModelUnits <= 0)
                    p.ModelUnits = 1m;
                for (int i = 0; i < p.ModelUnits; i++)
                {
                    if (p.ProductUnitID == ConstantsHelper.UNIT_SI_UNARY || !p.ProductUnitID.HasValue)
                    {
                        if (p.Subtotal.HasValue)
                            productsCharge += p.Subtotal.Value;
                    }
                    else
                    {
                        var SubscriptionRequest = new SubscriptionRequest
                        {
                            PaymentMethodToken = PaymentMethodToken,
                            PlanId = p.PaymentProviderProductName,
                            HasTrialPeriod = false,
                            Price = (isAdmin) ? 0m : p.Subtotal / p.ModelUnits
                        };
                        Result<Subscription> subResult = gateway.Subscription.Create(SubscriptionRequest);
                        if (subResult.IsSuccess() || isAdmin)
                        {
                            order.PaymentStatus = (uint)PaymentUtils.PaymentStatus.Subscribed | (uint)PaymentUtils.PaymentStatus.Success;
                            order.PaymentResponse += string.Format("::{0}", subResult.Target.Status.ToString());
                            order.PaymentPaid += subResult.Target.Price;
                            order.PaymentReference += string.Format("::{0}", subResult.Target.Id);
                            order.PaymentResponseShort += string.Format("::{0}", subResult.Message);
                            p.Paid = true;
                        }
                        else
                        {
                            order.PaymentStatus = (uint)PaymentUtils.PaymentStatus.Error;
                            order.PaymentError = (uint)PaymentUtils.PaymentError.BadPayment;
                            order.PaymentResponse = string.Format("{0},{1}::{2}", subResult.Message, string.Join(", ", subResult.Errors.DeepAll()), order.PaymentResponse);
                            order.PaymentResponseShort = subResult.Message;
                            break;
                        }
                    }

                }
            }

            if (productsCharge > 0m)
            {
                if (!isAdmin)
                {
                    //Do Product (non-sbscription billing)
                    TransactionRequest request = new TransactionRequest
                    {
                        Amount = productsCharge,
                        PaymentMethodToken = PaymentMethodToken,
                        Options = new TransactionOptionsRequest
                        {
                            SubmitForSettlement = true
                        }
                    };

                    Result<Transaction> txResult = gateway.Transaction.Sale(request);

                    if (txResult.IsSuccess())
                    {
                        if (order.PaymentStatus != null)
                            order.PaymentStatus = order.PaymentStatus | (uint)PaymentUtils.PaymentStatus.Transacted;
                        else
                            order.PaymentStatus = (uint)PaymentUtils.PaymentStatus.Transacted | (uint)PaymentUtils.PaymentStatus.Success;
                        order.PaymentResponse += string.Format("::{0}", txResult.Target.Status.ToString());
                        order.PaymentPaid += txResult.Target.Amount;
                        order.PaymentReference += string.Format("::{0}", txResult.Target.Id);
                        order.PaymentResponseShort += string.Format("::{0}", txResult.Message);
                        foreach (var p in order.Products)
                            p.Paid = true;
                    }
                    else
                    {
                        order.PaymentStatus = (uint)PaymentUtils.PaymentStatus.Error;
                        order.PaymentError = (uint)PaymentUtils.PaymentError.BadPayment;
                        order.PaymentResponse = string.Format("{0},{1}::{2}", txResult.Message, string.Join(", ", txResult.Errors.DeepAll()), order.PaymentResponse);
                        order.PaymentResponseShort = txResult.Message;
                    }
                }
                else
                {
                    if (order.PaymentStatus != null)
                        order.PaymentStatus = order.PaymentStatus | (uint)PaymentUtils.PaymentStatus.Transacted;
                    else
                        order.PaymentStatus = (uint)PaymentUtils.PaymentStatus.Transacted | (uint)PaymentUtils.PaymentStatus.Success;                    
                    order.PaymentResponse += "::admin";
                    order.PaymentPaid += 0m;
                    order.PaymentReference += "::admin";
                    order.PaymentResponseShort += "::admin";
                    foreach (var p in order.Products)
                        p.Paid = true;
                }
            }
        }

        public void MakePaymentResult(ref OrderViewModel order)
        {
            
        }

        public bool IsSubscriptionValid(string externalReference, out string subscriptionName)
        {
            try
            {
                if (gateway == null)
                    throw new NotSupportedException("Require a web context to facilitate payment introspection.");
                Subscription subscription = gateway.Subscription.Find(externalReference);
                subscriptionName = subscription.PlanId;
                if (subscription.Balance > 0)
                    return false;
                else
                    return true;
            }
            catch
            {
                subscriptionName = null;
                return false;
            }
        }
    }
}