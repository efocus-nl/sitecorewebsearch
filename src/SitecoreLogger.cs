using BoC.Logging;
using NCrawler.Interfaces;

namespace Efocus.Sitecore.LuceneWebSearch
{
    public class SitecoreLogger : ILog
    {
        private readonly ILogger _logger;

        public SitecoreLogger(ILogger logger)
        {
            _logger = logger;
        }

        public void Verbose(string format, params object[] parameters)
        {
            _logger.InfoFormat(format, parameters);
        }

        public void Warning(string format, params object[] parameters)
        {
            _logger.WarnFormat(format, parameters);
        }

        public void Debug(string format, params object[] parameters)
        {
            _logger.DebugFormat(format, parameters);
        }

        public void Error(string format, params object[] parameters)
        {
            _logger.ErrorFormat(format, parameters);
        }

        public void FatalError(string format, params object[] parameters)
        {
            _logger.FatalFormat(format, parameters);
        }
    }
}
