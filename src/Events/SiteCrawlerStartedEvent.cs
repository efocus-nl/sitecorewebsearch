using System;
using System.Linq;
using BoC.EventAggregator;
using Efocus.Sitecore.LuceneWebSearch.Support;
using NCrawler;
using Sitecore.Events;

namespace Efocus.Sitecore.LuceneWebSearch.Events
{
    public class SiteCrawlerStartedEvent : BaseEvent
    {
        public override void Publish(params object[] arguments)
        {
            base.Publish(arguments);

            var c = arguments.FirstOrDefault() as Crawler;
            if (c == null) throw new ArgumentException("Expected crawler");

            Event.RaiseEvent("SiteCrawler:Started", new CrawlStartedEventArgs(c));
        }
    }
}
