using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Xml.Linq;
using JetBrains.Annotations;
using Orchard.ContentManagement;
using Orchard.ContentManagement.Aspects;
using Orchard.Core.Common.Models;
using Orchard.Core.XmlRpc;
using Orchard.Core.XmlRpc.Models;
using Orchard.Environment.Extensions;
using Orchard.Localization;
using Orchard.Logging;
using Orchard.Mvc.Extensions;
using Orchard.Security;
using Orchard.Mvc.Html;
using Orchard.Core.Title.Models;
using Newtonsoft.Json;
using CNX.Shared.Helpers;
using CNX.Shared.Models;
using EXPEDIT.Share.Helpers;
using System.Dynamic;
using ImpromptuInterface;
using NKD.Services;
using NKD.Helpers;

namespace EXPEDIT.Transactions.Services {
    [UsedImplicitly]
    public class XmlRpcHandler : IXmlRpcHandler {
        private readonly IContentManager _contentManager;
        private readonly IAuthorizationService _authorizationService;
        private readonly IMembershipService _membershipService;
        private readonly RouteCollection _routeCollection;
        private readonly ITransactionsService _transactions;
        private readonly IUsersService _users;

        public XmlRpcHandler(IContentManager contentManager,
            IAuthorizationService authorizationService, 
            IMembershipService membershipService, 
            RouteCollection routeCollection,
            ITransactionsService transactions,
            IUsersService users) {
            _contentManager = contentManager;
            _authorizationService = authorizationService;
            _membershipService = membershipService;
            _routeCollection = routeCollection;
            _transactions = transactions;
            _users = users;
            Logger = NullLogger.Instance;
            T = NullLocalizer.Instance;
        }

        public ILogger Logger { get; set; }
        public Localizer T { get; set; }

        public void SetCapabilities(XElement options) {
            const string manifestUri = "http://schemas.expedit.com.au/services/manifest/transactions";
            options.SetElementValue(XName.Get("supportsSlug", manifestUri), "Yes");
        }

        public void Process(XmlRpcContext context) {
            var urlHelper = new UrlHelper(context.ControllerContext.RequestContext, _routeCollection);
            if (context.Request.MethodName == "transactions.validateModelContact")
            {
                var result = validateModelContact(
                    Convert.ToString(context.Request.Params[0].Value),
                    Convert.ToString(context.Request.Params[1].Value),
                    context._drivers);
                context.Response = new XRpcMethodResponse().Add(result);
            }
        }



        private string validateModelContact(string modelID, string contactID, IEnumerable<IXmlRpcDriver> drivers)
        {
            Guid mid, cid;
            if (!Guid.TryParse(modelID, out mid))
                return null;
            if (!Guid.TryParse(contactID, out cid))
                return null;
            return JsonConvert.SerializeObject(new { valid = _transactions.IsUserModelLicenseValid(mid, cid)});
        }

        
    }
}
