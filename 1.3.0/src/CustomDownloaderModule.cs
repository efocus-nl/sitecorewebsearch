using System.Net;
using NCrawler;
using Autofac;
using NCrawler.Interfaces;

namespace Efocus.Sitecore.LuceneWebSearch
{
    public class CustomDownloaderModule : NCrawlerModule
    {
        private readonly CookieContainer _cookieContainer;

        public CustomDownloaderModule(CookieContainer cookieContainer)
        {
            _cookieContainer = cookieContainer;
        }

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.Register(c => new CustomWebDownloader(_cookieContainer))
                   .As<IWebDownloader>()
                   .SingleInstance()
                   .ExternallyOwned();
        }

        public static void Setup(CookieContainer cookieContainer)
        {
            Setup(cookieContainer);
        }
    }
}