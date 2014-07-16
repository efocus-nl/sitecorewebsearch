using System;
using NCrawler.Services;

namespace Efocus.Sitecore.LuceneWebSearch
{
    public class HashtagIndependentInMemoryCrawlerHistoryService : InMemoryCrawlerHistoryService
    {
        protected override void Add(string key)
        {
            key = RemoveHash(key);
            base.Add(key);
        }

        private string RemoveHash(string key)
        {
            if (!String.IsNullOrEmpty(key) && key.Contains("#"))
            {
                return key.Substring(0, key.IndexOf('#'));
            }
            return key;
        }

        protected override bool Exists(string key)
        {
            key = RemoveHash(key);
            return base.Exists(key);
        }
        public override bool Register(string key)
        {
            key = RemoveHash(key);
            return base.Register(key);
        }
    }
}