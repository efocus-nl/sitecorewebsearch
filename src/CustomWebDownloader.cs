using System;
using System.Net;
using NCrawler.Services;

namespace Efocus.Sitecore.LuceneWebSearch
{
    public class CustomWebDownloader : WebDownloaderV2
    {
        private readonly CookieContainer _cookieContainer;

        public CustomWebDownloader(CookieContainer cookieContainer = null)
        {
            _cookieContainer = cookieContainer;
        }

        protected override void SetDefaultRequestProperties(HttpWebRequest request)
        {
            base.SetDefaultRequestProperties(request);
            if (_cookieContainer != null)
            {
            request.CookieContainer = _cookieContainer;
        }
    }
}
}