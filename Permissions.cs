using System.Collections.Generic;
using Orchard.Environment.Extensions.Models;
using Orchard.Security.Permissions;

namespace EXPEDIT.Transactions {
    public class Permissions : IPermissionProvider {
        public static readonly Permission PartnerFinancial = new Permission { Description = "Partner (Financial)", Name = "PartnerFinancial" };
        public static readonly Permission PartnerSoftware = new Permission { Description = "Partner (Software)", Name = "PartnerSoftware" };
        public static readonly Permission Partner = new Permission { Description = "Partner", Name = "Partner", ImpliedBy = new[] { PartnerSoftware, PartnerFinancial } };

        public virtual Feature Feature { get; set; }

        public IEnumerable<Permission> GetPermissions() {
            return new[] {
                Partner,
                PartnerSoftware,
                PartnerFinancial
            };
        }

        public IEnumerable<PermissionStereotype> GetDefaultStereotypes() {
            return new[] {
                new PermissionStereotype {
                    Name = "Administrator",
                    Permissions = new[] {Partner, PartnerSoftware, PartnerFinancial}
                },
                new PermissionStereotype {
                    Name = "Partner",
                    Permissions = new [] {Partner, PartnerSoftware, PartnerFinancial}
                },
            };
        }

    }
}