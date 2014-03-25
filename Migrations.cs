using System;
using System.Collections.Generic;
using System.Data;
using Orchard.ContentManagement.Drivers;
using Orchard.ContentManagement.MetaData;
using Orchard.ContentManagement.MetaData.Builders;
using Orchard.Core.Contents.Extensions;
using Orchard.Data.Migration;
using NKD.Models;


namespace EXPEDIT.Transactions {
    public class Migrations : DataMigrationImpl {

        public int Create() {

            ContentDefinitionManager.AlterTypeDefinition("CartMiniWidget", cfg => cfg
               .WithPart("WidgetPart")
               .WithPart("CommonPart")
               .WithSetting("Stereotype", "Widget"));

            return 1;
        }    

        //public int UpdateFrom1()
        //{
        //    return 2;
        //}
       
    }
}