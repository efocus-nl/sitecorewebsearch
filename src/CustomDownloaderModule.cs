using System;
using System.Net;
using NCrawler;
using Autofac;
using NCrawler.Interfaces;
using NCrawler.Services;

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
            builder.Register(c => new CustomWebDownloader(_cookieContainer))
                   .As<IWebDownloader>()
                   .SingleInstance()
                   .ExternallyOwned();
            builder.Register(c => new InMemoryCrawlerHistoryService()).As<ICrawlerHistory>().InstancePerDependency();
            builder.Register(c => new InMemoryCrawlerQueueService()).As<ICrawlerQueue>().InstancePerDependency();
            builder.Register(c => new SystemTraceLoggerService()).As<ILog>().InstancePerDependency();
#if !DOTNET4
            builder.Register(c => new ThreadTaskRunnerService()).As<ITaskRunner>().InstancePerDependency();
#else
			builder.Register(c => new NativeTaskRunnerService()).As<ITaskRunner>().InstancePerDependency();
#endif
            builder.Register((c, p) => new RobotService(p.TypedAs<Uri>(), c.Resolve<IWebDownloader>())).As<IRobot>().InstancePerDependency();
            builder.Register((c, p) => new CrawlerRulesService(p.TypedAs<Crawler>(), c.Resolve<IRobot>(p), p.TypedAs<Uri>())).As<ICrawlerRules>().InstancePerDependency();

        }

        public static void Setup(CookieContainer cookieContainer)
        {
            Setup(cookieContainer);
        }
    }
}