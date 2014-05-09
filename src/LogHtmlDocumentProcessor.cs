using System.Collections.Generic;
using BoC.Logging;

namespace Efocus.Sitecore.LuceneWebSearch
{
    class LogHtmlDocumentProcessor : HtmlDocumentProcessor
    {
        private readonly ILogger _logger;

        public LogHtmlDocumentProcessor()
        {
        }

        public LogHtmlDocumentProcessor(ILogger logger, Dictionary<IEnumerable<char>, IEnumerable<char>> filterTextRules,
            Dictionary<IEnumerable<char>, IEnumerable<char>> filterLinksRules)
            : base(filterTextRules, filterLinksRules)
        {
            _logger = logger;
        }

        protected override void AddStepToCrawler(NCrawler.Crawler crawler, NCrawler.PropertyBag propertyBag, string normalizedLink, string link)
        {
            if (_logger != null) _logger.DebugFormat("Crawler:AddStepToCrawler | {0}", normalizedLink);

            base.AddStepToCrawler(crawler, propertyBag, normalizedLink, link);
        }
    }
}
