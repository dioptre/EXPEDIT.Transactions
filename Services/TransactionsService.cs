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

namespace EXPEDIT.Transactions.Services {
    
    [UsedImplicitly]
    public class TransactionsService : ITransactionsService {
        private readonly IOrchardServices _orchardServices;
        private readonly IContentManager _contentManager;
        private readonly IMessageManager _messageManager;
        private readonly IScheduledTaskManager _taskManager;
        private readonly IUsersService _users;
        private readonly IMediaService _media;
        public ILogger Logger { get; set; }

        public TransactionsService(IContentManager contentManager, IOrchardServices orchardServices, IMessageManager messageManager, IScheduledTaskManager taskManager, IUsersService users,  IMediaService media )
        {
            _orchardServices = orchardServices;
            _contentManager = contentManager;
            _messageManager = messageManager;
            _taskManager = taskManager;
            _media = media;
            _users = users;
            T = NullLocalizer.Instance;
            Logger = NullLogger.Instance;
        }

        public Localizer T { get; set; }

        public void ConfirmPayment(OrderViewModel order)
        {
            throw new NotImplementedException();
        }
         

        public void ConfirmOrder(OrderViewModel order)
        {
            throw new NotImplementedException();
        }

        public OrderViewModel GetOrder()
        {
            throw new NotImplementedException();
        }


        public void UpdateOrder(OrderViewModel order)
        {
            throw new NotImplementedException();
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
                            PaymentProviderProductID = o.ApplicationPaymentProviderID,
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

        public void IncrementConfirmCounter(Guid supplierModelID, Guid modelID)
        {
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new XODBC(_users.ApplicationConnectionString, null, false);
                IncrementStatistic(d, supplierModelID, d.GetTableName<SupplierModel>(), ConstantsHelper.STAT_NAME_CLICKS_CONFIRM);
                IncrementStatistic(d, modelID, d.GetTableName<DictionaryModel>(), ConstantsHelper.STAT_NAME_CLICKS_CONFIRM);
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
