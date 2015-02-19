using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using NCrawler.Services;

namespace Efocus.Sitecore.LuceneWebSearch
{
    public class CustomWebDownloader : WebDownloaderV2, IWebDownloaderWithCookies
    {
        private readonly CookieContainer _cookieContainer;

        public CustomWebDownloader(CookieContainer cookieContainer = null)
        {
            _cookieContainer = cookieContainer;
        }

        public Uri DefaultDomain
        {
            get;
            set;
        }

        public List<String> Keys { get; set; }

        public IEnumerable<Cookie> KeyCookies
        {
            get
            {
                return Cookies.Cast<Cookie>().Where(cookie => Keys.Contains(cookie.Name));
            }
        }

        public CookieCollection Cookies
        {
            get;
            set;
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