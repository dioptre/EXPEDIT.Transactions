using System;
using Orchard;

namespace EXPEDIT.Transactions.Services.Payments
{
    public interface IMerchant : IDependency
    {
        object Environment { get; set; }
        string EnvironmentString { get; set; }
        string MerchantId { get; set; }
        string PrivateKey { get; set; }
        string PublicKey { get; set; }
        string ClientPublicKey { get; set; }
        string ClientPrivateKey { get; set; }
        string ServerReturnURL { get; set; }
    }
}
