using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using Orchard;
using System.ServiceModel;
using EXPEDIT.Transactions.ViewModels;

namespace EXPEDIT.Transactions.Services
{
     [ServiceContract]
    public interface ITransactionsService : IDependency 
    {
         [OperationContract]
         void ConfirmPayment(OrderViewModel order);
         
         [OperationContract]
         void ConfirmOrder(OrderViewModel order);

         [OperationContract]
         OrderViewModel GetOrder();

         [OperationContract]
         void UpdateOrder(OrderViewModel order);

         [OperationContract]
         IEnumerable<ProductViewModel> GetProducts(string text = null, Guid? supplierModelID = null, int? startRowIndex = null, int? pageSize = null);

         [OperationContract]
         ProductViewModel GetProduct(Guid productID);

         [OperationContract]
         void IncrementDownloadCounter(Guid productID);
  

    }
}