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
        Guid? GetInvoice(Guid invoiceID, string requestIPAddress);

        [OperationContract]
        Guid? GetOrderInvoice(Guid orderID, string requestIPAddress);

        [OperationContract]
        PartnerViewModel GetPartnership(ref PartnerViewModel partnership);

        [OperationContract]
        bool UpdatePartnership(PartnerViewModel m, string IPAddress);

        [OperationContract]
        bool SendTwoStepAuthentication(ref VerifyMobileModel verification);

        [OperationContract]
        void MakePayment(ref OrderViewModel order);

        [OperationContract]
        void MakePaymentResult(ref OrderViewModel order);

        [OperationContract]
        void PreparePayment(ref OrderViewModel order);

        [OperationContract]
        void PreparePaymentResult(ref OrderViewModel order);

        [OperationContract]
        OrderViewModel GetOrder(Guid orderID, bool detailed=false);

        [OperationContract]
        OrderViewModel GetOrderLast(bool detailed=false);

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

        [OperationContract]
        IEnumerable<InvoiceViewModel> GetInvoices(int? startRowIndex = null, int? pageSize = null);

        [OperationContract]
        IEnumerable<LicenseViewModel> GetLicenses(int? startRowIndex = null, int? pageSize = null);

        [OperationContract]
        ContactViewModel GetContact(Guid? contactID = null);

        [OperationContract]
        bool UpdateAccount(AccountViewModel m);

    }
}