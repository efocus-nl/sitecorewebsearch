using System;
using System.Linq;
using BoC.EventAggregator;
using Efocus.Sitecore.LuceneWebSearch.Support;
using Lucene.Net.Documents;
using NCrawler;
using Sitecore.Events;

namespace Efocus.Sitecore.LuceneWebSearch.Events
{
    public class SiteCrawlerDocumentUpdatedEvent : BaseEvent
    {
        public override void Publish(params object[] arguments)
        {
            base.Publish(arguments);

            var updateCrawler = arguments.FirstOrDefault() as Crawler;
            if (updateCrawler == null) throw new ArgumentException("Expected crawler");

            var document = arguments.LastOrDefault() as Document;
            if (document == null) throw new ArgumentException("Expected document");

            //Raise event that the givven document is updated
            Event.RaiseEvent("SiteCrawler:DocumentUpdated", new CrawlDocumentUpdatedEventArgs(updateCrawler, document));
        }
    }
}
