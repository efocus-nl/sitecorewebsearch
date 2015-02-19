using System;
using System.Collections.Generic;
using System.Net;

namespace Efocus.Sitecore.LuceneWebSearch
{
    public class UrlCrawlerOptions
    {
        public UrlCrawlerOptions()
        {
            Cookies = new CookieCollection();
            CookieKeys = new List<string>();
        }

        public UrlCrawlerOptions(string url)
            : this()
        {
            // TODO: Complete member initialization
            this.Url = url;
        }

        public String Url { get; set; }
        public List<String> CookieKeys { get; set; }
        public CookieCollection Cookies { get; set; }
    }
}
