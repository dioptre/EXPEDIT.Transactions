using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using JetBrains.Annotations;
using Orchard.ContentManagement;
using Orchard.FileSystems.Media;
using Orchard.Localization;
using Orchard.Security;
using Orchard.Settings;
using Orchard.Validation;
using Orchard;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Transactions;
using Orchard.Messaging.Services;
using Orchard.Logging;
using Orchard.Tasks.Scheduling;
using Orchard.Data;
#if XODB
using XODB.Module.BusinessObjects;
#else
using EXPEDIT.Utils.DAL.Models;
#endif
using EXPEDIT.Transactions.ViewModels;
using XODB.Services;
using Orchard.Media.Services;
using EXPEDIT.Transactions.Services.Payments;
using EntityFramework.Extensions;
using Newtonsoft.Json;
using EXPEDIT.Share.Helpers;
using XODB.Helpers;
using XODB.Models;
using RestSharp;
using System.Security.Cryptography;

namespace EXPEDIT.Transactions.Services {
    
    [UsedImplicitly]
    public class TransactionsService : ITransactionsService {
        private readonly IOrchardServices _orchardServices;
        private readonly IContentManager _contentManager;
        private readonly IMessageManager _messageManager;
        private readonly IScheduledTaskManager _taskManager;
        private readonly IUsersService _users;
        private readonly IMediaService _media;
        private readonly IPayment _payment;
        public ILogger Logger { get; set; }

        public TransactionsService(
            IContentManager contentManager, 
            IOrchardServices orchardServices, 
            IMessageManager messageManager, 
            IScheduledTaskManager taskManager, 
            IUsersService users,  
            IMediaService media,
            IPayment payment)
        {
            _orchardServices = orchardServices;
            _contentManager = contentManager;
            _messageManager = messageManager;
            _taskManager = taskManager;
            _media = media;
            _users = users;
            T = NullLocalizer.Instance;
            Logger = NullLogger.Instance;
            _payment = payment;
        }

        public Localizer T { get; set; }

        public void MakePayment(ref OrderViewModel order)
        {
            _payment.MakePayment(ref order);
        }

        public void MakePaymentResult(ref OrderViewModel order)
        {
            _payment.MakePaymentResult(ref order);
        }

        public void PreparePayment(ref OrderViewModel order)
        {
            //ContractConditions
            var contact = _users.ContactID;
            var orderID = order.OrderID;
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new XODBC(_users.ApplicationConnectionString, null);
                var contracts = (from o in d.SupplyContractConditions where o.Supply.CustomerPurchaseOrderID == orderID && o.Version == 0 && o.VersionDeletedBy == null
                             select o);
                foreach (var c in contracts)
                {
                    c.AgreedByContactID = contact;
                    c.Agreed = DateTime.UtcNow;
                }
                order.PaymentAntiForgeryKey = Guid.NewGuid();
                var md = new MetaData { MetaDataID = order.PaymentAntiForgeryKey.Value, MetaDataType = ConstantsHelper.METADATA_ANTIFORGERY, ContentToIndex = string.Format("{0}", orderID) };
                d.MetaDatas.AddObject(md);                        
                d.SaveChanges();
            }
            GetOrderOwner(ref order);
            _payment.PreparePayment(ref order);
        }

