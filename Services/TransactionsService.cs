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


        public IEnumerable<ProductViewModel> GetProducts()
        {
            var supplier = _users.CompanyID;
            var directory = _media.GetPublicUrl(@"EXPEDIT.Transactions");
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new XODBC(_users.ApplicationConnectionString, null);
                return (from o in d.SupplierModels where o.SupplierID == supplier
                        join m in d.DictionaryModels on o.ModelID equals m.ModelID
                            where m.DeviceTypeID == ConstantsHelper.DEVICE_TYPE_SOFTWARE
                        join u in d.DictionaryUnits on o.PriceUnitID equals u.UnitID
                        join c in d.Companies on m.CompanyID equals c.CompanyID
                        //join c in d.Currencies on o.CurrencyID equals c.CurrencyID //TODO
                        join s in d.StatisticDatas on m.ModelID equals s.ReferenceID
                        join df in d.Downloads on o.SupplierFileDataID equals df.FileDataID 
                            into gdf
                        from lj_df in gdf.DefaultIfEmpty()
                        where lj_df.FilterApplicationID == null && lj_df.FilterCompanyID == null && lj_df.FilterContactID == null && lj_df.FilterServerID == null && lj_df.LicenseID == null                        
                        && s.StatisticName == "Model Downloads"
                        select new ProductViewModel { 
                            ModelID = o.ModelID, 
                            CompanyID = m.CompanyID, 
                            MediaDirectory = directory, 
                            PricePerUnit = o.PricePerUnit, 
                            PriceUnitID = o.PriceUnitID,
                            CostUnit = u.StandardUnitName,
                            SupplierID = supplier,
                            Title = m.StandardModelName,
                            Subtitle = o.SupplierModelNumber,
                            HTML = o.SupplierModelDescription,
                            Manufacturer = c.CompanyName//,
                            //CostText = o.PricePerUnit ?? default(decimal),
                            //FreeDownloadID = (lj_df == null ? default(Guid?) : lj_df.DownloadID),
                            //Downloads = s.Count ?? 0
                        }).ToArray();
            }
        }


        public ProductViewModel GetProduct(Guid productID)
        {
            throw new NotImplementedException();
        }


        
       
    }
}
