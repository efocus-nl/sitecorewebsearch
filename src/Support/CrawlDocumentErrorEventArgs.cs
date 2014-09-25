using System;
using NCrawler;

namespace Efocus.Sitecore.LuceneWebSearch.Support
{
    [Serializable]
    public class CrawlDocumentErrorEventArgs : CrawlerEventArgs
    {
        private string id;
        private PropertyBag propertyBag;

        public CrawlDocumentErrorEventArgs(Crawler crawler, string id, PropertyBag propertyBag)
            : base(crawler)
        {
            this.id = id;
            this.propertyBag = propertyBag;
        }
    }
}
