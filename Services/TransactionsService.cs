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
using Orchard.Roles.Services;
using Orchard;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Transactions;
using Orchard.Messaging.Services;
using Orchard.Logging;
using Orchard.Tasks.Scheduling;
using Orchard.Data;
using NKD.Module.BusinessObjects;
using EXPEDIT.Transactions.ViewModels;
using NKD.Services;
using Orchard.MediaLibrary.Services;
using EXPEDIT.Transactions.Helpers;
using EXPEDIT.Transactions.Services.Payments;
using EntityFramework.Extensions;
using Newtonsoft.Json;
using EXPEDIT.Share.Helpers;
using NKD.Helpers;
using NKD.Models;
using RestSharp;
using System.Security.Cryptography;
using System.Web.Hosting;
using Orchard.Environment.Configuration;
using lh = CNX.Shared.Helpers;
using Orchard.Users.Models;
using Orchard.Roles.Models;
using System.Collections.Specialized;


namespace EXPEDIT.Transactions.Services {
    
    [UsedImplicitly]
    public class TransactionsService : ITransactionsService {

        private const string DIRECTORY_TEMP = "EXPEDIT.Transactions\\Temp";
        private readonly IOrchardServices _orchardServices;
        private readonly IUsersService _users;
        private readonly IMediaLibraryService _media;
        private readonly IPayment _payment;
        private readonly IStorageProvider _storage;
        private readonly IRoleService _roles;
        private ShellSettings _settings;
        private readonly IRepository<UserPartRecord> _userRepository;
        private readonly IRepository<UserRolesPartRecord> _userRolesRepository;

        public ILogger Logger { get; set; }

        public TransactionsService(
            IOrchardServices orchardServices, 
            IUsersService users,
            IMediaLibraryService media,
            IPayment payment,
            IStorageProvider storage,
            ShellSettings shellSettings,
            IRoleService roles,
            IRepository<UserPartRecord> userRepository,
            IRepository<UserRolesPartRecord> userRolesRepository)
        {
            _orchardServices = orchardServices;
            _media = media;
            _users = users;
            T = NullLocalizer.Instance;
            Logger = NullLogger.Instance;
            _payment = payment;
            _storage = storage;
            _settings = shellSettings;
            _roles = roles;
            _userRepository = userRepository;
            _userRolesRepository = userRolesRepository;
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
            var contact = _users.ContactID;
            var orderID = order.OrderID;
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new NKDC(_users.ApplicationConnectionString, null);
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
                var d = new NKDC(_users.ApplicationConnectionString, null);
                d.MetaDatas.Delete(f =>
                            f.MetaDataID == antiForgeryKey.Value
                            && f.MetaDataType == ConstantsHelper.METADATA_ANTIFORGERY
                          && f.ContentToIndex == orderID
                          && f.Version == 0
                          && f.VersionDeletedBy == null);
                d.SaveChanges();
            }
            _payment.PreparePaymentResult(ref order);
            if (order.PaymentError == (uint)PaymentUtils.PaymentError.BadCustomerID)
            {
                //Force Update Contact on Server Side
                using (new TransactionScope(TransactionScopeOption.Suppress))
                {
                    var d = new NKDC(_users.ApplicationConnectionString, null);
                    d.ApplicationPaymentProviderContacts.Where(f => f.ContactID == contactID).Delete();
                }
            }

        }

        public Guid? GetOrderCurrentID()
        {
            Guid id = default(Guid);
            var cookie = System.Web.HttpContext.Current.Request.Cookies["OrderID"];
            if (cookie != null)
            {
                Guid.TryParse(cookie.Value, out id);
            }
            if (id == default(Guid))
            {
                //Guid id = System.Web.HttpContext.Current.Request;
                var ii = System.Web.HttpContext.Current.Request.Url.Query.IndexOf("id=");
                if (ii > -1)
                {
                    ii += 3;
                    var ids = string.Join("", System.Web.HttpContext.Current.Request.Url.Query.Skip(ii).Take(36));
                    Guid.TryParse(ids, out id);
                }
                else
                {
                    ii = System.Web.HttpContext.Current.Request.Url.AbsoluteUri.Length - 36;
                    if (ii > -1)
                    {
                        var ids = string.Join("", System.Web.HttpContext.Current.Request.Url.AbsoluteUri.Skip(ii).Take(36));
                        Guid.TryParse(ids, out id);
                    }
                }
                using (new TransactionScope(TransactionScopeOption.Suppress))
                {
                    var d = new NKDC(_users.ApplicationConnectionString, null);
                    if ((from o in d.Supplies where o.Version == 0 && o.VersionDeletedBy == null && o.CustomerPurchaseOrderID == id select o.CustomerPurchaseOrderID).Any())
                        return id;                    
                }
            } 
            else
            {
                return id;
            }
            return null;
        }

        public OrderViewModel GetOrderCurrent()
        {
            //Try Get Cookie or Last Order
            OrderViewModel order = null;
            Guid? orderID = GetOrderCurrentID();
            if (orderID.HasValue)
                order = GetOrder(orderID.Value, true);
            else
                order = GetOrderLast(true);
            return order;
        }

