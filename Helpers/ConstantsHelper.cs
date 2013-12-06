using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EXPEDIT.Transactions.Helpers
{
    public class ConstantsHelper
    {
        public static Guid ACCOUNT_TYPE_ONLINE = new Guid("5C329B8D-007D-435E-8261-4FA72D7DF28A");
        public static Guid DEVICE_TYPE_SOFTWARE = new Guid("3f526009-827a-41b0-a633-14b422bdf27f");
        public static Guid ROUTE_TYPE_STORE_INTERNAL = new Guid("1a01fc89-c014-433f-be04-39c2f956aeb2");
        public static Guid ROUTE_TYPE_STORE_EXTERNAL = new Guid("7c9f3a25-011b-4f5e-8b1e-d345da13f8b1");
        public static int SQL_MAX_INT = 2147483647;
        public static string STAT_NAME_DOWNLOADS = "Downloads";
        public static string STAT_NAME_CLICKS_BUY = "ClicksBuy";
        public static string STAT_NAME_CLICKS_CONFIRM = "ClicksConfirm";
        public static string METADATA_ANTIFORGERY = "E_ANTIFORGERY";
    }
}