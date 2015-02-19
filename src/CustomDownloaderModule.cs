using System;
using System.Collections.Generic;
using System.Net;
using Autofac.Core;
using BoC.InversionOfControl;
using BoC.Logging;
using Efocus.Sitecore.LuceneWebSearch.Support;
using NCrawler;
using Autofac;
using NCrawler.Interfaces;
using NCrawler.Services;

namespace Efocus.Sitecore.LuceneWebSearch
{
    public class CustomNCrawlerModule : NCrawlerModule
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.Register(c => new CustomWebDownloader()).As<IWebDownloader>().ExternallyOwned();
            builder.Register(c => new HashtagIndependentInMemoryCrawlerHistoryService()).As<ICrawlerHistory>().InstancePerDependency();
            builder.Register(c => new InMemoryCrawlerQueueService()).As<ICrawlerQueue>().InstancePerDependency();
            builder.Register(c => new LogLoggerBridge(CreateLogger())).As<ILog>().InstancePerDependency();
            builder.Register(c => new NativeTaskRunnerService()).As<ITaskRunner>().InstancePerDependency();
            builder.Register((c, p) => new RobotService(ParameterExtensions.TypedAs<IEnumerable<Uri>>(p), ResolutionExtensions.Resolve<IWebDownloader>(c))).As<IRobot>().InstancePerDependency();
            builder.Register((c, p) => new CrawlerRulesService(ParameterExtensions.TypedAs<Crawler>(p), ResolutionExtensions.Resolve<IRobot>(c, p), ParameterExtensions.TypedAs<IEnumerable<Uri>>(p))).As<ICrawlerRules>().InstancePerDependency();
        }

        protected virtual ILogger CreateLogger()
        {
            ILogger logger = IoC.Resolver != null ? IoC.Resolver.Resolve<ILogger>() : null;
            if (logger == null)
            {
                logger = new SiteCoreLogger();
            }

            return logger;
        }

        public static void SetupCustomCrawlerModule()
        {
            NCrawlerModule.Setup(new Module[1]
        {
            new CustomNCrawlerModule()
          });
        }
    }
}