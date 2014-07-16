using BoC.InversionOfControl;
using BoC.Logging;

namespace Efocus.Sitecore.LuceneWebSearch.Init
{
    public class InitContainer : IContainerInitializer
    {
        private readonly IDependencyResolver _resolver;

        public InitContainer(IDependencyResolver resolver)
        {
            _resolver = resolver;
        }

        public void Execute()
        {
            _resolver.RegisterType<ILogger, SiteCoreLogger>();
        }
    }
}
