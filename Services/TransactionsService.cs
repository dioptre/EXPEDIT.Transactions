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
using EXPEDIT.Transactions.Helpers;
using XODB.Services;
using Orchard.Media.Services;
using EXPEDIT.Transactions.Services.Payments;
using EntityFramework.Extensions;

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
                var contracts = (from o in d.SupplyContractConditions where o.Supply.SupplierPurchaseOrderID == orderID && o.Version == 0 && o.VersionDeletedBy == null
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
            UpdateOrderOwner(order);
        }
         

        public OrderViewModel GetOrder(Guid orderID)
        {
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new XODBC(_users.ApplicationConnectionString, null);
                var items = (from o in d.SupplyItems where o.Supply.SupplierPurchaseOrderID==orderID && o.Version==0 && o.VersionDeletedBy == null 
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
                }
            
                var c = (from o in d.Contacts where o.ContactID == contact select o).FirstOrDefault();
                if (c != null)
                {
                    order.PaymentFirstname = c.Firstname;
                    order.PaymentLastname = c.Surname;
                    order.PaymentEmail = c.DefaultEmail;
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

                var c = (from o in d.Contacts where o.ContactID == contact select o).Single();
                if (string.IsNullOrWhiteSpace(c.Firstname))
                    c.Firstname = order.PaymentFirstname;
                if (string.IsNullOrWhiteSpace(c.Surname))
                    c.Surname = order.PaymentLastname;

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
                Supply s = (from o in d.Supplies where o.SupplierPurchaseOrderID == po.PurchaseOrderID select o).SingleOrDefault();
                if (s == null)
                {
                    s = new Supply { SupplyID = Guid.NewGuid(), SupplierPurchaseOrderID = po.PurchaseOrderID, DateOrdered = currentTime};
                    d.Supplies.AddObject(s);
                }
                //SupplyItem
                var oldItems = (from o in d.SupplyItems where o.Supply.SupplierPurchaseOrderID == po.PurchaseOrderID select o).ToList();
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
                        item.TaxPart = item.CostPart * 0.1m;
                        item.SubtotalPart = item.CostPart + item.TaxPart;
                    }
                    else
                    {
                        item.ModelUnitID = p.ProductUnitID;
                        item.CostPerUnitModel = p.PricePerUnit;
                        item.QuantityModel = p.Units;
                        item.CostModel = item.CostPerUnitModel * item.QuantityModel;
                        item.TaxModel = item.CostModel * 0.1m;
                        item.SubtotalModel = item.CostModel + item.TaxModel;
                    }

                    item.LabourUnitID = null;
                    item.CostPerUnitLabour = 0;
                    item.QuantityLabour = 0;
                    item.CostLabour = 0;
                    item.TaxLabour = 0;
                    item.SubtotalLabour = 0;
                    
                    item.Tax = item.TaxLabour + item.TaxModel + item.TaxPart; //TODO: If taxation gets complicated use itemtax table
                    item.OriginalSubtotal = item.SubtotalLabour + item.SubtotalModel + item.SubtotalPart;
                    item.Subtotal = item.OriginalSubtotal; //TODO: Discounting
                    
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


        public IEnumerable<ProductViewModel> GetProducts(string text = null, Guid? supplierModelID = null, int? startRowIndex = null, int? pageSize=null)
        {
            var supplier = _users.CompanyID;
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
            var supplier = _users.CompanyID;
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
       
    }
}
