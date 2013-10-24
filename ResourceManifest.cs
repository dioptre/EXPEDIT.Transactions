using Orchard.UI.Resources;

namespace EXPEDIT.Transactions {
    public class ResourceManifest : IResourceManifestProvider {
        public void BuildManifests(ResourceManifestBuilder builder) {
            builder.Add().DefineStyle("Transactions").SetUrl("expedit-transactions.css");
        }
    }
}
