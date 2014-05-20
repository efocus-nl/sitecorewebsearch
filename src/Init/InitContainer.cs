using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BoC.InversionOfControl;
using BoC.Logging;

namespace Efocus.Sitecore.LuceneWebSearch.Init
{
    public class InitContainer: IContainerInitializer
    {
        private readonly IDependencyResolver _resolver;

        public InitContainer(IDependencyResolver resolver)
        {
            _resolver = resolver;
        }

        public void Execute()
        {
            _resolver.RegisterType<ILogger,SiteCoreLogger>();
        }
    }
}
