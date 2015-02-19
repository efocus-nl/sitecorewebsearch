using System;
using System.Linq;
using BoC.EventAggregator;
using Efocus.Sitecore.LuceneWebSearch.Support;
using NCrawler;
using Sitecore.Events;

namespace Efocus.Sitecore.LuceneWebSearch.Events
{
    public class SiteCrawlerDocumentErrorEvent : BaseEvent
    {
        public override void Publish(params object[] arguments)
        {
            base.Publish(arguments);
            if (arguments.Count() != 3) throw new ArgumentOutOfRangeException("Wrong number of arguments specified");

            var updateCrawler = arguments[0] as Crawler;
            if (updateCrawler == null) throw new ArgumentException("Expected crawler");

            var id = arguments[1] as String;
            if (id == null) throw new ArgumentException("Expected id");

            var propertyBag = arguments[2] as PropertyBag;
            if (propertyBag == null) throw new ArgumentException("Expected PropertyBag");

            Event.RaiseEvent("SiteCrawler:DocumentError", new CrawlDocumentErrorEventArgs(updateCrawler, id, propertyBag));
        }
    }
}
