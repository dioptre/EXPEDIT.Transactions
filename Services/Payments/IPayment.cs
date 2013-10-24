using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EXPEDIT.Transactions.ViewModels;

namespace EXPEDIT.Transactions.Services.Payments
{
    public interface IPayment
    {
        bool MakePayment(OrderViewModel order);
    }
}
