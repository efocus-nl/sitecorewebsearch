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
            RegistrationExtensions.Register(builder, c => new CustomWebDownloader()).As<IWebDownloader>().ExternallyOwned();
            RegistrationExtensions.Register(builder, c => new HashtagIndependentInMemoryCrawlerHistoryService()).As<ICrawlerHistory>().InstancePerDependency();
            RegistrationExtensions.Register(builder, c => new InMemoryCrawlerQueueService()).As<ICrawlerQueue>().InstancePerDependency();
            RegistrationExtensions.Register(builder, c => new LogLoggerBridge(CreateLogger())).As<ILog>().InstancePerDependency();
            RegistrationExtensions.Register(builder, c => new NativeTaskRunnerService()).As<ITaskRunner>().InstancePerDependency();
            RegistrationExtensions.Register(builder, (c, p) => new RobotService(ParameterExtensions.TypedAs<Uri>(p), ResolutionExtensions.Resolve<IWebDownloader>(c))).As<IRobot>().InstancePerDependency();
            RegistrationExtensions.Register(builder, (c, p) => new CrawlerRulesService(ParameterExtensions.TypedAs<Crawler>(p), ResolutionExtensions.Resolve<IRobot>(c, p), ParameterExtensions.TypedAs<Uri>(p))).As<ICrawlerRules>().InstancePerDependency();
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