using System;

namespace Efocus.Sitecore.LuceneWebSearch
{
    [Serializable]
    public class SiteCoreItemNotFoundException : ApplicationException
    {
        private string itemId;
        [NonSerialized]
        private global::Sitecore.Data.ItemUri itemUri;

        public SiteCoreItemNotFoundException(string itemId, global::Sitecore.Data.ItemUri itemUri)
        {
            this.itemId = itemId;
            this.itemUri = itemUri;
        }

        public override string Message
        {
            get
            {
                return String.Format("SiteCore Item not found ({0}) {1}", itemId, itemUri);
            }
        }
    }
}
