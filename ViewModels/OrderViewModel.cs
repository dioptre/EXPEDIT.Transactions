﻿using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using System;
using System.Collections.Generic;

namespace EXPEDIT.Transactions.ViewModels
{
    public class OrderViewModel
    {
        [HiddenInput, Required, DisplayName("Order ID:")]
        public Guid? OrderID { get; set; }

        public IEnumerable<OrderProductViewModel> Products { get; set; }
        public string PaymentRedirectURL { get; set; }
        public string PaymentData { get; set; }
        public string PaymentResponse { get; set; }
        public string PaymentQuery { get; set; }
        public string PaymentQueryResponse { get; set; }
        public uint PaymentStatus { get; set; } //EXPEDIT.Transactions.Services.Payments.PaymentUtils.PaymentStatus
        public uint PaymentError { get; set; } //EXPEDIT.Transactions.Services.Payments.PaymentUtils.PaymentError
        public string PaymentCustomerID { get; set; }
        public string PaymentFirstname { get; set; }
        public string PaymentLastname { get; set; }
        public string PaymentPostcode { get; set; }
        public string PaymentNumber { get; set; }
        public string PaymentExpirationMonth { get; set; }
        public string PaymentExpirationYear { get; set; }
        public string PaymentVerification { get; set; }
        public SelectList TransactionType { get; set; }

    }
}