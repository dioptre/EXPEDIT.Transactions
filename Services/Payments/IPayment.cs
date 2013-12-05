using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EXPEDIT.Transactions.ViewModels;
using Orchard;

namespace EXPEDIT.Transactions.Services.Payments
{
    public interface IPayment : IDependency
    {
        void MakePayment(ref OrderViewModel order);
        void MakePaymentResult(ref OrderViewModel order);
        void PreparePayment(ref OrderViewModel order);
        void PreparePaymentResult(ref OrderViewModel order);
    }
}
