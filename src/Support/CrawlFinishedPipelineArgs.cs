using System;
using NCrawler;

namespace Efocus.Sitecore.LuceneWebSearch.Support
{
    [Serializable]
    public class CrawlFinishedEventArgs : CrawlerEventArgs
    {
        public CrawlFinishedEventArgs(Crawler crawler)
            : base(crawler)
        {
        }
    }
}
