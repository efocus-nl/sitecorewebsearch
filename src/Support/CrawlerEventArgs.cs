using System;
using NCrawler;
using Sitecore.Events;

namespace Efocus.Sitecore.LuceneWebSearch.Support
{
    [Serializable]
    public class CrawlerEventArgs : EventArgs, IPassNativeEventArgs
    {
        [NonSerialized]
        private readonly Crawler _crawler;

        protected CrawlerEventArgs(Crawler crawler)
        {
            _crawler = crawler;
        }

        public Crawler Crawler
        {
            get { return _crawler; }
        }
    }
}
