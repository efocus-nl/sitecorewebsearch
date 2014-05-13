using System;
using NCrawler;
using Sitecore.Pipelines;

namespace Efocus.Sitecore.LuceneWebSearch.Support
{
    [Serializable]
    public class CrawlDocumentUpdatedEventArgs : CrawlerEventArgs
    {
        private readonly Lucene.Net.Documents.Document _document;

        public CrawlDocumentUpdatedEventArgs(Crawler crawler, Lucene.Net.Documents.Document document)
            : base(crawler)
        {
            _document = document;
        }
    }
}
