using System;
using System.Linq;
using BoC.EventAggregator;
using Efocus.Sitecore.LuceneWebSearch.Support;
using Sitecore.Events;

namespace Efocus.Sitecore.LuceneWebSearch.Events
{
    public class SiteCrawlerAnalysisDocumentEvent : BaseEvent
    {
        public override void Publish(params object[] arguments)
        {
            base.Publish(arguments);

            var c = arguments.FirstOrDefault() as CrawlDocumentAnalyseEventArgs;
            if (c == null) throw new ArgumentException("Expected CrawlDocumentAnalyseEventArgs");

            Event.RaiseEvent("SiteCrawler:DocumentAnalyse", c);
        }
    }
}
