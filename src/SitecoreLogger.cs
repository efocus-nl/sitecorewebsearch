using System;
using BoC.Logging;

namespace Efocus.Sitecore.LuceneWebSearch
{
    public class SiteCoreLogger : ILogger
    {
        public SiteCoreLogger()
        {
            IsDebugEnabled = false;
            IsInfoEnabled = global::Sitecore.Diagnostics.Log.Enabled;
            IsWarnEnabled = global::Sitecore.Diagnostics.Log.Enabled;
            IsErrorEnabled = global::Sitecore.Diagnostics.Log.Enabled;
            IsFatalEnabled = global::Sitecore.Diagnostics.Log.Enabled;
        }

        public IDisposable Stack(string name)
        {
            return null;
        }

        public void Debug(string message)
        {
            global::Sitecore.Diagnostics.Log.Debug(message, this);
        }

        public void Debug(string message, Exception exception)
        {
            global::Sitecore.Diagnostics.Log.Debug(message, this);
        }

        public void DebugFormat(string format, params object[] args)
        {
            global::Sitecore.Diagnostics.Log.Debug(String.Format(format, args), this);
        }

        public void DebugFormat(Exception exception, string format, params object[] args)
        {
            global::Sitecore.Diagnostics.Log.Debug(String.Format(format, args), this);
        }

        public void DebugFormat(IFormatProvider formatProvider, string format, params object[] args)
        {
            global::Sitecore.Diagnostics.Log.Debug(String.Format(format, formatProvider, args), this);
        }

        public void DebugFormat(Exception exception, IFormatProvider formatProvider, string format, params object[] args)
        {
            global::Sitecore.Diagnostics.Log.Debug(String.Format(format, formatProvider, args), this);
        }

        public void Info(string message)
        {
            global::Sitecore.Diagnostics.Log.Info(message, this);
        }

        public void Info(string message, Exception exception)
        {
            global::Sitecore.Diagnostics.Log.Info(message, this);
        }

        public void InfoFormat(string format, params object[] args)
        {
            global::Sitecore.Diagnostics.Log.Info(String.Format(format, args), this);
        }

        public void InfoFormat(Exception exception, string format, params object[] args)
        {
            global::Sitecore.Diagnostics.Log.Info(String.Format(format, args), this);
        }

        public void InfoFormat(IFormatProvider formatProvider, string format, params object[] args)
        {
            global::Sitecore.Diagnostics.Log.Info(String.Format(format, formatProvider, args), this);
        }

        public void InfoFormat(Exception exception, IFormatProvider formatProvider, string format, params object[] args)
        {
            global::Sitecore.Diagnostics.Log.Info(String.Format(format, formatProvider, args), this);
        }

        public void Warn(string message)
        {
            global::Sitecore.Diagnostics.Log.Warn(message, this);
        }

        public void Warn(string message, Exception exception)
        {
            global::Sitecore.Diagnostics.Log.Warn(message, this);
        }

        public void WarnFormat(string format, params object[] args)
        {
            global::Sitecore.Diagnostics.Log.Warn(String.Format(format, args), this);
        }

        public void WarnFormat(Exception exception, string format, params object[] args)
        {
            global::Sitecore.Diagnostics.Log.Warn(String.Format(format, args), this);
        }

        public void WarnFormat(IFormatProvider formatProvider, string format, params object[] args)
        {
            global::Sitecore.Diagnostics.Log.Warn(String.Format(format, formatProvider, args), this);
        }

        public void Error(string message)
        {
            global::Sitecore.Diagnostics.Log.Error(message, this);
        }

        public void Error(string message, Exception exception)
        {
            global::Sitecore.Diagnostics.Log.Error(message, this);
        }

        public void ErrorFormat(string format, params object[] args)
        {
            global::Sitecore.Diagnostics.Log.Error(String.Format(format, args), this);
        }

        public void ErrorFormat(Exception exception, string format, params object[] args)
        {
            global::Sitecore.Diagnostics.Log.Error(String.Format(format, args), this);
        }

        public void ErrorFormat(IFormatProvider formatProvider, string format, params object[] args)
        {
            global::Sitecore.Diagnostics.Log.Error(String.Format(format, formatProvider, args), this);
        }

        public void ErrorFormat(Exception exception, IFormatProvider formatProvider, string format, params object[] args)
        {
            global::Sitecore.Diagnostics.Log.Error(String.Format(format, formatProvider, args), this);
        }

        public void Fatal(string message)
        {
            global::Sitecore.Diagnostics.Log.Fatal(message, this);
        }

        public void Fatal(string message, Exception exception)
        {
            global::Sitecore.Diagnostics.Log.Fatal(message, this);
        }

        public void FatalFormat(string format, params object[] args)
        {
            global::Sitecore.Diagnostics.Log.Fatal(String.Format(format, args), this);
        }

        public void FatalFormat(Exception exception, string format, params object[] args)
        {
            global::Sitecore.Diagnostics.Log.Fatal(String.Format(format, args), this);
        }

        public void FatalFormat(IFormatProvider formatProvider, string format, params object[] args)
        {
            global::Sitecore.Diagnostics.Log.Fatal(String.Format(format, formatProvider, args), this);
        }

        public void FatalFormat(Exception exception, IFormatProvider formatProvider, string format, params object[] args)
        {
            global::Sitecore.Diagnostics.Log.Fatal(String.Format(format, formatProvider, args), this);
        }

        public Type OwnerType { get; set; }
        public bool IsDebugEnabled { get; private set; }
        public bool IsInfoEnabled { get; private set; }
        public bool IsWarnEnabled { get; private set; }
        public bool IsErrorEnabled { get; private set; }
        public bool IsFatalEnabled { get; private set; }
    }
}
