using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using Orchard;
using System.ServiceModel;
using EXPEDIT.Transactions.ViewModels;
using NKD.Models;

namespace EXPEDIT.Transactions.Services
{
     [ServiceContract]
    public interface ITransactionsService : IDependency 
    {
         [OperationContract]
         PartnerViewModel GetPartnership(Guid? contractID = default(Guid?));

         [OperationContract]
         bool UpdatePartnership(PartnerViewModel m, string IPAddress);

         [OperationContract]
         bool SendTwoStepAuthentication(ref IVerifyMobile verification);

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
         IEnumerable<DownloadViewModel> GetDownloads(Guid orderID);

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

         [OperationContract]
         bool SubmitSoftware(SoftwareSubmissionViewModel s);

    }
}