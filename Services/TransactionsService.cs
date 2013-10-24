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

namespace EXPEDIT.Transactions.Services {
    
    [UsedImplicitly]
    public class TransactionsService : ITransactionsService {
        private readonly IOrchardServices _orchardServices;
        private readonly IContentManager _contentManager;
        private readonly IMessageManager _messageManager;
        private readonly IScheduledTaskManager _taskManager;
        public ILogger Logger { get; set; }

        public TransactionsService(IContentManager contentManager, IOrchardServices orchardServices, IMessageManager messageManager, IScheduledTaskManager taskManager)
        {
            _orchardServices = orchardServices;
            _contentManager = contentManager;
            _messageManager = messageManager;
            _taskManager = taskManager;
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
            //SupplierModel
            throw new NotImplementedException();
        }


        public ProductViewModel GetProduct(Guid productID)
        {
            throw new NotImplementedException();
        }


        
       
    }
}