        public void PreparePaymentResult(ref OrderViewModel order)
        {
            var orderID = string.Format("{0}", order.OrderID.Value);
            var contactID = _users.ContactID;
            var antiForgeryKey = order.PaymentAntiForgeryKey;
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new XODBC(_users.ApplicationConnectionString, null);
                var md = (from o in d.MetaDatas 
                          where o.MetaDataID==antiForgeryKey.Value
                          && o.MetaDataType == ConstantsHelper.METADATA_ANTIFORGERY 
                          && o.ContentToIndex == orderID
                          && o.Version == 0
                          && o.VersionDeletedBy == null
                          select o).Single();
                d.MetaDatas.DeleteObject(md);
                d.SaveChanges();
            }
            _payment.PreparePaymentResult(ref order);
            if (order.PaymentError == (uint)PaymentUtils.PaymentError.BadCustomerID)
            {
                //Force Update Contact on Server Side
                using (new TransactionScope(TransactionScopeOption.Suppress))
                {
                    var d = new XODBC(_users.ApplicationConnectionString, null);
                    d.ApplicationPaymentProviderContacts.Where(f => f.ContactID == contactID).Delete();
                }
            }

        }
         

        public OrderViewModel GetOrder(Guid orderID)
        {
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new XODBC(_users.ApplicationConnectionString, null);
                var items = (from o in d.SupplyItems where o.Supply.CustomerPurchaseOrderID==orderID && o.Version==0 && o.VersionDeletedBy == null 
                             select new OrderProductViewModel { 
                                 SupplierModelID = o.SupplierModelID, 
                                 SupplierPartID = o.SupplierPartID,
                                 ModelID=o.ModelID, 
                                 PartID=o.PartID, 
                                 PaymentProviderProductID = o.ApplicationPaymentProviderProductID,
                                 PaymentProviderProductName= (o.ApplicationPaymentProviderProduct==null) ? null : o.ApplicationPaymentProviderProduct.PaymentProviderProductName 
                             });
                var m = new OrderViewModel { OrderID = orderID, Products = items.ToList() };
                return m;
            }
        }

        public bool GetOrderPaid(Guid orderID)
        {
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new XODBC(_users.ApplicationConnectionString, null);
                return (from po in d.PurchaseOrders where po.PurchaseOrderID==orderID
                 join s in d.Supplies on po.PurchaseOrderID equals s.CustomerPurchaseOrderID
                 join i in d.Invoices on s.SupplyID equals i.SupplyID
                 join pi in d.PaymentInvoices on i.InvoiceID equals pi.InvoiceID
                 where pi.IsFinalPaymentInvoice == true
                 select po.PurchaseOrderID).Any();
            }
        }

        public bool GetOrderProcessed(Guid orderID)
        {
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new XODBC(_users.ApplicationConnectionString, null);
                return (from po in d.PurchaseOrders
                        where po.PurchaseOrderID == orderID
                        join s in d.Supplies on po.PurchaseOrderID equals s.CustomerPurchaseOrderID
                        join i in d.Invoices on s.SupplyID equals i.SupplyID
                        join pi in d.PaymentInvoices on i.InvoiceID equals pi.InvoiceID
                        select po.PurchaseOrderID).Any();
            }
        }

        public void GetOrderOwner(ref OrderViewModel order)
        {
            var contact = _users.ContactID;
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new XODBC(_users.ApplicationConnectionString, null);
                var ppProducts = order.Products.Where(f => f.PaymentProviderProductID != null).Select(f => f.PaymentProviderProductID.Value).ToArray();
                order.PaymentCustomerID = (from o in d.ApplicationPaymentProviderProducts
                                           join p in d.ApplicationPaymentProviderContacts on o.ApplicationPaymentProviderID equals p.ApplicationPaymentProviderID
                                           where ppProducts.Contains(o.ApplicationPaymentProviderProductID)
                                           && p.ContactID == contact
                                           && p.Version == 0
                                           && p.VersionDeletedBy == null
                                           && p.CustomerReference != null
                                           orderby p.CustomerReference descending
                                           select p.CustomerReference).FirstOrDefault();
                var m = (from o in d.Addresses
                         join ca in d.ContactAddresses on o.AddressID equals ca.AddressID
                         where ca.ContactID == contact && ca.Version==0 && ca.VersionDeletedBy==null && o.Version==0 && o.VersionDeletedBy==null
                         orderby o.IsBusiness descending
                         orderby o.Sequence descending
                         select o).FirstOrDefault();
                if (m != null)
                {
                    order.PaymentStreet = m.Street;
                    order.PaymentStreetExtended = m.Extended;
                    order.PaymentLocality = m.City;
                    order.PaymentRegion = m.State;
                    order.PaymentPostcode = m.Postcode;
                    order.PaymentCountry = m.Country;
                    order.PaymentPhone = m.Phone;
                    order.PaymentCompany = m.AddressName;
                }
            
                var c = (from o in d.Contacts where o.ContactID == contact select o).FirstOrDefault();
                if (c != null)
                {
                    order.PaymentFirstname = c.Firstname;
                    order.PaymentLastname = c.Surname;
                    order.PaymentEmail = c.DefaultEmail;
                    if (string.IsNullOrWhiteSpace(order.PaymentPhone))
                        order.PaymentPhone = c.DefaultMobile;
                }
                else
                {
                    order.PaymentEmail = _users.Email;
                }
                
            }
        }

        public void UpdateOrderOwner(OrderViewModel order)
        {
            var customerID = order.PaymentCustomerID;
            var contact = _users.ContactID;
            var ppProducts = order.Products.Where(f => f.PaymentProviderProductID != null).Select(f => f.PaymentProviderProductID.Value).ToArray();
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                //Update customer id
                var d = new XODBC(_users.ApplicationConnectionString, null);
                var pc = (from o in d.ApplicationPaymentProviderProducts
                          join p in d.ApplicationPaymentProviderContacts on o.ApplicationPaymentProviderID equals p.ApplicationPaymentProviderID
                          where ppProducts.Contains(o.ApplicationPaymentProviderProductID)
                          && p.ContactID == contact
                          && p.Version == 0
                          && p.VersionDeletedBy == null
                          select p).Distinct();
                var old = from o in pc where o.CustomerReference != customerID select o;
                foreach (var o in old)
                    d.ApplicationPaymentProviderContacts.DeleteObject(o);
                if (!string.IsNullOrWhiteSpace(customerID) && !pc.Any(f => f.CustomerReference == customerID))
                {
                    var pp = (from o in d.ApplicationPaymentProviderProducts
                              join p in d.ApplicationPaymentProviders on o.ApplicationPaymentProviderID equals p.ApplicationPaymentProviderID
                              where ppProducts.Contains(o.ApplicationPaymentProviderProductID)
                              && p.Version == 0
                              && p.VersionDeletedBy == null
                              select p.ApplicationPaymentProviderID).First();
                    var ppc = new ApplicationPaymentProviderContact
                    {
                        ApplicationPaymentProviderID = pp,
                        ApplicationPaymentProviderContactID = Guid.NewGuid(),
                        CustomerReference = customerID,
                        ContactID = contact
                    };
                    d.ApplicationPaymentProviderContacts.AddObject(ppc);
                }

                //d.Addresses.Where(f => f.Version == 0 && f.VersionDeletedBy == null).Update(f => new Address { Sequence = f.Sequence+1 }); 
                var m = (from o in d.Addresses
                         join ca in d.ContactAddresses on o.AddressID equals ca.AddressID
                         where ca.ContactID == contact && ca.Version == 0 && ca.VersionDeletedBy == null && o.Version == 0 && o.VersionDeletedBy == null
                         orderby o.IsBusiness descending
                         orderby o.Sequence descending
                         select o).FirstOrDefault();
                if (m != null)
                {
                    if (m.AddressName != order.PaymentCompany)
                        m.AddressName = order.PaymentCompany;
                    if (m.Phone != order.PaymentPhone)
                        m.Phone = order.PaymentPhone;
                    if (m.Street != order.PaymentStreet)
                        m.Street = order.PaymentStreet;
                    if (m.Extended != order.PaymentStreetExtended)
                        m.Extended = order.PaymentStreetExtended;
                    if (m.City != order.PaymentLocality)
                        m.City = order.PaymentLocality;
                    if (m.State != order.PaymentRegion)
                        m.State = order.PaymentRegion;
                    if (m.Postcode != order.PaymentPostcode)
                        m.Postcode = order.PaymentPostcode;
                    if (m.Country != order.PaymentCountry)
                        m.Country = order.PaymentCountry;
                    if (m.Email != order.PaymentEmail)
                        m.Email = order.PaymentEmail;
                    if (!m.IsBusiness)
                        m.IsBusiness = true;
                    if (m.EntityState == System.Data.EntityState.Modified)
                    {
                        var max = (from o in d.Addresses
                                   join ca in d.ContactAddresses on o.AddressID equals ca.AddressID
                                   where ca.ContactID == contact
                                   select o.Sequence).Max();
                        m.Sequence = max + 1;
                    }
                }
                else
                {
                    Guid addressID = Guid.NewGuid();
                    m = new Address { AddressID = addressID };
                    m.Street = order.PaymentStreet;
                    m.Extended = order.PaymentStreetExtended;
                    m.City = order.PaymentLocality;
                    m.State = order.PaymentRegion;
                    m.Postcode = order.PaymentPostcode;
                    m.Email = order.PaymentEmail;
                    m.IsBusiness = true;
                    m.Sequence = 0;
                    d.Addresses.AddObject(m);
                    d.ContactAddresses.AddObject(
                        new ContactAddress
                        {
                            ContactAddressID = Guid.NewGuid(),
                            AddressID = addressID,
                            ContactID = contact
                        }
                        );

                    
                }
                order.PaymentAddressID = m.AddressID;

                var c = (from o in d.Contacts where o.ContactID == contact select o).Single();
                if (string.IsNullOrWhiteSpace(c.Firstname))
                    c.Firstname = order.PaymentFirstname;
                if (string.IsNullOrWhiteSpace(c.Surname))
                    c.Surname = order.PaymentLastname;

                d.SaveChanges();
            }
        }

        public void UpdateOrderPaid(OrderViewModel order)
        {
            var application = _users.ApplicationID;
            var customerID = order.PaymentCustomerID;
            var contact = _users.ContactID;
            var warnings = new List<string>();
            var now = DateTime.UtcNow;
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                //Update customer id
                var d = new XODBC(_users.ApplicationConnectionString, null);
                //var transaction = d.BeginTransaction(System.Data.IsolationLevel.ReadCommitted); //TODO implement transactions
                PurchaseOrder po = (from o in d.PurchaseOrders where order.OrderID.HasValue && o.PurchaseOrderID == order.OrderID select o).Single();
                Supply s = (from o in d.Supplies where o.CustomerPurchaseOrderID == po.PurchaseOrderID select o).Single();
                var oldItems = (from o in d.SupplyItems.Include("UnitModel") where o.Supply.CustomerPurchaseOrderID == po.PurchaseOrderID select o).ToList();
                Invoice i = new Invoice
                { 
                    InvoiceID = GuidHelper.NewComb() ,
                    CurrencyID = oldItems.Any() ? oldItems.First().CurrencyID : default(Guid?),
                    CustomerContactID = contact,
                    CustomerAddressID = order.PaymentAddressID,
                    Dated = now,
                    SupplyID = s.SupplyID,
                    CustomerReferenceNumber = order.PaymentCustomerID
                };
                d.Invoices.AddObject(i);
                var ppProducts = order.Products.Where(f => f.PaymentProviderProductID != null).Select(f => f.PaymentProviderProductID.Value).ToArray();
                var currentConditions = new List<ContractConditionViewModel>();
                var processedSupplyItems = new List<SupplyItem>();
                var sequence = 0;
                foreach (var p in order.Products)
                {
                    //SupplierModel, Model, Part, Count
                    var items = (from o in oldItems
                                where
                                    o.SupplierModelID == p.SupplierModelID
                                    && o.SupplierPartID == p.SupplierPartID
                                    && o.ModelID == p.ModelID
                                    && o.PartID == p.PartID
                                select o);
                    if (p.CurrencyID != null && p.CurrencyID != i.CurrencyID)
                        warnings.Add(string.Format("Disparity in product currencies, order: ({0}) basket product ({1})", order.OrderID, JsonConvert.SerializeObject(p)));
                    if (!items.Any())                    
                        warnings.Add(string.Format("Badly processed order ({0}) missing basket product ({1})" , order.OrderID, JsonConvert.SerializeObject(p)));
                    else
                    {
                        //Could group tegether supplyitems but not doing it
                        //items.Sum(f => f.QuantityLabour + f.QuantityModel + f.QuantityPart);

                        foreach (var supplyItem in items)
                        {

                            processedSupplyItems.Add(supplyItem);
                            if (i.CurrencyID != supplyItem.CurrencyID)
                                warnings.Add(string.Format("Disparity in product currencies, order: ({0}) basket product ({1})", order.OrderID, JsonConvert.SerializeObject(p)));                           

                            if (supplyItem.SupplierPartID.HasValue && supplyItem.QuantityPart > 0m)
                            {
                                InvoiceLine lineItem = new InvoiceLine
                                {
                                    InvoiceLineID = Guid.NewGuid(),
                                    InvoiceID = i.InvoiceID,
                                    CurrencyID = supplyItem.CurrencyID,
                                    SupplyItemID = supplyItem.SupplyItemID,
                                    ReferenceType = d.GetTableName(typeof(SupplierPart)),
                                    ReferenceID = supplyItem.SupplierPartID,
                                    Description = (from o in d.SupplierParts where o.SupplierPartID == supplyItem.SupplierPartID select o.Part.StandardPartName).FirstOrDefault(),
                                    Quantity = supplyItem.QuantityPart,
                                    Tax = (supplyItem.TaxPart ?? 0m), //TODO: If taxation gets complicated use itemtax table
                                    OriginalSubtotal = (supplyItem.CostPart ?? 0m),
                                    Subtotal = (supplyItem.SubtotalPart ?? 0m), //TODO: Discounts
                                    Sequence = sequence
                                };
                                i.InvoiceLine.Add(lineItem);
                                sequence++;

                                //TODO: if we add part as an asset, it should go in assetdata table
                                //TODO: if we add part, should add as license to licenseasset
                            }

                            if (supplyItem.QuantityLabour > 0m)
                            {
                                InvoiceLine lineItem = new InvoiceLine
                                {
                                    InvoiceLineID = Guid.NewGuid(),
                                    InvoiceID = i.InvoiceID,
                                    CurrencyID = supplyItem.CurrencyID,
                                    SupplyItemID = supplyItem.SupplyItemID,
                                    ReferenceType = ConstantsHelper.REFERENCE_TYPE_LABOUR,
                                    //ReferenceID = TODO
                                    Description = "Labour",
                                    Quantity = supplyItem.QuantityLabour,
                                    Tax = (supplyItem.TaxLabour ?? 0m), //TODO: If taxation gets complicated use itemtax table
                                    OriginalSubtotal = (supplyItem.CostLabour ?? 0m),
                                    Subtotal = (supplyItem.SubtotalLabour ?? 0m) //TODO: Discounts
                                };
                                i.InvoiceLine.Add(lineItem);

                            }

                            if (supplyItem.SupplierModelID.HasValue && supplyItem.QuantityModel > 0m)
                            {
                                var mu = supplyItem.UnitModel;
                                var muString = (mu != null) ? string.Format(" - [{0}]", mu.StandardUnitName).ToUpper() : "";
                                var mn = (from o in d.SupplierModels where o.SupplierModelID == supplyItem.SupplierModelID select o.Model.StandardModelName).FirstOrDefault();
                                InvoiceLine lineItem = new InvoiceLine
                                {
                                    InvoiceLineID = Guid.NewGuid(),
                                    InvoiceID = i.InvoiceID,
                                    CurrencyID = supplyItem.CurrencyID,
                                    SupplyItemID = supplyItem.SupplyItemID,
                                    ReferenceType = d.GetTableName(typeof(SupplierModel)),
                                    ReferenceID = supplyItem.SupplierModelID,
                                    Description = string.Format("{0}{1}",
                                        mn,
                                        muString
                                        ),
                                    Quantity = supplyItem.QuantityModel,
                                    Tax = (supplyItem.TaxModel ?? 0m), //TODO: If taxation gets complicated use itemtax table
                                    OriginalSubtotal = (supplyItem.CostModel ?? 0m),
                                    Subtotal = (supplyItem.SubtotalModel ?? 0m) //TODO: Discounts
                                };
                                i.InvoiceLine.Add(lineItem);

                                //Asset TODO:Check to update old asset
                                var assetID = Guid.NewGuid();
                                var asset = new Asset
                                {
                                    AssetID = assetID,
                                    AssetName = string.Format("{0} [{1}]", string.Join(null, mn.Take(60 - 42)), assetID),
                                    InitialCost = lineItem.Subtotal,
                                    ProRataCost = supplyItem.CostPerUnitModel,
                                    ProRataUnitID = supplyItem.ModelUnitID,
                                    Purchased = now,
                                    PurchaseOrderID = order.OrderID,
                                    ModelID = supplyItem.ModelID,
                                    CurrentContactID = contact
                                };
                                d.Assets.AddObject(asset);
                                DateTime? nextMaintenance = default(DateTime?);
                                if (mu != null)
                                {
                                    if (mu.UnitID == ConstantsHelper.UNIT_SI_SECONDS)
                                        nextMaintenance = now.AddSeconds(Convert.ToDouble(supplyItem.QuantityModel.Value));
                                    else if (mu.EquivalentUnitID == ConstantsHelper.UNIT_SI_SECONDS)
                                        nextMaintenance = now.AddSeconds(Convert.ToDouble(supplyItem.QuantityModel.Value * mu.EquivalentMultiplier));
                                    else
                                        warnings.Add(string.Format("Product unit description not time based. Could not update maintenance schedule. Order: ({0}) Asset: ({1})", order.OrderID, asset.AssetID));
                                }
                                var assetMaintenance = new AssetMaintenance
                                {
                                    AssetMaintenanceID = Guid.NewGuid(),
                                    AssetID = asset.AssetID,
                                    NextDueDateBilling = nextMaintenance
                                };
                                asset.AssetMaintenance.Add(assetMaintenance);
                                var license = new License
                                {
                                    LicenseID = Guid.NewGuid(),
                                    LicenseeGUID = contact,
                                    ContactID = contact,
                                    ValidForUnitID = supplyItem.ModelUnitID,
                                    ValidForDuration = supplyItem.QuantityModel,
                                    ValidFrom = now,
                                    Expiry = nextMaintenance,
                                    SupportExpiry = nextMaintenance,
                                    ApplicationID = application,
                                    ServiceAuthorisationMethod = ConstantsHelper.LICENSE_SERVER_AUTH_METHOD
                                };
                                d.Licenses.AddObject(license);
                                var licenseAsset = new LicenseAsset
                                {
                                    LicenseAssetID = Guid.NewGuid(),
                                    LicenseID = license.LicenseID,
                                    AssetID = asset.AssetID,
                                    ModelID = supplyItem.ModelID
                                };
                                d.LicenseAssets.AddObject(licenseAsset);

                                var m = (from o in d.DictionaryModels where o.ModelID==supplyItem.ModelID select o).FirstOrDefault();
                                if (m != null)
                                {
                                    if (m.UserGuideFileDataID.HasValue)
                                        d.Downloads.AddObject(new Download
                                        {
                                            DownloadID = Guid.NewGuid(),
                                            FileAllocated = now,
                                            FilterContactID = contact,
                                            RemainingDownloads = ConstantsHelper.DOWNLOADS_REMAINING_DEFAULT,
                                            FileDataID = m.UserGuideFileDataID.Value,
                                            LicenseID = license.LicenseID,
                                            LicenseAssetID = licenseAsset.LicenseAssetID,
                                            ValidFrom = now,
                                            ValidUntil = nextMaintenance
                                        });
                                    if (m.SecureFileDataID.HasValue)
                                        d.Downloads.AddObject(new Download
                                        {
                                            DownloadID = Guid.NewGuid(),
                                            FileAllocated = now,
                                            FilterContactID = contact,
                                            RemainingDownloads = ConstantsHelper.DOWNLOADS_REMAINING_DEFAULT,
                                            FileDataID = m.SecureFileDataID.Value,
                                            LicenseID = license.LicenseID,
                                            LicenseAssetID = licenseAsset.LicenseAssetID,
                                            ValidFrom = now,
                                            ValidUntil = nextMaintenance
                                        });
                                }

                            }
                        }

                    }

                   

                }
                //warning if anything left over
                var leftOvers = (from o in oldItems where !(from processed in processedSupplyItems select processed.SupplyItemID).Contains(o.SupplyItemID) select o);
                foreach (var oldItem in leftOvers)
                    warnings.Add(string.Format("Badly processed order ({0}), spurious basket product: ({1})", order.OrderID, JsonConvert.SerializeObject(oldItem)));


                //invoice
                i.TaxAmount = i.InvoiceLine.Sum(f => f.Tax);
                i.OriginalTotal = i.InvoiceLine.Sum(f => f.OriginalSubtotal);
                i.Total = i.InvoiceLine.Sum(f=>f.Subtotal); //Todo: Discounts
                
                //supply
                s.CustomerApprovedBy = contact;
                s.IsSupplied = true;
                s.IsPaid = true; //started payment, finalised=cancelled

                //PurchaseOrder
                //TODO: update orderstatus
               
                //payment
                var pay = new Payment
                {
                    PaymentID = Guid.NewGuid(),
                    ExternalReferenceName = order.PaymentReference,
                    CustomerContactID = contact,
                    OriginalAmount = i.Total,
                    CurrencyID = i.CurrencyID,
                    Amount = order.PaymentPaid,
                    Paid = now
                };
                d.Payments.AddObject(pay);
                //paymentInvoice
                var payInvoice = new PaymentInvoice
                {
                    PaymentID = pay.PaymentID,
                    InvoiceID = i.InvoiceID
                };
                pay.PaymentInvoice.Add(payInvoice);
                //Check invoice amt vs order.PaymentPaid!
                if (i.Total != pay.Amount)
                {
                    warnings.Add(string.Format("Discrepancy in payment, order: ({0}). Total:{1} & Paid:{2}", order.OrderID, i.Total, pay.Amount));
                }
                if (pay.Amount >= i.Total)
                {
                    payInvoice.IsFinalPaymentInvoice = true;
                }
                else
                {
                    //Remove downloads issued
                    var downloads = d.ObjectStateManager.GetObjectStateEntries(System.Data.EntityState.Added).Where(f => f.Entity.GetType() == typeof(Download)).Select(f => (Download)f.Entity);
                    foreach (var o in downloads)
                        d.Downloads.DeleteObject(o);
                    //Remove licenses issued
                    var licenseAssetss = d.ObjectStateManager.GetObjectStateEntries(System.Data.EntityState.Added).Where(f => f.Entity.GetType() == typeof(LicenseAsset)).Select(f => (LicenseAsset)f.Entity);
                    foreach (var o in licenseAssetss)
                        d.LicenseAssets.DeleteObject(o);
                    var licenses = d.ObjectStateManager.GetObjectStateEntries(System.Data.EntityState.Added).Where(f => f.Entity.GetType() == typeof(License)).Select(f => (License)f.Entity);
                    foreach (var o in licenses)
                        d.Licenses.DeleteObject(o);
                    //Remove assets
                    var assets = d.ObjectStateManager.GetObjectStateEntries(System.Data.EntityState.Added).Where(f => f.Entity.GetType() == typeof(Asset)).Select(f => (Asset)f.Entity);
                    foreach (var o in assets)
                        d.Assets.DeleteObject(o);
                    var assetMaintenances = d.ObjectStateManager.GetObjectStateEntries(System.Data.EntityState.Added).Where(f => f.Entity.GetType() == typeof(AssetMaintenance)).Select(f => (AssetMaintenance)f.Entity);
                    foreach (var o in assetMaintenances)
                        d.AssetMaintenances.DeleteObject(o);
                }

                if (warnings.Count > 0)
                    _users.WarnAdmins(warnings);

                d.SaveChanges();
            }
        }

        public void UpdateOrder(OrderViewModel order)
        {
            var application = _users.ApplicationID;
            var contact = _users.ContactID;
            var currentTime = DateTime.UtcNow;
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new XODBC(_users.ApplicationConnectionString, null, false);
                //PurchaseOrder
                PurchaseOrder po = (from o in d.PurchaseOrders where order.OrderID.HasValue && o.PurchaseOrderID == order.OrderID select o).SingleOrDefault();
                if (po == null)
                {
                    if (!order.OrderID.HasValue)
                        order.OrderID = Guid.NewGuid();
                    po = new PurchaseOrder { PurchaseOrderID = order.OrderID.Value, Ordered = currentTime, CustomerContactID = contact};
                    d.PurchaseOrders.AddObject(po);
                }
                //Supply
                Supply s = (from o in d.Supplies where o.CustomerPurchaseOrderID == po.PurchaseOrderID select o).SingleOrDefault();
                if (s == null)
                {
                    s = new Supply { SupplyID = Guid.NewGuid(), CustomerPurchaseOrderID = po.PurchaseOrderID, DateOrdered = currentTime};
                    d.Supplies.AddObject(s);
                }
                //SupplyItem
                var oldItems = (from o in d.SupplyItems where o.Supply.CustomerPurchaseOrderID == po.PurchaseOrderID select o).ToList();
                var currentConditions = new List<ContractConditionViewModel>();
                foreach (var p in order.Products)
                {
                    currentConditions.AddRange(p.ContractConditions);

                    //SupplierModel, Model, Part, Count
                    var item = (from o in oldItems 
                                where 
                                    o.SupplierModelID == p.SupplierModelID
                                    && o.SupplierPartID == p.SupplierPartID
                                    && o.ModelID == p.ModelID
                                    && o.PartID == p.PartID
                                select o).SingleOrDefault();
                    if (item != null)
                        oldItems.Remove(item);
                    else {
                        item = new SupplyItem { 
                            SupplyID = s.SupplyID, 
                            SupplyItemID = Guid.NewGuid(),
                            ModelID = p.ModelID,
                            PartID = p.PartID,
                            SupplierModelID = p.SupplierModelID,
                            SupplierPartID = p.SupplierPartID,
                            ApplicationPaymentProviderProductID = p.PaymentProviderProductID
                        };
                        d.SupplyItems.AddObject(item);
                    }
                    item.CurrencyID = p.CurrencyID;

                    if (p.PartID.HasValue || p.SupplierPartID.HasValue)
                    {
                        throw new NotImplementedException();
                        item.PartUnitID = p.ProductUnitID;
                        item.CostPerUnitPart = p.PricePerUnit;
                        item.QuantityPart = p.Units;
                        item.CostPart = item.CostPerUnitPart * item.QuantityPart;
                        item.TaxPart = item.CostPart * ConstantsHelper.TAX_DEFAULT;
                        item.SubtotalPart = item.CostPart + item.TaxPart;
                    }
                    else
                    {
                        item.ModelUnitID = p.ProductUnitID;
                        item.CostPerUnitModel = p.PricePerUnit;
                        item.QuantityModel = p.Units;
                        item.CostModel = item.CostPerUnitModel * item.QuantityModel;
                        item.TaxModel = item.CostModel * ConstantsHelper.TAX_DEFAULT;
                        item.SubtotalModel = item.CostModel + item.TaxModel;
                    }

                    item.LabourUnitID = null;
                    item.CostPerUnitLabour = 0;
                    item.QuantityLabour = 0;
                    item.CostLabour = 0;
                    item.TaxLabour = 0;
                    item.SubtotalLabour = 0;
                    
                    item.Tax = (item.TaxLabour ?? 0m) + (item.TaxModel ?? 0m) + (item.TaxPart ?? 0m); //TODO: If taxation gets complicated use itemtax table
                    item.OriginalSubtotal = (item.CostLabour ?? 0m) + (item.CostModel ?? 0m) + (item.CostPart ?? 0m);
                    item.Subtotal = (item.SubtotalLabour ?? 0m) + (item.SubtotalModel ?? 0m) + (item.SubtotalPart ?? 0m); ; //TODO: Discounting
                    
                }
                //Clear removed items
                foreach (var oldItem in oldItems)
                {
                    oldItem.CostLabour = 0;
                    oldItem.CostModel = 0;
                    oldItem.CostPart = 0;
                    oldItem.QuantityLabour = 0;
                    oldItem.QuantityModel = 0;
                    oldItem.QuantityPart = 0;
                    oldItem.Subtotal = 0;
                    oldItem.SubtotalLabour = 0;
                    oldItem.SubtotalModel = 0;
                    oldItem.SubtotalPart = 0;
                    oldItem.Tax = 0; //TODO: If taxation gets complicated use itemtax table
                    oldItem.TaxLabour = 0;
                    oldItem.TaxModel = 0;
                    oldItem.TaxPart = 0;
                    oldItem.OriginalSubtotal = 0;
                }
                
                //ContractConditions
                var oldConditions = (from o in d.SupplyContractConditions where o.SupplyID == s.SupplyID select o).ToList();
                currentConditions = currentConditions.Where(f=>f.ContractID.HasValue).GroupBy(f => string.Format("{0}{1}", f.ContractID, f.ContractConditionID), (key, list) => list.First()).ToList();
                foreach (var c in currentConditions)
                {
                    //SupplierModel, Model, Part, Count
                    var item = (from o in oldConditions
                                where
                                    o.ContractID == c.ContractID
                                    && o.ContractConditionID == c.ContractConditionID
                                select o).SingleOrDefault();
                    if (item != null)
                        oldConditions.Remove(item);
                    else
                    {
                        item = new SupplyContractCondition { SupplyContractConditionID = Guid.NewGuid(), SupplyID = s.SupplyID, ContractID = c.ContractID.Value, ContractConditionID = c.ContractConditionID };
                        d.SupplyContractConditions.AddObject(item);
                    }
                    foreach (var oldCondition in oldConditions)
                        d.SupplyContractConditions.DeleteObject(oldCondition);
                }

                d.SaveChanges();

            }
        }

        public IEnumerable<DownloadViewModel> GetDownloads(Guid orderID)
        {
            var contact = _users.ContactID;
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new XODBC(_users.ApplicationConnectionString, null);
                return (from o in d.E_SP_GetDownloads(orderID, contact)
                        select new DownloadViewModel
                        {
                            DownloadID = o.DownloadID,
                            Description = o.Description
                        }
                        ).ToArray();
            }
        }


        public IEnumerable<ProductViewModel> GetProducts(string text = null, Guid? supplierModelID = null, int? startRowIndex = null, int? pageSize=null)
        {
            var supplier = _users.ApplicationCompanyID;
            var application = _users.ApplicationID;
            var directory = _media.GetPublicUrl(@"EXPEDIT.Transactions");
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new XODBC(_users.ApplicationConnectionString, null);
                return (from o in d.E_SP_GetProductModels(text, application, supplier, ConstantsHelper.DEVICE_TYPE_SOFTWARE, supplierModelID, startRowIndex, pageSize) 
                        select new ProductViewModel { 
                            SupplierModelID = o.SupplierModelID,
                            ModelID = o.ModelID, 
                            CompanyID = o.CompanyID, 
                            MediaDirectory = directory, 
                            PricePerUnit = o.PricePerUnit, 
                            PriceUnitID = o.PriceUnitID,
                            CostUnit = o.CostUnit,
                            SupplierID = supplier,
                            Title = o.Title,
                            Subtitle = o.Subtitle,
                            HTML = o.HTML,
                            Manufacturer = o.Manufacturer,
                            CurrencyID = o.CurrencyID,
                            CurrencyPostfix = o.CurrencyPostfix,
                            CurrencyPrefix = o.CurrencyPrefix,
                            FreeDownloadID = o.FreeDownloadID,
                            Downloads = o.Downloads,
                            PaymentProviderID = o.ApplicationPaymentProviderID,
                            PaymentProviderProductID = o.ApplicationPaymentProviderProductID,
                            PaymentProviderProductName = o.PaymentProviderProductName,
                            ProductID = o.ProductID,
                            ProductUnitID = o.ProductUnitID,
                            ProductUnitName = o.UnitName,
                            ProductUnitNamePaymentProvider = o.PaymentProviderUnitName,
                            IsRecurring = o.IsRecurring,
                            KitUnitDefault = o.KitDefault,
                            KitUnitMaximum = o.KitMaximum,
                            KitUnitMinimum = o.KitMinimum,
                            UnitDefault = o.UnitDefault,
                            UnitMaximum = o.UnitMaximum,
                            UnitMinimum = o.UnitMinimum,
                            LastUpdated = DateTime.Now,
                            Rating = o.Rating,
                            RatingScale = o.RatingScale,
                            UrlExternal = o.ExternalURL,
                            UrlInternal = o.InternalURL
                        }).ToArray();
            }
        }


        public ProductViewModel GetProduct(Guid supplierModelID)
        {
            return GetProducts(null, supplierModelID).First();
        }

        public IEnumerable<ContractConditionViewModel> GetContractConditions(Guid[] referenceIDs)
        {
            var supplier = _users.ApplicationCompanyID;
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new XODBC(_users.ApplicationConnectionString, null);
                return (from o in d.E_SP_GetProductContractConditions(string.Join(",", referenceIDs), supplier, false, true, false)
                         select new ContractConditionViewModel
                         {
                             ContractID = o.ContractID,
                             ContractConditionID = o.ContractConditionID,
                             ContractText = o.ContractText,
                             IsForDistributors = false,
                             IsForEndUsers = true,
                             IsForSuppliers = false
                         }).ToArray();
            }
            
        }

        public void IncrementDownloadCounter(Guid productID)
        {
            
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new XODBC(_users.ApplicationConnectionString, null, false);
                string refTable = null;
                if (d.DictionaryModels.Any(f => f.ModelID == productID))
                    refTable = d.GetTableName<DictionaryModel>();
                else if (d.DictionaryParts.Any(f => f.PartID == productID))
                    refTable = d.GetTableName<DictionaryPart>();
                else
                    throw new InvalidOperationException(string.Format("Can't increment unknown product {0}.", productID));
                IncrementStatistic(d, productID, refTable, ConstantsHelper.STAT_NAME_DOWNLOADS);              
                d.SaveChanges();
            }

        }

        public void IncrementBuyCounter(Guid supplierModelID, Guid modelID)
        {
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new XODBC(_users.ApplicationConnectionString, null, false);               
                IncrementStatistic(d, supplierModelID, d.GetTableName<SupplierModel>(), ConstantsHelper.STAT_NAME_CLICKS_BUY);
                IncrementStatistic(d, modelID, d.GetTableName<DictionaryModel>(), ConstantsHelper.STAT_NAME_CLICKS_BUY);
                d.SaveChanges();
            }
        }        

        public void IncrementStatistic(XODBC d, Guid referenceID, string referenceTable, string statName)
        {
            var stat = (from o in d.StatisticDatas
                        where o.ReferenceID == referenceID && o.TableType == referenceTable
                        && o.StatisticDataName == statName
                        select o).FirstOrDefault();
            if (stat == null)
            {
                stat = new StatisticData { StatisticDataID = Guid.NewGuid(), TableType = referenceTable, ReferenceID = referenceID, StatisticDataName = statName, Count = 0 };
                d.StatisticDatas.AddObject(stat);
            }
            stat.Count++;
        }


        public PartnerViewModel GetPartnership(Guid? contractID = default(Guid?))
        {

            var contact = _users.GetContact(_users.Username);
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {                
                var d = new XODBC(_users.ApplicationConnectionString, null);
                Contract contract = null;
                if (contractID.HasValue)
                    contract = (from o in d.Contracts where o.ContractID == contractID.Value && o.VersionDeletedBy == null && o.Version == 0 select o).FirstOrDefault();
                if (contract == null)
                    contract = (from o in d.Contracts where o.Started == null && o.ObligeeID==contact.ContactID && o.ParentContractID == ConstantsHelper.CONTRACT_PARTNER && o.VersionDeletedBy == null && o.Version == 0 orderby o.VersionUpdated descending select o).FirstOrDefault();
                if (contract == null)
                {
                    var original = (from o in d.Contracts where o.ContractID == ConstantsHelper.CONTRACT_PARTNER && o.VersionDeletedBy == null && o.Version == 0 select o).Single(); //TODO fix static reference
                    contract = new Contract();
                    original.Mirror<Contract>(ref contract);                    
                    contract.ContractID = Guid.NewGuid();
                    contract.ParentContractID = original.ContractID;
                    d.Contracts.AddObject(contract);
                }
                if (contract.ObligeeID != contact.ContactID)
                    contract.ObligeeID = contact.ContactID;
                if (contract.ObligorCompanyID != ConstantsHelper.COMPANY_DEFAULT)
                    contract.ObligorCompanyID = ConstantsHelper.COMPANY_DEFAULT;
                if (contract.AssigneeID != contact.ContactID)
                    contract.AssigneeID = contact.ContactID;
                var twoStep = (from o in d.TwoStepAuthenticationDatas where o.TableType == ConstantsHelper.REFERENCE_TYPE_CONTRACT && o.ReferenceID == contract.ContractID && o.ContactID==contact.ContactID && o.VersionDeletedBy == null && o.Version == 0 orderby o.Sequence descending select o).FirstOrDefault();
                if (twoStep == null)
                {
                    twoStep = new TwoStepAuthenticationData();
                    twoStep.TwoStepAuthenticationDataID = Guid.NewGuid();
                    twoStep.TableType = ConstantsHelper.REFERENCE_TYPE_CONTRACT;
                    twoStep.ReferenceID = contract.ContractID;
                    twoStep.ContactID = contact.ContactID;
                    twoStep.Mobile = contact.DefaultMobile;
                    twoStep.ReferenceName = contact.DefaultMobile;
                    twoStep.VerificationCode = Guid.NewGuid().ToString().Substring(0, 4);
                    d.TwoStepAuthenticationDatas.AddObject(twoStep);
                }
                d.SaveChanges();
                var m = new PartnerViewModel { 
                    ContactID = contact.ContactID,  
                    TwoStepID = twoStep.TwoStepAuthenticationDataID,
                    Lastname = contact.Surname,
                    Firstname = contact.Firstname,
                    ContractID = contract.ContractID,
                    ContractText = contract.ContractText,
                    Mobile = contact.DefaultMobile,                     
                };
                return m;
            }
        }

        public bool UpdatePartnership(PartnerViewModel m, string IPAddress)
        {
            var contact = _users.GetContact(_users.Username);
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new XODBC(_users.ApplicationConnectionString, null);
                var twoStep = (from o in d.TwoStepAuthenticationDatas where o.TwoStepAuthenticationDataID == m.VerificationID && o.VersionDeletedBy == null && o.Version == 0 select o).Single();
                if (m.VerificationCode != twoStep.VerificationCode || string.IsNullOrWhiteSpace(twoStep.VerificationCode))
                    return false;
                twoStep.Verified = DateTime.UtcNow;
                twoStep.RequestedByIP = IPAddress;
                var contract = (from o in d.Contracts where o.ContractID == m.ContractID && o.VersionDeletedBy == null && o.Version == 0 select o).Single();
                contract.Started = DateTime.UtcNow;
                d.SaveChanges();
                return true;
            }
        }


        public bool SendTwoStepAuthentication(ref IVerifyMobile verify)
        {
            //First check mobile
            if (string.IsNullOrWhiteSpace(verify.Mobile))
                return false;
            verify.Mobile = verify.Mobile.Replace(" ","");
            if (!verify.Mobile.IsMobile())
                return false;
                    
             using (new TransactionScope(TransactionScopeOption.Suppress))
             {
                 var twoStepID = verify.VerificationID;
                 var d = new XODBC(_users.ApplicationConnectionString, null);
                 var twoStep = (from o in d.TwoStepAuthenticationDatas where o.TwoStepAuthenticationDataID == twoStepID && o.VersionDeletedBy == null && o.Version == 0 select o).Single();
                 var client = new RestClient("https://api.smsglobal.com");
                 //System.Net.ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;
                 // client.Authenticator = new HttpBasicAuthenticator(username, password);
                 var request = new RestRequest("/v1/sms/", Method.POST);
                 var id = "80d249a9eecc9232fb6ed0f843e7f230";
                 var secret = "6239e342d7abb35c853ad33e65931f64";
                 var timestamp = string.Format("{0}", DateHelper.NowToUnixTimestamp());
                 var nonce = Guid.NewGuid().ToString().Replace("-", "").Substring(0, 32);
                 string hash = null;
                 using (HMACSHA256 hmac = new HMACSHA256(Convert.FromBase64String(secret)))
                     hash = Convert.ToBase64String(hmac.ComputeHash(System.Text.Encoding.ASCII.GetBytes(string.Format("{0}\n{1}\n{2}\n{3}\n{4}\n{5}\n\n", timestamp, nonce, "POST", "/v1/sms/", "api.smsglobal.com", "443", null))));
                 client.Authenticator = new OAuth2AuthorizationRequestHeaderAuthenticator(string.Format("id=\"{0}\", ts=\"{1}\", nonce=\"{2}\", mac=\"{3}\"", id, timestamp, nonce, hash), "MAC");
                 client.PreAuthenticate = true;
                 request.AddParameter("origin", "61400970789");
                 var destination = verify.Mobile.Replace("+","");
                 request.AddParameter("destination", destination);
                 request.AddParameter("message", string.Format("Your MiningAppStore code is {0}", twoStep.VerificationCode));
                 //IRestResponse<Person> response2 = client.Execute<Person>(request);
                 IRestResponse response = client.Execute(request);
                 if (response.StatusCode != System.Net.HttpStatusCode.Created)
                     return false;
                 twoStep.Sent = DateTime.UtcNow;
                 twoStep.Mobile = destination;
                 twoStep.Contact.DefaultMobile = verify.Mobile;
                 d.SaveChanges();

             }
             return false;

        }
       
    }
}
