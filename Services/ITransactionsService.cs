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
         void MakePayment(ref OrderViewModel order);

         [OperationContract]
         void MakePaymentResult(ref OrderViewModel order);

         [OperationContract]
         void PreparePayment(ref OrderViewModel order);

         [OperationContract]
         void PreparePaymentResult(ref OrderViewModel order);

         [OperationContract]
         OrderViewModel GetOrder(Guid orderID);

         [OperationContract]
         void GetOrderOwner(ref OrderViewModel order);

         [OperationContract]
         bool GetOrderPaid(Guid orderID);

         [OperationContract]
         bool GetOrderProcessed(Guid orderID);

         [OperationContract]
         void UpdateOrder(OrderViewModel order);

         [OperationContract]
         void UpdateOrderOwner(OrderViewModel order);

         [OperationContract]
         void UpdateOrderPaid(OrderViewModel order);

         [OperationContract]
         IEnumerable<ProductViewModel> GetProducts(string text = null, Guid? supplierModelID = null, int? startRowIndex = null, int? pageSize = null);

         [OperationContract]
         ProductViewModel GetProduct(Guid supplierModelID);

         [OperationContract]
         void IncrementDownloadCounter(Guid productID);

         [OperationContract]
         void IncrementBuyCounter(Guid supplierModelID, Guid modelID);
         
         [OperationContract]
         IEnumerable<ContractConditionViewModel> GetContractConditions(Guid[] referenceIDs);

    }
}