        public OrderViewModel GetOrderLast(bool detailed=false)
        {
            var contact = _users.ContactID;
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new NKDC(_users.ApplicationConnectionString, null);
                var id = (from o in d.Supplies where o.Version == 0 && o.VersionDeletedBy == null && o.IsPaid != true orderby o.DateOrdered descending select o.CustomerPurchaseOrderID).FirstOrDefault();
                if (id.HasValue)
                    return GetOrder(id.Value, detailed);
                else
                    return null;
            }
        }

        public OrderViewModel GetOrder(Guid orderID, bool detailed=false)
        {
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new NKDC(_users.ApplicationConnectionString, null);
                IQueryable<OrderProductViewModel> items = null;
                if (!detailed)
                    items = (from o in d.SupplyItems where o.Supply.CustomerPurchaseOrderID==orderID && o.Version==0 && o.VersionDeletedBy == null 
                             select new OrderProductViewModel { 
                                 SupplierModelID = o.SupplierModelID, 
                                 SupplierPartID = o.SupplierPartID,
                                 ModelID=o.ModelID, 
                                 PartID=o.PartID, 
                                 PaymentProviderProductID = o.ApplicationPaymentProviderProductID,
                                 PaymentProviderProductName= (o.ApplicationPaymentProviderProduct==null) ? null : o.ApplicationPaymentProviderProduct.PaymentProviderProductName,
                                 Subtotal = o.Subtotal,
                                 ProductUnitID = o.ModelUnitID,
                                 ProductUnitName = o.UnitModel.StandardUnitName,
                                 IsRecurring = o.UnitModel.EquivalentUnitID != ConstantsHelper.UNIT_SI_UNARY && o.ModelUnitID != ConstantsHelper.UNIT_SI_UNARY
                             });
                else
                    items = (from o in d.SupplyItemDetailViews
                             where o.CustomerPurchaseOrderID == orderID && o.Version == 0 && o.VersionDeletedBy == null
                             select new OrderProductViewModel
                             {
                                 SupplierModelID = o.SupplierModelID,
                                 SupplierPartID = o.SupplierPartID,
                                 ModelID = o.ModelID,
                                 ProductUnitName = o.ModelUnitName,
                                 PartID = o.PartID,
                                 PaymentProviderProductID = o.ApplicationPaymentProviderProductID,
                                 PartUnits = o.QuantityPart,
                                 ModelUnits = o.QuantityModel,
                                 LabourUnits = o.QuantityLabour,
                                 Subtotal = o.Subtotal,
                                 ProductUnitID = o.ModelUnitID,
                                 ModelName = o.StandardModelName,
                                 CurrencyPostfix = o.PostfixCharacters,
                                 CurrencyPrefix = o.PrefixCharacters,
                                 Tax = o.Tax,
                                 IsRecurring = o.ModelUnitName != null && o.ModelUnitName != "u" && o.ModelUnitID != ConstantsHelper.UNIT_SI_UNARY
                             });
                var m = new OrderViewModel { OrderID = orderID, Products = items.ToList() };                
                return m;
            }
        }

        public bool GetOrderPaid(Guid orderID)
        {
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new NKDC(_users.ApplicationConnectionString, null);
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
                var d = new NKDC(_users.ApplicationConnectionString, null);
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
                var d = new NKDC(_users.ApplicationConnectionString, null);
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
                var d = new NKDC(_users.ApplicationConnectionString, null);
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
                              select p.ApplicationPaymentProviderID).FirstOrDefault();
                    if (pp == default(Guid))
                        pp = ConstantsHelper.PAYMENT_PROVIDER_DEFAULT;
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
                    m.AddressName = order.PaymentCompany;
                    m.Street = order.PaymentStreet;
                    m.Extended = order.PaymentStreetExtended;
                    m.Phone = order.PaymentPhone;
                    m.Country = order.PaymentCountry;
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
            var isAdmin = _orchardServices.Authorizer.Authorize(Orchard.Security.StandardPermissions.SiteOwner);
            var application = _users.ApplicationID;
            var customerID = order.PaymentCustomerID;
            var contact = _users.ContactID;
            var warnings = new List<string>();
            var now = DateTime.UtcNow;
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                //Update customer id
                var d = new NKDC(_users.ApplicationConnectionString, null);
                //var transaction = d.BeginTransaction(System.Data.IsolationLevel.ReadCommitted); //TODO implement transactions
                PurchaseOrder po = (from o in d.PurchaseOrders where order.OrderID.HasValue && o.PurchaseOrderID == order.OrderID select o).Single();
                Supply s = (from o in d.Supplies where o.CustomerPurchaseOrderID == po.PurchaseOrderID select o).Single();
                var oldItems = (from o in d.SupplyItems.Include("UnitModel") where o.Supply.CustomerPurchaseOrderID == po.PurchaseOrderID select o).ToList();
                Invoice i = new Invoice
                { 
                    InvoiceID = lh.GuidHelper.NewComb() ,
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
                                var qtyLicenses = 1;
                                if (supplyItem.QuantityModel < 500)
                                    qtyLicenses = Convert.ToInt32(supplyItem.QuantityModel.Value);
                                for (int j = 0; j < qtyLicenses; j++)
                                {
                                    //Asset TODO:Check to update old asset
                                    var assetID = Guid.NewGuid();
                                    var asset = new Asset
                                    {
                                        AssetID = assetID,
                                        AssetName = string.Format("{0} [{1}]", string.Join(null, mn.Take(60 - 42)), assetID),
                                        InitialCost = lineItem.Subtotal,
                                        ProRataCost = supplyItem.CostPerUnitModel + supplyItem.TaxModel,
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
                                            nextMaintenance = now.AddSeconds(1.0); //supplyItem.QuantityModel.Value
                                        else if (mu.EquivalentUnitID == ConstantsHelper.UNIT_SI_SECONDS)
                                            nextMaintenance = now.AddSeconds(Convert.ToDouble(1m * mu.EquivalentMultiplier));
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
                                        LicenseeUsername = _users.Username,
                                        ContactID = contact,
                                        ValidForUnitID = supplyItem.ModelUnitID,
                                        ValidForDuration = 1, //supplyItem.QuantityModel,
                                        ValidFrom = now,
                                        Expiry = nextMaintenance,
                                        SupportExpiry = nextMaintenance,
                                        ApplicationID = application,
                                        ServiceAuthorisationMethod = ConstantsHelper.LICENSE_SERVER_AUTH_METHOD,
                                        VersionUpdated = now
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


                                    var m = (from o in d.DictionaryModels where o.ModelID == supplyItem.ModelID select o).FirstOrDefault();
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
                    Paid = now,
                    VersionUpdated = now
                };
                d.Payments.AddObject(pay);
                //paymentInvoice
                var payInvoice = new PaymentInvoice
                {
                    PaymentID = pay.PaymentID,
                    InvoiceID = i.InvoiceID,
                    VersionUpdated = now
                };
                pay.PaymentInvoice.Add(payInvoice);
                //Check invoice amt vs order.PaymentPaid!
                if (i.Total != pay.Amount)
                {
                    warnings.Add(string.Format("Discrepancy in payment, order: ({0}). Total:{1} & Paid:{2}", order.OrderID, i.Total, pay.Amount));
                }
                if (pay.Amount >= i.Total || isAdmin)
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
                var d = new NKDC(_users.ApplicationConnectionString, null, false);
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
                foreach (var p in order.Products)
                {
                    //SupplierModel, Model, Part, Count
                    var item = (from o in oldItems 
                                where 
                                    o.SupplierModelID == p.SupplierModelID
                                    && o.SupplierPartID == p.SupplierPartID
                                    && o.ModelID == p.ModelID
                                    && o.PartID == p.PartID
                                select o).SingleOrDefault();
                    if (!p.ModelUnits.HasValue || p.ModelUnits.Value <= 0) //Remove the zeroed item
                        continue;
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
                    if (!item.CurrencyID.HasValue)
                        item.CurrencyID = p.CurrencyID;

                    if (p.PartID.HasValue || p.SupplierPartID.HasValue)
                    {
                        throw new NotImplementedException(); //Todo, add parts and labour
                        item.PartUnitID = p.ProductUnitID;
                        item.CostPerUnitPart = p.PricePerPartUnit;
                        item.QuantityPart = p.PartUnits;
                        item.CostPart = item.CostPerUnitPart * item.QuantityPart;
                        item.TaxPart = item.CostPart * ConstantsHelper.TAX_DEFAULT;
                        item.SubtotalPart = item.CostPart + item.TaxPart;
                    }
                    else
                    {    
                        item.QuantityModel = p.ModelUnits;
                        if (!item.ModelUnitID.HasValue)
                            item.ModelUnitID = p.ProductUnitID;
                        if (!item.CostPerUnitModel.HasValue)
                            item.CostPerUnitModel = p.PricePerModelUnit;
                        item.CostModel = item.CostPerUnitModel * item.QuantityModel;
                        item.TaxModel = item.CostModel * ConstantsHelper.TAX_DEFAULT;
                        item.SubtotalModel = item.CostModel + item.TaxModel;
                    }

                    //Not Implemented
                    item.LabourUnitID = null;
                    item.CostPerUnitLabour = null;
                    item.QuantityLabour = null;
                    item.CostLabour = null;
                    item.TaxLabour = null;
                    item.SubtotalLabour = null;
                    
                    item.Tax = (item.TaxLabour ?? 0m) + (item.TaxModel ?? 0m) + (item.TaxPart ?? 0m); //TODO: If taxation gets complicated use itemtax table
                    item.OriginalSubtotal = (item.CostLabour ?? 0m) + (item.CostModel ?? 0m) + (item.CostPart ?? 0m);
                    item.Subtotal = (item.SubtotalLabour ?? 0m) + (item.SubtotalModel ?? 0m) + (item.SubtotalPart ?? 0m); ; //TODO: Discounting
                    
                }
                //Clear removed items
                foreach (var oldItem in oldItems)
                {
                    d.SupplyItems.DeleteObject(oldItem);
                    //oldItem.CostLabour = null;
                    //oldItem.CostModel = null;
                    //oldItem.CostPart = null;
                    //oldItem.QuantityLabour = null;
                    //oldItem.QuantityModel = null;
                    //oldItem.QuantityPart = null;
                    //oldItem.Subtotal = null;
                    //oldItem.SubtotalLabour = null;
                    //oldItem.SubtotalModel = null;
                    //oldItem.SubtotalPart = null;
                    //oldItem.Tax = null; //TODO: If taxation gets complicated use itemtax table
                    //oldItem.TaxLabour = null;
                    //oldItem.TaxModel = null;
                    //oldItem.TaxPart = null;
                    //oldItem.OriginalSubtotal = null;
                }
                

                //d.SupplyContractConditions.Delete(f=>f.Agreed == null && f.SupplyID==s.SupplyID);
                


                var requiredConditions = GetContractConditions(order.Products.Where(f=>f.ModelID.HasValue).Select(f=>f.ModelID.Value).Union(order.Products.Where(f=>f.SupplierModelID.HasValue).Select(f=>f.SupplierModelID.Value)).ToArray())
                    .GroupBy(f=>string.Format("{0}{1}", f.ContractID, f.ContractConditionID));                
                var currentConditions = (from o in d.SupplyContractConditions where o.Version==0 && o.VersionDeletedBy == null && o.SupplyID == s.SupplyID select o)
                    .AsEnumerable()
                    .GroupBy(f => string.Format("{0}{1}", f.ContractID, f.ContractConditionID));
                foreach (var delete in currentConditions.Where(f=> !requiredConditions.Any(g=>g.Key == f.Key)).Select(f=>f.AsEnumerable()))
                    foreach (var dc in delete)
                        d.SupplyContractConditions.DeleteObject(dc);
                foreach (var create in requiredConditions.Where(f=> !currentConditions.Any(g=>g.Key == f.Key)).Select(f=>f.AsEnumerable()))
                    foreach (var cc in create)
                        d.SupplyContractConditions.AddObject(
                            new SupplyContractCondition { 
                                SupplyContractConditionID = Guid.NewGuid(), 
                                SupplyID = s.SupplyID, 
                                ContractID = cc.ContractID.Value, 
                                ContractConditionID = cc.ContractConditionID 
                            });

                d.SaveChanges();

            }
        }

        public IEnumerable<DownloadViewModel> GetDownloads(Guid orderID)
        {
            var contact = _users.ContactID;
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new NKDC(_users.ApplicationConnectionString, null);
                return (from o in d.E_SP_GetDownloads(orderID, contact)
                        select new DownloadViewModel
                        {
                            DownloadID = o.DownloadID,
                            Description = o.Description
                        }
                        ).ToArray();
            }
        }


        public IEnumerable<ProductViewModel> GetProducts(string text = null, Guid? supplierModelID = null, int? startRowIndex = null, int? pageSize=null, string filterCategories = null)
        {
            //var ownerCompanyID = _users.ApplicationCompanyID;
            //var supplier = "EPSOFT" etc
            var application = _users.ApplicationID;
            var directory = _storage.GetPublicUrl(@"EXPEDIT.Transactions");
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new NKDC(_users.ApplicationConnectionString, null);
                return (from o in d.E_SP_GetProductModels(text, application, null, null, ConstantsHelper.DEVICE_TYPE_SOFTWARE, filterCategories, supplierModelID, startRowIndex, pageSize) 
                        select new ProductViewModel { 
                            SupplierModelID = o.SupplierModelID,
                            ModelID = o.ModelID, 
                            CompanyID = o.CompanyID, 
                            MediaDirectory = directory, 
                            PricePerModelUnit = o.PricePerUnit, //Todo Add Parts & Labour
                            PriceModelUnitID = o.PriceUnitID,
                            CostModelUnit = o.CostUnit,
                            SupplierID = o.SupplierID,
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

        public IEnumerable<ContractConditionViewModel> GetContractConditions(Guid[] referenceIDs, bool includeText=true)
        {
            var supplier = _users.ApplicationCompanyID;
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new NKDC(_users.ApplicationConnectionString, null);
                return (from o in d.E_SP_GetProductContractConditions(string.Join(",", referenceIDs), supplier, false, true, false, includeText)
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

        public void ConfirmContractConditions(Guid orderID)
        {
            var contact = _users.ContactID;
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new NKDC(_users.ApplicationConnectionString, null);
                var contracts = (from o in d.SupplyContractConditions
                                 where o.Supply.CustomerPurchaseOrderID == orderID && o.Version == 0 && o.VersionDeletedBy == null
                                 select o);
                foreach (var c in contracts)
                {
                    c.AgreedByContactID = contact;
                    c.Agreed = DateTime.UtcNow;
                }
                d.SaveChanges();
            }
        }

        public bool CheckContractConditions(Guid orderID)
        {
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new NKDC(_users.ApplicationConnectionString, null);
                return !d.SupplyContractConditions.Any(f=> f.Supply.CustomerPurchaseOrderID == orderID && f.Agreed == null);
            }

        }

        public void IncrementDownloadCounter(Guid productID)
        {
            
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new NKDC(_users.ApplicationConnectionString, null, false);
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
                var d = new NKDC(_users.ApplicationConnectionString, null, false);               
                IncrementStatistic(d, supplierModelID, d.GetTableName<SupplierModel>(), ConstantsHelper.STAT_NAME_CLICKS_BUY);
                IncrementStatistic(d, modelID, d.GetTableName<DictionaryModel>(), ConstantsHelper.STAT_NAME_CLICKS_BUY);
                d.SaveChanges();
            }
        }        

        public void IncrementStatistic(NKDC d, Guid referenceID, string referenceTable, string statName)
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


        public PartnerViewModel GetPartnership(ref PartnerViewModel m)
        {
            var now = DateTime.UtcNow;       
            if (m == null)
                m = new PartnerViewModel { };
            var cid = m.ContractID;
            var contact = _users.GetContact(_users.Username);
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {                
                var d = new NKDC(_users.ApplicationConnectionString, null);
                Contract contract = null;
                if (m.ContractID.HasValue)
                    contract = (from o in d.Contracts where o.ContractID == cid.Value && o.VersionDeletedBy == null && o.Version == 0 select o).FirstOrDefault();
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
                if (m.IsContractValid)
                {
                    var twoStep = (from o in d.TwoStepAuthenticationDatas where o.TableType == ConstantsHelper.REFERENCE_TYPE_CONTRACT && o.ReferenceID == contract.ContractID && o.ContactID == contact.ContactID && o.VersionDeletedBy == null && o.Version == 0 orderby o.Sequence descending select o).FirstOrDefault();
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
                    contract.ContractText = contract.ParentContract.ContractText.Replace("{Email}", contact.DefaultEmail.Replace("{", "{{").Replace("}", "}}")).Replace("{Mobile}", contact.DefaultMobile.Replace("{", "{{").Replace("}", "}}")).Replace("{Firstname}", contact.Firstname.Replace("{", "{{").Replace("}", "}}")).Replace("{Surname}", m.Lastname.Replace("{", "{{").Replace("}", "}}")).Replace("{Company}", m.Company.Replace("{", "{{").Replace("}", "}}")).Replace("{Date}", now.ToLongDateString() + " " + now.ToLongTimeString() + " UTC");
                    m.TwoStepID = twoStep.TwoStepAuthenticationDataID;
                }
                m.ContactID = contact.ContactID;                
                m.Lastname = contact.Surname;
                m.Firstname = contact.Firstname;
                m.ContractID = contract.ContractID;
                m.Mobile = contact.DefaultMobile;
                contract.Comment = JsonConvert.SerializeObject(m);        
                d.SaveChanges();
                m.ContractText = contract.ContractText;
                return m;
            }
        }

        public bool UpdatePartnership(PartnerViewModel m, string IPAddress)
        {
            if (m == null)
                return false;
            if (m.VerificationID == default(Guid))
                return false;
            var contact = _users.GetContact(_users.Username);
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new NKDC(_users.ApplicationConnectionString, null);
                var twoStep = (from o in d.TwoStepAuthenticationDatas where o.TwoStepAuthenticationDataID == m.VerificationID && o.VersionDeletedBy == null && o.Version == 0 select o).Single();
                if (string.IsNullOrWhiteSpace(twoStep.Contact.Firstname))
                    twoStep.Contact.Firstname = m.Firstname;
                if (string.IsNullOrWhiteSpace(twoStep.Contact.Surname))
                    twoStep.Contact.Surname = m.Lastname;
                if (twoStep.Contact.Firstname != m.Firstname || twoStep.Contact.Surname != m.Lastname)
                    return false;
                if (m.VerificationCode != twoStep.VerificationCode || string.IsNullOrWhiteSpace(twoStep.VerificationCode))
                    return false;
                twoStep.Verified = DateTime.UtcNow;
                twoStep.RequestedByIP = IPAddress;
                var contract = (from o in d.Contracts where o.ContractID == m.ContractID && o.VersionDeletedBy == null && o.Version == 0 select o).Single();
                contract.Started = DateTime.UtcNow;
                d.SaveChanges();                
            }
            
            var userPartRecord = _userRepository.Get(record => record.UserName == _users.Username);
            var roleRecord = _roles.GetRoleByName("Partner");
            if (roleRecord == null)
            {
                _roles.CreateRole("Partner");
                _roles.CreatePermissionForRole("Partner", "Partner");
                _roles.CreatePermissionForRole("Partner", "PartnerSoftware");
                roleRecord = _roles.GetRoleByName("Partner");
            }
            if (userPartRecord == null)
                return false;
            var existingAssociation = _userRolesRepository.Get(record => record.UserId == userPartRecord.Id && record.Role.Id == roleRecord.Id);
            if (existingAssociation == null)
                _userRolesRepository.Create(new UserRolesPartRecord { Role = roleRecord, UserId = userPartRecord.Id });
            return true;
        }


        public bool SendTwoStepAuthentication(ref VerifyMobileModel verify)
        {
            //First check mobile
            if (string.IsNullOrWhiteSpace(verify.Mobile))
                return false;
            verify.Mobile = verify.Mobile.Replace(" ","").Replace("-","");
            if (!verify.Mobile.IsMobile())
                return false;
            if (verify.VerificationID == default(Guid))
                return false;
                    
             using (new TransactionScope(TransactionScopeOption.Suppress))
             {
                 var twoStepID = verify.VerificationID;
                 var d = new NKDC(_users.ApplicationConnectionString, null);
                 var twoStep = (from o in d.TwoStepAuthenticationDatas where o.TwoStepAuthenticationDataID == twoStepID && o.VersionDeletedBy == null && o.Version == 0 select o).Single();
                 //Don't repeat within a minute
                 if (twoStep.Sent.HasValue && twoStep.Sent.Value.AddMinutes(2) > DateTime.UtcNow)
                     return true;
                 var client = new RestClient("https://api.smsglobal.com");
                 //System.Net.ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;
                 // client.Authenticator = new HttpBasicAuthenticator(username, password);
                 var request = new RestRequest("/v1/sms/", Method.POST);
                 var timestamp = string.Format("{0:0}", DateHelper.Timestamp);
                 var nonce = Guid.NewGuid().ToString().Replace("-", "").Substring(0, 32);
                 string hash = null;
                 var raw = System.Text.Encoding.ASCII.GetBytes(string.Format("{0}\n{1}\n{2}\n{3}\n{4}\n{5}\n\n", timestamp, nonce, "POST", "/v1/sms/", "api.smsglobal.com", "443", null));
                 using (HMACSHA256 hmac = new HMACSHA256(System.Text.Encoding.ASCII.GetBytes(ConstantsHelper.APP_VERIFY_SECRET)))
                     hash = Convert.ToBase64String(hmac.ComputeHash(raw, 0, raw.Length));
                 var mac = string.Format("MAC id=\"{0}\", ts=\"{1}\", nonce=\"{2}\", mac=\"{3}\"", ConstantsHelper.APP_VERIFY_ID, timestamp, nonce, hash);
                 //client.Authenticator = new OAuth2AuthorizationRequestHeaderAuthenticator();
                 //client.PreAuthenticate = true;
                 request.AddHeader("Authorization", mac);
                 request.AddParameter("origin", ConstantsHelper.APP_VERIFY_REPLYTO);
                 //var destination = verify.Mobile.Replace("+","");
                 request.AddParameter("destination", verify.Mobile); // destination);
                 request.AddParameter("message", string.Format("{0} {1}", ConstantsHelper.APP_VERIFY_PREFIX, twoStep.VerificationCode));
                 //IRestResponse<Person> response2 = client.Execute<Person>(request);
                 IRestResponse response = client.Execute(request);
                 if (response.StatusCode != System.Net.HttpStatusCode.Created)
                     return false;
                 twoStep.Sent = DateTime.UtcNow;
                 twoStep.Mobile = verify.Mobile;
                 twoStep.Contact.DefaultMobile = verify.Mobile;
                 d.SaveChanges();
                 return true;
             }

        }

        public bool ReceiveTwoStepAuthentication(string id)
        {
              return true;
        }

        public bool SubmitSoftware(SoftwareSubmissionViewModel m)
        {
            if (m.SoftwareSubmissionID == default(Guid))
                return false;
            var contact = _users.ContactID;
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new NKDC(_users.ApplicationConnectionString, null);
                //First check if any data is not owned by me
                if ((from o in d.MetaDatas where o.MetaDataID==m.SoftwareSubmissionID && o.VersionOwnerContactID!=contact.Value select o).Any())
                    return false;
                if ((from o in d.FileDatas where o.ReferenceID == m.SoftwareSubmissionID && o.VersionOwnerContactID != contact.Value select o).Any())
                    return false;
                var s = (from o in d.MetaDatas where o.MetaDataID==m.SoftwareSubmissionID && o.Version==0 && o.VersionDeletedBy==null select o).SingleOrDefault();
                if (s == null)
                {
                    s = new MetaData
                    {
                        MetaDataID = m.SoftwareSubmissionID,
                        MetaDataType = ConstantsHelper.DOCUMENT_TYPE_SOFTWARE_SUBMISSION,
                        VersionOwnerContactID = contact,
                        VersionUpdated = DateTime.UtcNow
                    };
                    d.MetaDatas.AddObject(s);
                }
                if (s.VersionOwnerContactID != contact)
                    return false;
                if (!string.IsNullOrWhiteSpace(m.Description) || m.ForDevelopment || m.ForManagement || m.ForSale)
                {
                    var content = JsonConvert.SerializeObject(m);
                    if (content != s.ContentToIndex)
                        s.ContentToIndex = content;
                }
                d.SaveChanges();
                var table = d.GetTableName<MetaData>();
                if (m.Files != null)
                {
                    var mediaPath = HostingEnvironment.IsHosted ? HostingEnvironment.MapPath("~/Media/") ?? "" : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Media");
                    var storagePath = Path.Combine(mediaPath, _settings.Name);
                    foreach (var f in m.Files)
                    {
                        var filename = string.Concat(f.Value.FileName.Reverse().Take(50).Reverse());

                        var file = new FileData
                        {
                            FileDataID = f.Key,
                            TableType = table,
                            ReferenceID = m.SoftwareSubmissionID,
                            FileTypeID = null, //TODO give type
                            FileName = filename,
                            FileLength = f.Value.ContentLength,
                            MimeType = f.Value.ContentType,
                            VersionOwnerContactID = contact,
                            DocumentType = ConstantsHelper.DOCUMENT_TYPE_SOFTWARE_SUBMISSION
                        };
                        m.FileLengths.Add(f.Key, f.Value.ContentLength);
                        _media.GetMediaFolders(DIRECTORY_TEMP);
                        var path = string.Format("{0}\\{1}-{2}-{3}", DIRECTORY_TEMP, m.SoftwareSubmissionID.ToString().Replace("-", ""), f.Key.ToString().Replace("-", "").Substring(15), filename.ToString().Replace("-", ""));
                        var sf = _storage.CreateFile(path);                        
                        using (var sw = sf.OpenWrite())
                            f.Value.InputStream.CopyTo(sw);
                        f.Value.InputStream.Close();
                        try
                        {

                            using (var dh = new DocHelper.FilterReader(Path.Combine(storagePath, path)))
                                file.FileContent = dh.ReadToEnd();
                        }
                        catch { }
                        using (var sr = sf.OpenRead())
                            file.FileBytes = sr.ToByteArray();
                        _storage.DeleteFile(path);
                        file.FileChecksum = file.FileBytes.ComputeHash();
                        d.FileDatas.AddObject(file);
                        d.SaveChanges(); //Commit after each file

                    }
                }


            }

            return true;
        }


        public bool SubmitProjectPledge(VMarketPledgeViewModel m)
        {
            //if (m.SoftwareSubmissionID == default(Guid))
            //    return false;
            //var contact = _users.ContactID;
            //using (new TransactionScope(TransactionScopeOption.Suppress))
            //{
            //    var d = new NKDC(_users.ApplicationConnectionString, null);
            //    //First check if any data is not owned by me
            //    if ((from o in d.MetaDatas where o.MetaDataID == m.SoftwareSubmissionID && o.VersionOwnerContactID != contact.Value select o).Any())
            //        return false;
            //    if ((from o in d.FileDatas where o.ReferenceID == m.SoftwareSubmissionID && o.VersionOwnerContactID != contact.Value select o).Any())
            //        return false;
            //    var s = (from o in d.MetaDatas where o.MetaDataID == m.SoftwareSubmissionID && o.Version == 0 && o.VersionDeletedBy == null select o).SingleOrDefault();
            //    if (s == null)
            //    {
            //        s = new MetaData
            //        {
            //            MetaDataID = m.SoftwareSubmissionID,
            //            MetaDataType = ConstantsHelper.DOCUMENT_TYPE_SOFTWARE_SUBMISSION,
            //            VersionOwnerContactID = contact,
            //            VersionUpdated = DateTime.UtcNow
            //        };
            //        d.MetaDatas.AddObject(s);
            //    }
            //    if (s.VersionOwnerContactID != contact)
            //        return false;
            //    if (!string.IsNullOrWhiteSpace(m.Description) || m.ForDevelopment || m.ForManagement || m.ForSale)
            //    {
            //        var content = JsonConvert.SerializeObject(m);
            //        if (content != s.ContentToIndex)
            //            s.ContentToIndex = content;
            //    }
            //    d.SaveChanges();
            //    var table = d.GetTableName<MetaData>();
            //    if (m.Files != null)
            //    {
            //        var mediaPath = HostingEnvironment.IsHosted ? HostingEnvironment.MapPath("~/Media/") ?? "" : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Media");
            //        var storagePath = Path.Combine(mediaPath, _settings.Name);
            //        foreach (var f in m.Files)
            //        {
            //            var filename = string.Concat(f.Value.FileName.Reverse().Take(50).Reverse());

            //            var file = new FileData
            //            {
            //                FileDataID = f.Key,
            //                TableType = table,
            //                ReferenceID = m.SoftwareSubmissionID,
            //                FileTypeID = null, //TODO give type
            //                FileName = filename,
            //                FileLength = f.Value.ContentLength,
            //                MimeType = f.Value.ContentType,
            //                VersionOwnerContactID = contact,
            //                DocumentType = ConstantsHelper.DOCUMENT_TYPE_SOFTWARE_SUBMISSION
            //            };
            //            m.FileLengths.Add(f.Key, f.Value.ContentLength);
            //            _media.GetMediaFolders(DIRECTORY_TEMP);
            //            var path = string.Format("{0}\\{1}-{2}-{3}", DIRECTORY_TEMP, m.SoftwareSubmissionID.ToString().Replace("-", ""), f.Key.ToString().Replace("-", "").Substring(15), filename.ToString().Replace("-", ""));
            //            var sf = _storage.CreateFile(path);
            //            using (var sw = sf.OpenWrite())
            //                f.Value.InputStream.CopyTo(sw);
            //            f.Value.InputStream.Close();
            //            try
            //            {

            //                using (var dh = new DocHelper.FilterReader(Path.Combine(storagePath, path)))
            //                    file.FileContent = dh.ReadToEnd();
            //            }
            //            catch { }
            //            using (var sr = sf.OpenRead())
            //                file.FileBytes = sr.ToByteArray();
            //            _storage.DeleteFile(path);
            //            file.FileChecksum = file.FileBytes.ComputeHash();
            //            d.FileDatas.AddObject(file);
            //            d.SaveChanges(); //Commit after each file

            //        }
            //    }


            //}

            return true;
        }

        public Guid? GetOrderInvoice(Guid orderID, string requestIPAddress)
        {
            try
            {
                var contact = _users.ContactID;
                var invoiceID = default(Guid?);
                using (new TransactionScope(TransactionScopeOption.Suppress))
                {
                    var d = new NKDC(_users.ApplicationConnectionString, null, false);
                    invoiceID = (from o in d.Supplies
                                 join i in d.Invoices on o.SupplyID equals i.SupplyID
                                 where o.CustomerPurchaseOrderID == orderID && o.PurchaseOrderCustomer.CustomerContactID == contact
                                 && o.Version == 0 && o.VersionDeletedBy == null
                                 && i.Version == 0 && i.VersionDeletedBy == null
                                 orderby i.VersionUpdated descending
                                 select i.InvoiceID).FirstOrDefault();
                }
                if (invoiceID.HasValue)
                    return GetInvoice(invoiceID.Value, requestIPAddress);
                return null;
            }
            catch
            {
                return null;
            }
        }

        public Guid? GetInvoice(Guid invoiceID, string requestIPAddress)
        {
            try
            {
                var application = _users.ApplicationID;
                var contact = _users.ContactID;
                var company = _users.ApplicationCompanyID;
                //var server = _users.ServerID;
                var now = DateTime.UtcNow;
                using (new TransactionScope(TransactionScopeOption.Suppress))
                {
                    var d = new NKDC(_users.ApplicationConnectionString, null, false);
                    var invoiceTableType = d.GetTableName(typeof(Invoice), true);
                    var invoice = (from o in d.Invoices
                                   where o.InvoiceID == invoiceID
                                   && o.Version == 0 && o.VersionDeletedBy == null
                                   select o).Single();
                    if (contact != invoice.CustomerContactID) //TODO: check other contact owner/company etc
                        throw new OrchardSecurityException(T(string.Format("Not authorized to view invoice contact: {0} invoice: {1}", contact, invoiceID)));
                    var download = (from o in d.Downloads
                                    join f in d.FileDatas on o.FileDataID equals f.FileDataID
                                    where f.ReferenceID == invoiceID && f.TableType == invoiceTableType
                                    && f.VersionDeletedBy == null && f.Version == 0 && o.VersionDeletedBy == null && o.Version == 0
                                    select o).FirstOrDefault();
                    if (download != null)
                        return download.DownloadID;
                    Stream stream = new MemoryStream();
                    invoice.GetPDF(ref stream, _storage.GetAbsolutePath(ConstantsHelper.PDF_LOGO));
                    stream.Seek(0, SeekOrigin.Begin);
                    var bytes = stream.ToByteArray();
                    var file = new FileData
                    {
                        FileDataID = Guid.NewGuid()
                        ,
                        TableType = invoiceTableType
                        ,
                        ReferenceID = invoiceID
                        ,
                        FileBytes = bytes
                        ,
                        FileTypeID = ConstantsHelper.FILE_TYPE_INVOICE
                        ,
                        FileName = string.Format("Invoice-[{0}].pdf", invoiceID)
                        ,
                        FileChecksum = bytes.ComputeHash()
                        ,
                        VersionOwnerContactID = contact
                        ,
                        VersionOwnerCompanyID = null //Only allow me to view
                        ,
                        DocumentType = ConstantsHelper.DOCUMENT_TYPE_INVOICE
                    };
                    stream.Close();
                    download = new Download
                    {
                        DownloadID = Guid.NewGuid(),
                        FileAllocated = now,
                        FilterContactID = contact,
                        RemainingDownloads = ConstantsHelper.DOWNLOADS_REMAINING_DEFAULT,
                        FileDataID = file.FileDataID,
                        ValidFrom = now,
                    };
                    download.FileData = file;
                    d.Downloads.AddObject(download);
                    d.SaveChanges();
                    return download.DownloadID;
                }
            }
            catch
            {
                return null;
            }
        }


        public IEnumerable<InvoiceViewModel> GetInvoices(int? startRowIndex = null, int? pageSize = null)
        {

            var contact = _users.ContactID;
            var application = _users.ApplicationID;
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new NKDC(_users.ApplicationConnectionString, null);
                var r = (from o in d.E_SP_GetInvoices(null, application, null, null, null, contact, null, startRowIndex, pageSize)
                        select o);
                var m = new List<InvoiceViewModel>();
                foreach (var o in r)
                {
                    InvoiceViewModel i = m.LastOrDefault();
                    if (i == null || i.InvoiceID != o.InvoiceID)
                    {
                        i = new InvoiceViewModel
                        {
                            InvoiceID = o.InvoiceID,                           
                            FreightTax = o.FreightTax,
                            FreightAmount = o.FreightAmount,
                            DiscountIncludesFreight = o.DiscountIncludesFreight,
                            DiscountAllFreight = o.DiscountAllFreight,
                            DiscountAmount = o.DiscountAmount,
                            TaxAmount = o.TaxAmount,
                            OriginalTotal = o.OriginalTotal,
                            CurrencyID = o.CurrencyID,
                            CurrencyPrefix = o.CurrencyPrefix,
                            CurrencyPostfix = o.CurrencyPostfix,
                            Total = o.Total,
                            Dated = o.Dated,
                            Communicated = o.Communicated,
                            InvoiceLines = new List<InvoiceLineViewModel>()

                        };
                        m.Add(i);
                    }
                    if (o.InvoiceLineID.HasValue && !i.InvoiceLines.Any(f => f.InvoiceLineID == o.InvoiceLineID))
                    {
                        ((List<InvoiceLineViewModel>)i.InvoiceLines).Add(new InvoiceLineViewModel
                        {
                            InvoiceLineID = o.InvoiceLineID,
                            InvoiceID = o.InvoiceID,
                            SupplyItemID = o.SupplyItemID,
                            ReferenceType = o.ReferenceType,
                            ReferenceID = o.ReferenceID,
                            Description = o.Description,
                            Quantity = o.Quantity,
                            TaxID = o.TaxID,
                            Tax = o.Tax,
                            TaxName = o.TaxName,
                            DiscountAmount = o.DiscountAmount,
                            OriginalSubtotal = o.OriginalSubtotal,
                            Subtotal = o.Subtotal
                        });
                    }

                }
                return m;
            }
        }

        public IEnumerable<SoftwareSubmissionViewModel> GetSoftware(int? startRowIndex = null, int? pageSize = null)
        {
             var contact = _users.ContactID;
            var application = _users.ApplicationID;
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new NKDC(_users.ApplicationConnectionString, null);
                var r = (from o in d.E_SP_GetSoftwareSubmissions(application, contact, null, startRowIndex, pageSize)
                         select o).ToArray();
                var m = new List<SoftwareSubmissionViewModel>();
                foreach (var o in r)
                {
                    if (o.ContentToIndex == null)
                        continue;
                    dynamic md = JsonConvert.DeserializeObject<NullableExpandoObject>(o.ContentToIndex);
                    SoftwareSubmissionViewModel s = m.LastOrDefault();
                    if (s == null || s.SoftwareSubmissionID != o.MetaDataID)
                    {
                        s = new SoftwareSubmissionViewModel
                        {
                            Description = md.Description,
                            Uploaded = o.VersionUpdated,
                            SoftwareSubmissionID = o.MetaDataID,
                            RowNumber = o.Row,
                            ForDevelopment = md.ForDevelopment,
                            ForManagement = md.ForManagement,
                            ForSale = md.ForSale,
                            ShareFiles = new Dictionary<Guid, string>()
                        };
                        m.Add(s);
                        if (o.FileDataID.HasValue && !s.ShareFiles.Any(f => f.Key == o.FileDataID))
                            s.ShareFiles.Add(o.FileDataID.Value, o.FileName);
                    }
                }
                return m;
            }            
        }

        public IEnumerable<LicenseViewModel> GetLicenses(int? startRowIndex = null, int? pageSize = null)
        {

            var contact = _users.ContactID;
            var application = _users.ApplicationID;
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new NKDC(_users.ApplicationConnectionString, null);
                var r = (from o in d.E_SP_GetLicenses(application, null, null, null, contact, null, null, null, null, startRowIndex, pageSize)
                        select o).ToArray();
                var m = new List<LicenseViewModel>();
                foreach(var o in r)
                {
                    LicenseViewModel l = m.LastOrDefault();
                    if (l == null || l.LicenseID != o.LicenseID)
                    {
                        l = new LicenseViewModel
                            {
                                LicenseID = o.LicenseID,
                                CompanyID = o.CompanyID,
                                ContactID = o.ContactID,
                                LicenseeGUID = o.LicenseeGUID,
                                LicenseeName = o.LicenseeName,
                                LicenseeUsername = o.LicenseeUsername,
                                LicenseeUniqueMachineCode1 = o.LicenseeUniqueMachineCode1,
                                LicenseeUniqueMachineCode2 = o.LicenseeUniqueMachineCode2,
                                LicenseeGroupID = o.LicenseeGroupID,
                                LicensorIP = o.LicensorIP,
                                LicensorName = o.LicensorName,
                                LicenseTypeID = o.LicenseTypeID,
                                LicenseType = o.LicenseType,
                                LicenseURL = o.LicenseURL,
                                RootServerName = o.RootServerName,
                                RootServerID = o.RootServerID,
                                ServerName = o.ServerName,
                                ServerID = o.ServerID,
                                ApplicationID = o.ApplicationID,
                                ServiceAuthenticationMethod = o.ServiceAuthenticationMethod,
                                ServiceAuthorisationMethod = o.ServiceAuthorisationMethod,
                                ValidFrom = o.ValidFrom,
                                Expiry = o.Expiry,
                                SupportExpiry = o.SupportExpiry,
                                ValidForDuration = o.ValidForDuration,
                                ValidForUnitID = o.ValidForUnitID,
                                ValidForUnitName = o.ValidForUnitName,
                                LicenseAssets = new List<LicenseAssetViewModel>(),
                                LicenseDownloads = new List<DownloadViewModel>()

                            };
                        m.Add(l);                       
                    }
                    if (o.Downloads != null)
                    {
                        dynamic[] downloads = JsonConvert.DeserializeObject<NullableExpandoObject[]>(o.Downloads);
                        foreach(dynamic dl in downloads)
                            ((List<DownloadViewModel>)l.LicenseDownloads).Add(new DownloadViewModel { DownloadID = dl.id as Guid?, Description = dl.description });
                    }                        
                    LicenseAssetViewModel la = l.LicenseAssets.FirstOrDefault(f => f.LicenseAssetID == o.LicenseAssetID);
                    if (o.LicenseAssetID.HasValue && la == null)
                    {
                        la = new LicenseAssetViewModel {
                            LicenseAssetID = o.LicenseAssetID,
                            LicenseID = o.LicenseID,
                            AssetID = o.AssetID,
                            ProRataCost = o.ProRataCost,
                            ModelID = o.ModelID,
                            ModelName = o.StandardModelName,
                            Restrictions = o.ModelRestrictions,
                            LicenseAssetModelParts = new List<LicenseAssetModelPartViewModel>()
                        };
                        ((List<LicenseAssetViewModel>)l.LicenseAssets).Add(la);
                    }
                    if (o.LicenseAssetModelPartID.HasValue && la != null && !la.LicenseAssetModelParts.Any(f=>f.LicenseAssetModelPartID == o.LicenseAssetModelPartID))
                    {
                        ((List<LicenseAssetModelPartViewModel>)la.LicenseAssetModelParts).Add(
                            new LicenseAssetModelPartViewModel {
                                LicenseAssetModelPartID = o.LicenseAssetModelPartID,
                                LicenseID = o.LicenseID,
                                LicenseAssetID = o.LicenseAssetID,
                                ModelID = o.ModelID,
                                ModelPartID = o.ModelPartID,
                                PartName = o.StandardPartName,
                                Restrictions = o.PartRestrictions
                            }
                            );
                    }
                   
                }
                return m;
            }
        }

        public ContactViewModel GetContact(Guid? contact = null)
        {
            if (!contact.HasValue)
                contact = _users.ContactID;
            var application = _users.ApplicationID;
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new NKDC(_users.ApplicationConnectionString, null);
                var r = (from o in d.Contacts
                         where o.ContactID == contact.Value && o.Version == 0 && o.VersionDeletedBy == null
                         select o).Single();
                var c = new ContactViewModel
                             {
                                 ContactID = r.ContactID,
                                 ContactName = r.ContactName,
                                 Title = r.Title,
                                 Surname = r.Surname,
                                 Firstname = r.Firstname,
                                 Username = r.Username,
                                 Hash = r.Hash,
                                 DefaultEmail = r.DefaultEmail,
                                 DefaultEmailValidated = r.DefaultEmailValidated,
                                 DefaultMobile = r.DefaultMobile,
                                 DefaultMobileValidated = r.DefaultMobileValidated,
                                 MiddleNames = r.MiddleNames,
                                 Initials = r.Initials,
                                 DOB = r.DOB,
                                 BirthCountryID = r.BirthCountryID,
                                 BirthCity = r.BirthCity,
                                 AspNetUserID = r.AspNetUserID,
                                 XafUserID = r.XafUserID,
                                 OAuthID = r.OAuthID,
                                 Photo = r.Photo,
                                 ShortBiography = r.ShortBiography,
                                 ContactAddresses = new List<ContactAddressViewModel>()
                             };

                var a = ((List<ContactAddressViewModel>)c.ContactAddresses);
                bool primary = true;
                foreach (var ca in (from o in d.ContactAddresses where o.ContactID == contact && o.Version == 0 && o.VersionDeletedBy == null && o.Address.Version == 0 && o.Address.VersionDeletedBy == null orderby o.VersionUpdated descending select o.Address))
                {
                    var address = new ContactAddressViewModel
                    {
                        AddressID = ca.AddressID,
                        AddressTypeID = ca.AddressTypeID,
                        AddressName = ca.AddressName,
                        Sequence = ca.Sequence,
                        Street = ca.Street,
                        Extended = ca.Extended,
                        City = ca.City,
                        State = ca.State,
                        Country = ca.Country,
                        Postcode = ca.Postcode,
                        IsHQ = ca.IsHQ,
                        IsPostBox = ca.IsPostBox,
                        IsBusiness = ca.IsBusiness,
                        IsHome = ca.IsHome,
                        Phone = ca.Phone,
                        Fax = ca.Fax,
                        Email = ca.Email,
                        Mobile = ca.Mobile,
                        LocationID = ca.LocationID
                    };
                    if (primary)
                    {
                        c.PrimaryAddress = address;
                    }
                    a.Add(address);
                    
                }
                return c;
            }
        }

        public bool UpdateCart(NameValueCollection m)
        {
            if (m == null)
                return false;
            Guid orderID;
            if (!Guid.TryParse(m["OrderID"], out orderID))
                return false;
            var order = GetOrder(orderID);
            foreach (var p in order.Products)
            {
                if (p.ModelID.HasValue && p.ModelID != default(Guid))
                {
                    var temp = m[p.ModelID.ToString()];
                    if (!string.IsNullOrWhiteSpace(temp))
                    {
                        int mQtyNew;
                        if (!int.TryParse(temp, out mQtyNew))
                            return false;
                        p.ModelUnits = mQtyNew;

                    }
                }
            }
            UpdateOrder(order);
            return true;
        }

        public bool UpdateAccount(AccountViewModel m)
        {
               
            var contact = _users.ContactID;
            var application = _users.ApplicationID;
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new NKDC(_users.ApplicationConnectionString, null);
                //Ensure only owner can change details
                if (m.Contact.ContactID.HasValue && m.Contact.ContactID.Value != contact)
                    return false;
                var c = (from o in d.Contacts where o.Version == 0 && o.VersionDeletedBy == null && o.ContactID==contact select o).Single();
                var a = (from o in d.ContactAddresses where o.ContactID == contact && o.Version == 0 && o.VersionDeletedBy == null && o.Address.Version == 0 && o.Address.VersionDeletedBy == null orderby o.VersionUpdated descending select o.Address).FirstOrDefault();
                if (a == null)
                {
                    a = new Address
                    {
                        AddressID = Guid.NewGuid(),
                        VersionUpdated = DateTime.UtcNow
                    };
                    var ca = new ContactAddress
                    {
                        ContactAddressID = Guid.NewGuid(),
                        AddressID = a.AddressID,
                        ContactID = contact,
                        VersionUpdated = DateTime.UtcNow
                    };
                    a.ContactAddresses.Add(ca);
                    d.Addresses.AddObject(a);
                }
                if (!string.IsNullOrWhiteSpace(m.Contact.Title) && c.Title != m.Contact.Title)
                    c.Title = m.Contact.Title;
                if (!string.IsNullOrWhiteSpace(m.Contact.Firstname) && c.Firstname != m.Contact.Firstname)
                    c.Firstname = m.Contact.Firstname;
                if (!string.IsNullOrWhiteSpace(m.Contact.Surname) && c.Surname != m.Contact.Surname)
                    c.Surname = m.Contact.Surname;
                if (!string.IsNullOrWhiteSpace(m.Contact.DefaultEmail) && c.DefaultEmail != m.Contact.DefaultEmail)
                {
                    if (!_users.UpdateUserEmail(m.Contact.DefaultEmail))
                        return false;
                    c.DefaultEmail = m.Contact.DefaultEmail;
                }
                if (!string.IsNullOrWhiteSpace(m.Contact.DefaultEmail) && a.Email != m.Contact.DefaultEmail)
                    a.Email = m.Contact.DefaultEmail; //Use the same email
                if (!string.IsNullOrWhiteSpace(m.Contact.DefaultMobile) && c.DefaultMobile != m.Contact.DefaultMobile)
                    c.DefaultMobile = m.Contact.DefaultMobile;
                if (!string.IsNullOrWhiteSpace(m.Contact.DefaultMobile) && a.Mobile != m.Contact.DefaultMobile)
                    a.Mobile = m.Contact.DefaultMobile; //Use the same mobile
                if (a.AddressName != m.Contact.PrimaryAddress.AddressName)
                    a.AddressName = m.Contact.PrimaryAddress.AddressName; //Company Name
                if (!string.IsNullOrWhiteSpace(m.Contact.PrimaryAddress.Street) && a.Street != m.Contact.PrimaryAddress.Street)
                    a.Street = m.Contact.PrimaryAddress.Street;
                if (a.Extended != m.Contact.PrimaryAddress.Extended)
                    a.Extended = m.Contact.PrimaryAddress.Extended;
                if (!string.IsNullOrWhiteSpace(m.Contact.PrimaryAddress.City) && a.City != m.Contact.PrimaryAddress.City)
                    a.City = m.Contact.PrimaryAddress.City;
                if (!string.IsNullOrWhiteSpace(m.Contact.PrimaryAddress.State) && a.State != m.Contact.PrimaryAddress.State)
                    a.State = m.Contact.PrimaryAddress.State;
                if (!string.IsNullOrWhiteSpace(m.Contact.PrimaryAddress.Country) && a.Country != m.Contact.PrimaryAddress.Country)
                    a.Country = m.Contact.PrimaryAddress.Country;
                if (!string.IsNullOrWhiteSpace(m.Contact.PrimaryAddress.Postcode) && a.Postcode != m.Contact.PrimaryAddress.Postcode)
                    a.Postcode = m.Contact.PrimaryAddress.Postcode; 

                d.SaveChanges();
                return true;
            }           
                        
        }

        public bool IsUserModelLicenseValid(Guid modelID, Guid? contactID = null)
        {
            if (_orchardServices.Authorizer.Authorize(Orchard.Security.StandardPermissions.SiteOwner))
                return true;
            var toReturn = false;
            var application = _users.ApplicationID;
            var username = _users.Username;
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new NKDC(_users.ApplicationConnectionString, null);
                if (contactID.HasValue)
                    username = (from o in d.Contacts where o.Version == 0 && o.VersionDeletedBy == null && o.ContactID == contactID select o.Username).Single();
                var r = (from o in d.E_SP_GetLicenses(application, null, null, null, null, username, null, modelID, null, null, null)
                         select o).ToArray();
                if (r.Any(f => f.Expiry > DateTime.UtcNow || f.Expiry == default(DateTime?)))
                    return true;
                else
                {
                    var externalModelNames = (from o in d.ApplicationPaymentProviderProductModels
                                              where
                                                  o.ModelID == modelID &&
                                                  o.VersionDeletedBy == null &&
                                                  o.Version == 0 &&
                                                  o.ApplicationPaymentProviderProduct.Version == 0 &&
                                                  o.ApplicationPaymentProviderProduct.VersionDeletedBy == null
                                              select o.ApplicationPaymentProviderProduct.PaymentProviderProductName).AsEnumerable().Select(f=>f.ToUpperInvariant()).ToArray();
                    foreach(var l in r) {
                        if (string.IsNullOrWhiteSpace(l.PaymentReferences))
                            continue;
                        if (!l.VersionUpdated.HasValue || (l.VersionUpdated.HasValue && l.VersionUpdated.Value.AddDays(0.5) < DateTime.UtcNow)) //only check with payment provider once every 12 hours
                        {
                            var license = (from o in d.Licenses where o.LicenseID == l.LicenseID select o).Single();
                            var payments = l.PaymentReferences.Split(new string[] { "::" }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var payment in payments)
                            {
                                string sub;
                                if (_payment.IsSubscriptionValid(payment, out sub))
                                {
                                    if (externalModelNames.Contains(sub.ToUpperInvariant()))
                                    {
                                        //Update license expiry OK
                                        var multiplier = (from o in d.DictionaryUnits
                                                          where
                                                              (o.EquivalentUnitID == ConstantsHelper.UNIT_SI_SECONDS || o.UnitID == ConstantsHelper.UNIT_SI_SECONDS) &&
                                                              o.UnitID == l.ValidForUnitID &&
                                                              o.Version == 0 &&
                                                              o.VersionDeletedBy == null &&
                                                              o.EquivalentMultiplier != null
                                                          select o.EquivalentMultiplier).FirstOrDefault();
                                        if (multiplier.HasValue && multiplier.Value >= 1)
                                        {
                                            while (license.Expiry < DateTime.UtcNow)
                                            {
                                                license.Expiry = license.Expiry.Value.AddSeconds(Convert.ToDouble(multiplier));
                                                license.SupportExpiry = license.Expiry;
                                            }                                            
                                        }
                                        toReturn = true;

                                    }
                                }
                                else
                                {
                                    if (externalModelNames.Contains(sub.ToUpperInvariant()))
                                    {
                                        //Update license expiry EXPIRED
                                        //just do nothing for now
                                    }
                                }
                            }
                            license.VersionUpdated = DateTime.UtcNow;
                            d.SaveChanges();
                        }
                    }
                }

            }
            return toReturn;

        }
    }
}
