using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EXPEDIT.Transactions.Services.Payments
{
    public class PaymentUtils
    {
        [Flags]
        public enum PaymentStatus : uint
        {
            Error = 0x00,
            Success = 0x01,
            ReceivedCustomer = 0x02,
            Subscribed = 0x04,
            Transacted = 0x08
        }

        public enum PaymentError : uint
        {
            BadCustomerID = 0x10000,
            BadPayment = 0x10001,
        }
        

    }
}