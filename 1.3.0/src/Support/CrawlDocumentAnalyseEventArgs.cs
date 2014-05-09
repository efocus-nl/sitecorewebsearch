using System;
using NCrawler;

namespace Efocus.Sitecore.LuceneWebSearch.Support
{
    [Serializable]
    public class CrawlDocumentAnalyseEventArgs : CrawlerEventArgs
    {
        [NonSerialized]
        private HtmlAgilityPack.HtmlDocument htmlDoc;

        public CrawlDocumentAnalyseEventArgs(Crawler crawler, HtmlAgilityPack.HtmlDocument htmlDoc)
            : base(crawler)
        {
            this.htmlDoc = htmlDoc;
            Skip = false;
        }

        public bool Skip { get; set; }
    }
}
