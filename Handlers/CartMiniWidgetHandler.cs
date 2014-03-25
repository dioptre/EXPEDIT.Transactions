using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using JetBrains.Annotations;
using Orchard.ContentManagement;
using Orchard.Data;
using Orchard.ContentManagement.Handlers;
using Orchard.Localization;
using EXPEDIT.Transactions.Services;
using EXPEDIT.Transactions.ViewModels;
//using Orchard.Widgets.Models;
using Orchard;

namespace EXPEDIT.Transactions.Handlers
{
    public class CartMiniWidgetHandler : ContentHandler
    {
        private readonly ITransactionsService _transactions;
        public CartMiniWidgetHandler(ITransactionsService transactions)
        {
            _transactions= transactions;
        }
		protected override void BuildDisplayShape(BuildDisplayContext context)
		{
			base.BuildDisplayShape(context);

            if (context.ContentItem.ContentType == "CartMiniWidget")
            {
                //Try Get Cookie or Last Order
                OrderViewModel order = null;
                Guid id = default(Guid);

                //Guid id = System.Web.HttpContext.Current.Request;
                var ii = System.Web.HttpContext.Current.Request.Url.Query.IndexOf("id=");
                if (ii > -1)
                {
                    ii += 3;
                    var ids = string.Join("", System.Web.HttpContext.Current.Request.Url.Query.Skip(ii).Take(36));
                    Guid.TryParse(ids, out id);
                }
                else
                {
                    ii = System.Web.HttpContext.Current.Request.Url.AbsoluteUri.Length - 36;
                    if (ii > -1)
                    {
                        var ids = string.Join("", System.Web.HttpContext.Current.Request.Url.AbsoluteUri.Skip(ii).Take(36));
                        Guid.TryParse(ids, out id);
                    }
                }

                if (id == default(Guid))
                {
                    var cookie = System.Web.HttpContext.Current.Request.Cookies["OrderID"];
                    if (cookie != null)
                    {
                        Guid.TryParse(cookie.Value, out id);
                    }
                }

                if (id == default(Guid))
                    order = _transactions.GetOrderLast(true);
                else
                    order = _transactions.GetOrder(id,true);

                dynamic packageDisplay = context.New.CartMini(
                    Order: order
                );
                //context.Content.As<WidgetPart>().Zone
                context.Shape.Zones["Content"].Add(packageDisplay);
            }
		}
    }
}


       


