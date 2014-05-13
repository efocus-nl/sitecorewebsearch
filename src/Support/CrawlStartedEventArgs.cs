using System;
using NCrawler;

namespace Efocus.Sitecore.LuceneWebSearch.Support
{
    [Serializable]
    public class CrawlStartedEventArgs : CrawlerEventArgs
    {

        public CrawlStartedEventArgs(Crawler crawler)
            : base(crawler)
        {
        }
    }
}