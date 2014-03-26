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
                dynamic packageDisplay = context.New.CartMini(
                    Order: _transactions.GetOrderCurrent()
                );
                //context.Content.As<WidgetPart>().Zone
                context.Shape.Zones["Content"].Add(packageDisplay);
            }
		}
    }
}


       


