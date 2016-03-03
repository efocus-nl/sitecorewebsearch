using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using BoC.InversionOfControl;
using BoC.Logging;
using Efocus.Sitecore.LuceneWebSearch.Helpers;
using Efocus.Sitecore.LuceneWebSearch.SitecoreProcessors;
using Efocus.Sitecore.LuceneWebSearch.Support;
using HtmlAgilityPack;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using NCrawler;
using NCrawler.Events;
using NCrawler.HtmlProcessor;
using NCrawler.Interfaces;
using NCrawler.Services;
using Sitecore;
using Sitecore.Configuration;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Abstractions;
using Sitecore.ContentSearch.Diagnostics;
using Sitecore.ContentSearch.LuceneProvider;
using Sitecore.ContentSearch.LuceneProvider.Converters;
using Sitecore.ContentSearch.LuceneProvider.Sharding;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Events;
using Sitecore.Extensions;
using Sitecore.Jobs;
using Sitecore.Links;
using Sitecore.Search;
using Sitecore.SecurityModel;
using BuiltinFields = Sitecore.ContentSearch.BuiltinFields;
using CrawlFinishedEventArgs = Efocus.Sitecore.LuceneWebSearch.Support.CrawlFinishedEventArgs;
using FSDirectory = Lucene.Net.Store.FSDirectory;

namespace Efocus.Sitecore.LuceneWebSearch
{
    public class NCrawlerProviderCrawler : IProviderCrawler, IPipelineStep
    {
        private bool _initialized;

        static NCrawlerProviderCrawler()
        {
            CustomNCrawlerModule.SetupCustomCrawlerModule();
        }

        private ILogger _logger;
        private SwitchOnRebuildLuceneIndex _index;
        private readonly StringCollection _urls = new StringCollection();
        //private readonly StringCollection _triggers = new StringCollection();
        private readonly StringCollection _rebuildTriggers = new StringCollection();
        private readonly Dictionary<IEnumerable<char>, IEnumerable<char>> _indexFilters = new Dictionary<IEnumerable<char>, IEnumerable<char>>();
        private readonly Dictionary<IEnumerable<char>, IEnumerable<char>> _followFilters = new Dictionary<IEnumerable<char>, IEnumerable<char>>();
        private readonly Object _runninglock = new Object();
        private Dictionary<string, string> _globalVariables;
        private bool _cancelled;
        private readonly LuceneIndexFieldStorageValueFormatter _fieldFormatter = new LuceneIndexFieldStorageValueFormatter();

        private IProviderUpdateContext _updateContext;

        private bool _isrunning = false;
        private bool _updateIndexRunning = false;
        private object _updateIndexRunningLock = new object();

        #region configurable settings

        public bool AdhereToRobotRules { get; set; }
        public bool UseCookies { get; set; }
        public int MaximumThreadCount { get; set; }
        public int MaximumCrawlDepth { get; set; }
        public int MaximumDocuments { get; set; }
        public TimeSpan MaximumCrawlTime { get; set; }
        public string RegexExcludeFilter { get; set; }
        public UriComponents UriSensitivity { get; set; }

        /// Setup filter that removed all the text between [key] and [value]
        /// This can be custom tags like <!--BeginTextFilter--> and <!--EndTextFilter-->
        /// or whatever you prefer. This way you can control what text is extracted on every page
        /// Most cases you want just to filter the header information or menu text
        public IDictionary<IEnumerable<char>, IEnumerable<char>> IndexFilters
        {
            get { return _indexFilters; }
        }
        public virtual void AddIndexFilter(XmlNode node)
        {
            if (node != null && node.Attributes != null)
            {
                var key = node.Attributes["start"];
                var keyRegex = node.Attributes["startregex"];
                var end = node.Attributes["end"];
                var endRegex = node.Attributes["endregex"];
                if ((key != null || keyRegex != null) && (end != null || endRegex != null))
                {
                    IndexFilters.Add(
                        key != null ? (IEnumerable<char>)key.Value : new RegexString(keyRegex.Value),
                        end != null ? (IEnumerable<char>)end.Value : new RegexString(endRegex.Value)
                        );
                }
            }
        }

        /// Setup filter that tells the crawler not to follow links between tags
        /// that start with [key] and ends with [value]. This can be custom tags like
        /// <!--BeginNoFollow--> and <!--EndNoFollow--> or whatever you prefer.
        /// This was you can control what links the crawler should not follow
        public IDictionary<IEnumerable<char>, IEnumerable<char>> FollowFilters
        {
            get { return _followFilters; }
        }

        public virtual void AddFollowFilter(XmlNode node)
        {
            if (node != null && node.Attributes != null)
            {
                var key = node.Attributes["start"];
                var keyRegex = node.Attributes["startregex"];
                var end = node.Attributes["end"];
                var endRegex = node.Attributes["endregex"];
                if ((key != null || keyRegex != null) && (end != null || endRegex != null))
                {
                    FollowFilters.Add(
                        key != null ? (IEnumerable<char>)key.Value : new RegexString(keyRegex.Value),
                        end != null ? (IEnumerable<char>)end.Value : new RegexString(endRegex.Value)
                        );
                }
            }
        }

        public IList Urls
        {
            get { return _urls; }
        }

        #endregion

        #region Helpers

        readonly System.Reflection.FieldInfo _writerField = typeof(IProviderUpdateContext).GetField("_writer", BindingFlags.Instance | BindingFlags.GetField | BindingFlags.NonPublic);
        private readonly HashtagIndependentInMemoryCrawlerHistoryService _historyService;
        //private DirectoryHelper _directoryHelper;

        public IndexWriter GetIndexWriter(IProviderUpdateContext updateContext)
        {
            //sigh
            return _writerField.GetValue(updateContext) as IndexWriter;
        }

        protected Dictionary<string, string> GlobalVariables
        {
            get
            {
                if (_globalVariables != null)
                    return _globalVariables;

                _globalVariables = new Dictionary<string, string>();
                foreach (var node in Factory.GetConfigNodes(".//sc.variable").Cast<XmlNode>())
                {
                    _globalVariables[node.Attributes["name"].Value] = node.Attributes["value"].Value;
                }
                return _globalVariables;
            }
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

        private void InitializeDefaultRebuildTriggers(SwitchOnRebuildLuceneIndex index)
        {
            _rebuildTriggers.Add(String.Format("efocus:rebuildindex:{0}", index.Name.ToLower()));
        }

        //private void InitializeDefaultTriggers(SwitchOnRebuildLuceneIndex index)
        //{
        //    _triggers.Add("publish:end");
        //    _triggers.Add(String.Format("efocus:updateindex:{0}", index.Name.ToLower()));
        //}

        private static StringBuilder GetExceptionLog(Exception exception)
        {
            var sb = new StringBuilder();
            sb.AppendLine(exception.Message);
            sb.AppendLine("Stacktrace");
            sb.AppendLine(exception.StackTrace);
            Action<Exception, int> traceException = null;
            traceException = (e, depth) =>
            {
                if (e == null) return;
                for (int i = 0; i < depth; i++) sb.Append("\t");

                sb.AppendLine(e.Message);
                if (e.InnerException != null)
                {
                    traceException(e.InnerException, depth + 1);
                }
            };

            traceException(exception.InnerException, 0);
            return sb;
        }

        protected IEnumerable<Uri> GetTransformedUrls()
        {
            return _urls.Cast<string>().Select(s =>
            {
                var url = s;
                if (string.IsNullOrEmpty(url)) return null;

                if (url.Contains("$("))
                {
                    url = GlobalVariables.Aggregate(url, (current, variable) => current.Replace("$(" + variable.Key + ")", variable.Value));
                }
                if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                    url = "http://" + url;

                if (Uri.IsWellFormedUriString(url, UriKind.Absolute)) return new Uri(url, UriKind.Absolute);
                if (Uri.IsWellFormedUriString(url, UriKind.Relative)) return new Uri(url, UriKind.Relative);

                return null;
            }).Where(s => s != null);
        }

        private static String CreateDepthString(int p)
        {
            var sb = new StringBuilder();
            for (var x = 0; x < p; x++)
            {
                sb.Append("-");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Evaluates if the current document must be added or if it can be skipped
        /// </summary>
        /// <param name="propertyBag"></param>
        /// <param name="updateCrawler"></param>
        /// <param name="id"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        protected bool EvaluateSkipConditions(PropertyBag propertyBag, UpdateContextAwareCrawler updateCrawler, string id, out String message)
        {
            message = String.Empty;
            var htmlDocProp = propertyBag["HtmlDoc"];
            if (htmlDocProp != null && htmlDocProp.Value != null)
            {
                var htmlDoc = htmlDocProp.Value as HtmlDocument;
                if (htmlDoc != null)
                {
                    //Raise a custom event indicating that a document is analysed
                    var args = new CrawlDocumentAnalyseEventArgs(updateCrawler, htmlDoc);
                    Event.RaiseEvent("SiteCrawler:DocumentAnalyse", args);
                    //When the Skip field is set the event handlers have indicated that this document should be skipped
                    if (args.Skip)
                    {
                        message = "CrawlDocumentAnalyse Skip = true";
                        return true;
                    }

                    if (EvaluateSkipConditions(htmlDoc, id, out message)) return true;
                }
            }
            return false;
        }

        protected bool EvaluateSkipConditions(HtmlDocument htmlDoc, string id, out string message)
        {
            message = String.Empty;
            var metaNodes = htmlDoc.DocumentNode.SelectNodes("//meta");
            if (metaNodes != null)
                foreach (var metaNode in metaNodes)
                {
                    var name = metaNode.GetAttributeValue("name", string.Empty);
                    var content = metaNode.GetAttributeValue("content", string.Empty);
                    if (("robots".Equals(name, StringComparison.InvariantCultureIgnoreCase) ||
                         "internal-index".Equals(name, StringComparison.InvariantCultureIgnoreCase))
                        && !string.IsNullOrEmpty(content) && content.ToLowerInvariant().Contains("noindex"))
                    {
                        message = String.Format("encountered meta tag {0}='{1}'", name, content);
                        return true;
                    }
                }

            var canonicalNodes = htmlDoc.DocumentNode.SelectNodes("//link[@rel='canonical']");
            var canonicalTag = canonicalNodes != null
                                   ? canonicalNodes.FirstOrDefault()
                                   : null;
            if (canonicalTag != null)
            {
                var canonicalLink = canonicalTag.GetAttributeValue("href", string.Empty);
                if (!canonicalLink.ToLower().Contains(id.ToLower()))
                {
                    message = String.Format("encountered canonical tag to '{0}'", canonicalLink);
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region Document helpers
        protected virtual Document CreateDocument(PropertyBag propertyBag, ShortID runningContextId, string documentId)
        {
            var document = new Document();
            this.AddCommonFields(document, propertyBag, runningContextId, documentId);
            this.AddContent(document, propertyBag);
            return document;
        }
        protected virtual void AddContent(Document document, PropertyBag propertyBag)
        {
            document.Add(LuceneFieldCreator.CreateTextField(BuiltinFields.Content, propertyBag.Text, _fieldFormatter));
            document.Add(LuceneFieldCreator.CreateTextField(BuiltinFields.Content, propertyBag.Title, _fieldFormatter));

            if (propertyBag["Meta"] != null)
            {
                var p = propertyBag["Meta"];
                if (p != null)
                {
                    var metaTags = p.Value as string[];
                    if (metaTags != null)
                    {
                        foreach (var metaTag in metaTags)
                        {
                            if (metaTag.StartsWith("keywords:", StringComparison.InvariantCultureIgnoreCase))
                            {
                                String keywordsValue = ValueOrEmpty(metaTag.Substring("keywords:".Length));
                                document.Add(LuceneFieldCreator.CreateTextField(BuiltinFields.Content, keywordsValue, _fieldFormatter));
                            }
                            else if (metaTag.StartsWith("description:", StringComparison.InvariantCultureIgnoreCase))
                            {
                                string description = ValueOrEmpty(metaTag.Substring("description:".Length));

                                document.Add(LuceneFieldCreator.CreateTextField(BuiltinFields.Content, description, _fieldFormatter));
                                document.Add(LuceneFieldCreator.CreateTextField(CustomFields.Description, description, _fieldFormatter));
                                document.Add(LuceneFieldCreator.CreateDataField(CustomFields.Description, description, _fieldFormatter));
                            }
                            else if (metaTag.StartsWith("efcrawler:extrafield:", StringComparison.InvariantCultureIgnoreCase))
                            {
                                //efcrawler:extrafield:templateid: value
                                var extraField = metaTag.Substring("efcrawler:extrafield:".Length);
                                extraField = extraField.Substring(0, extraField.IndexOf(':'));
                                var fulllength = "efcrawler:extrafield:".Length + extraField.Length + 2;
                                string description = ValueOrEmpty(metaTag.Substring(fulllength));

                                document.Add(LuceneFieldCreator.CreateTextField(extraField, description, _fieldFormatter));
                            }
                        }
                    }
                }
            }
            if (document.Get(CustomFields.Description) == null)
            {
                document.Add(LuceneFieldCreator.CreateDataField(CustomFields.Description, "", _fieldFormatter));
            }

            if (propertyBag.Headers.AllKeys.Any(k => k == AddHeadersHttpRequestProcessor.SitecoreItemHeaderKey))
            {
                var itemId = propertyBag.Headers[AddHeadersHttpRequestProcessor.SitecoreItemHeaderKey];
                if (!String.IsNullOrEmpty(itemId))
                {
                    var itemUri = ItemUri.Parse(itemId);
                    if (itemUri != null)
                    {
                        //Using security disabler for secured search
                        using (new SecurityDisabler())
                        {
                            var item = Database.GetItem(itemUri);
                            if (item != null)
                            {
                                AddVersionIdentifiers(item, document);
                                AddSpecialFields(item, document);
                            }
                            else
                            {
                                throw new SiteCoreItemNotFoundException(itemId, itemUri);
                            }
                        }
                    }
                }
            }
        }

        protected virtual string GetItemId(ID id, string language, string version)
        {
            Assert.ArgumentNotNull((object)id, "id");
            Assert.ArgumentNotNull((object)language, "language");
            Assert.ArgumentNotNull((object)version, "version");
            return ShortID.Encode(id) + language + "%" + version;
        }

        /// <summary>
        /// Adds the common fields.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="propertyBag"></param>
        protected virtual void AddCommonFields(Document document, PropertyBag propertyBag, ShortID runningContextId, string id)
        {
            // Here we add some useful fields:
            // Name - is is both used to search and to display the results
            // Url - is used to identify and open the file associated with the result.
            // Icon is used to display icon in the search results pane
            // Tags and Path can be used to narrow down the search but are not explicitly used in the current UI
            // Boost is used to adjust the priority of results from the filesystem relative to other locations.


            // Notice the functions used:
            //  CreateTextField(name, value) - creates a field optimized for full-text search.
            //                                 the content of the field cannot be retrieved from the index.
            //
            //  CreateValueField(name, value) - creates a field optimized for value search (such as dates, GUIDs etc).
            //                                 the content of the field cannot be retrieved from the index.
            //
            //  CreateDataField(name, value) - creates a field that will be returned in the search result.
            //                                 it is not possible to search for values in such fields.
            //
            // These functions are just helpers, and it is possible to use just Lucene.Net API here.
            document.Add(LuceneFieldCreator.CreateValueField(BuiltinFields.Path, id, _fieldFormatter));
            document.Add(LuceneFieldCreator.CreateDataField(BuiltinFields.Path, id, _fieldFormatter));

            document.Add(LuceneFieldCreator.CreateTextField(BuiltinFields.Name, ValueOrEmpty(propertyBag.Title), _fieldFormatter));
            document.Add(LuceneFieldCreator.CreateDataField(BuiltinFields.Name, ValueOrEmpty(propertyBag.Title), _fieldFormatter));
            document.Add(LuceneFieldCreator.CreateValueField(CustomFields.UpdateContextId, runningContextId.ToString().ToLower(), _fieldFormatter));
            document.Add(LuceneFieldCreator.CreateDataField(CustomFields.Depth, propertyBag.Step.Depth.ToString(CultureInfo.InvariantCulture), _fieldFormatter));
            //document.Add(LuceneFieldCreator.CreateTextField(BuiltinFields.Tags, ValueOrEmpty(Tags), _fieldFormatter)); TODO: find out about Tags
            //document.Add(LuceneFieldCreator.CreateDataField(BuiltinFields.Tags, ValueOrEmpty(Tags), _fieldFormatter)); TODO: find out about Tags
            document.Add(LuceneFieldCreator.CreateDataField(BuiltinFields.Group, ValueOrEmpty(id), _fieldFormatter));
            //document.Boost = this.Boost; TODO: find out about Boost
        }

        private string ValueOrEmpty(string p)
        {
            return p ?? "";
        }

        protected virtual void AddVersionIdentifiers(Item item, Document document)
        {
            Assert.ArgumentNotNull(item, "item");
            Assert.ArgumentNotNull(document, "document");
            document.Add(LuceneFieldCreator.CreateValueField(BuiltinFields.Database, item.Database.Name, _fieldFormatter));
            var itemId = GetItemId(item.ID, item.Language.ToString(), item.Version.ToString());
            document.Add(LuceneFieldCreator.CreateValueField(BuiltinFields.ID, itemId, _fieldFormatter));
            document.Add(LuceneFieldCreator.CreateDataField(BuiltinFields.ID, itemId, _fieldFormatter));
            document.Add(LuceneFieldCreator.CreateValueField(BuiltinFields.Language, item.Language.ToString(), _fieldFormatter));
            document.Add(LuceneFieldCreator.CreateTextField(BuiltinFields.Template, ShortID.Encode(item.TemplateID), _fieldFormatter));
            document.Add(LuceneFieldCreator.CreateDataField(BuiltinFields.Url, item.Uri.ToString(), _fieldFormatter));
        }

        protected virtual void AddSpecialFields(Item item, Document document)
        {
            if (item != null)
            {
                string displayName = item.Appearance.DisplayName;
                Assert.IsNotNull((object)displayName, "Item's display name is null.");
                document.Add(LuceneFieldCreator.CreateTextField(BuiltinFields.Name, item.Name, _fieldFormatter));
                document.Add(LuceneFieldCreator.CreateTextField(BuiltinFields.Name, displayName, _fieldFormatter));
                document.Add(LuceneFieldCreator.CreateValueField(BuiltinFields.Icon, item.Appearance.Icon, _fieldFormatter));
                document.Add(LuceneFieldCreator.CreateTextField(BuiltinFields.Creator, item.Statistics.CreatedBy, _fieldFormatter));
                document.Add(LuceneFieldCreator.CreateTextField(BuiltinFields.Editor, item.Statistics.UpdatedBy, _fieldFormatter));
                document.Add(LuceneFieldCreator.CreateTextField(BuiltinFields.AllTemplates, this.GetAllTemplates(item), _fieldFormatter));
                document.Add(LuceneFieldCreator.CreateTextField(BuiltinFields.TemplateName, item.TemplateName, _fieldFormatter));
                if (this.IsHidden(item))
                    document.Add(LuceneFieldCreator.CreateValueField(BuiltinFields.Hidden, "1", _fieldFormatter));
                document.Add(LuceneFieldCreator.CreateValueField(BuiltinFields.Created, item[FieldIDs.Created], _fieldFormatter));
                document.Add(LuceneFieldCreator.CreateValueField(BuiltinFields.Updated, item[FieldIDs.Updated], _fieldFormatter));

                foreach (var idSegment in GetItemPath(item))
                {
                    document.Add(LuceneFieldCreator.CreateTextField(BuiltinFields.Path, idSegment, _fieldFormatter));
                }

                document.Add(LuceneFieldCreator.CreateTextField(BuiltinFields.Links, this.GetItemLinks(item), _fieldFormatter));
            }
            // TODO: find out about Tags
            //if (this.Tags.Length <= 0)
            //    return;
        }

        protected string GetAllTemplates(Item item)
        {
            Assert.ArgumentNotNull((object)item, "item");
            Assert.IsNotNull((object)item.Template, "Item's template is null.");
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(ShortID.Encode(item.TemplateID));
            stringBuilder.Append(" ");
            foreach (TemplateItem templateItem in item.Template.BaseTemplates)
            {
                stringBuilder.Append(ShortID.Encode(templateItem.ID));
                stringBuilder.Append(" ");
            }
            return ((object)stringBuilder).ToString();
        }

        private bool IsHidden(Item item)
        {
            Assert.ArgumentNotNull((object)item, "item");
            if (item.Appearance.Hidden)
                return true;
            if (item.Parent != null)
                return IsHidden(item.Parent);
            else
                return false;
        }

        protected string GetItemLinks(Item item)
        {
            Assert.ArgumentNotNull((object)item, "item");
            StringBuilder stringBuilder = new StringBuilder();
            foreach (ItemLink itemLink in item.Links.GetAllLinks(false))
            {
                stringBuilder.Append(" ");
                stringBuilder.Append(ShortID.Encode(itemLink.TargetItemID));
            }
            return ((object)stringBuilder).ToString();
        }

        protected IEnumerable<string> GetItemPath(Item item)
        {
            Assert.ArgumentNotNull((object)item, "item");
            return item.Paths.LongID.Split('/').Select(idSegment => new Regex("[{}-]", RegexOptions.Compiled).Replace(idSegment, string.Empty));
        }
        #endregion

        #region Event helpers

        private void HandleRebuildIndexEvent(object sender, EventArgs eventArgs)
        {
            if (_updateContext == null)
                _updateContext = new LuceneFullRebuildContext(_index, new CommitPolicyExecutor());

            //JobManager.Start(new JobOptions(string.Format("efcrawler:rebuild:{0}", _updateContext.Index.Name), "Indexing", _updateContext.Index.Name, this, "UpdateIndex", new object[] { _updateContext as LuceneFullRebuildContext, true}));
            RebuildFromRoot(_updateContext as LuceneFullRebuildContext);
        }

        private void HandleUpdateIndexEvent(object sender, EventArgs eventArgs)
        {
            UpdateIndex(_updateContext as LuceneUpdateContext);
            RebuildAutoCompleteIndex();
        }
        #endregion

        #region Crawler exception handlers
        private void CrawlerCancelled(object sender, EventArgs eventArgs)
        {
            this._cancelled = true;
            if (_logger != null) _logger.Info("Crawler cancelled, removing backupdir");
            //var dir = _directoryHelper.GetDirectoryName(_index);
            //if (_directoryHelper.RestoreDirectoryBackup(dir))
            //{
            //    _directoryHelper.DeleteBackupDirectory(dir);
            //}
        }

        private void CrawlerAfterDownload(object sender, AfterDownloadEventArgs e)
        {
            Uri uriToCrawl = null;
            String responseTime = "unknown";
            if (e.Response != null)
            {
                uriToCrawl = e.Response.ResponseUri;
                responseTime = e.Response.DownloadTime.TotalSeconds.ToString();
            }
            if (uriToCrawl == null)
            {
                uriToCrawl = e.CrawlStep.Uri;
            }

            if (_logger != null) _logger.InfoFormat("{0} in {1}", uriToCrawl, responseTime);
        }

        private void CrawlerDownloadException(object sender, DownloadExceptionEventArgs e)
        {
            if (e.Exception is WebException)
            {
                WebException webException = (WebException)e.Exception;
                _logger.InfoFormat("Error downloading '{0}': {1}; {2} - Continueing crawl", e.CrawlStep.Uri, webException.Status, webException.Source);
            }
            else
            {
                _logger.InfoFormat("Error downloading '{0}': {1} - Continueing crawl", e.CrawlStep.Uri, e.Exception.Message);
            }
        }

        private void CrawlerPipelineException(object sender, PipelineExceptionEventArgs e)
        {
            Uri currentUri = ExtractCurrentUri(e.PropertyBag);
            _logger.InfoFormat("Error processsing '{0}': {1}", currentUri, e.Exception.Message);
        }

        private Uri ExtractCurrentUri(PropertyBag propertyBag)
        {
            if (propertyBag.ResponseUri != null) return propertyBag.ResponseUri;
            return propertyBag.Step.Uri;
        }
        #endregion

        public void Initialize(ISearchIndex index)
        {
            //both config hint="list:AddCrawler" and LuceneIndex.Initialize call this method so check if we have already initialized
            if (_initialized) return;

            _initialized = true;
            _logger = CreateLogger();
            _index = index as SwitchOnRebuildLuceneIndex;

            //if (_triggers.Count == 0) InitializeDefaultTriggers(_index);
            if (_rebuildTriggers.Count == 0) InitializeDefaultRebuildTriggers(_index);
            foreach (var trigger in _rebuildTriggers)
            {
                Event.Subscribe(trigger, HandleRebuildIndexEvent);
            }

            if (_index != null) _logger.Info(string.Format("Crawler initialized: {0}", _index.Name));
        }

        public bool HasItemsToIndex()
        {
            throw new NotImplementedException();
        }

        public bool IsExcludedFromIndex(IIndexable indexable)
        {
            if (indexable.DataSource.Equals("Sitecore"))
                return true;
            return false;
        }

        public virtual void RebuildFromRoot(IProviderUpdateContext context, IndexingOptions indexingOptions = IndexingOptions.Default)
        {
            Assert.ArgumentNotNull((object)context, "context");
            _updateContext = context;

            UpdateIndex(_updateContext as LuceneFullRebuildContext, true);
            RebuildAutoCompleteIndex();
        }

        public void RebuildFromRoot(IProviderUpdateContext context, IndexingOptions indexingOptions, CancellationToken cancellationToken)
        {
            this.RebuildFromRoot(context, indexingOptions);
        }

        public void RefreshFromRoot(IProviderUpdateContext context, IIndexable indexableStartingPoint, IndexingOptions indexingOptions)
        {
            throw new NotImplementedException();
        }

        public void RefreshFromRoot(IProviderUpdateContext context, IIndexable indexableStartingPoint, IndexingOptions indexingOptions,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public virtual void Update(IProviderUpdateContext context, IIndexableUniqueId indexableUniqueId, IndexingOptions indexingOptions = IndexingOptions.Default)
        {
            using (new SecurityDisabler())
            {
                context.Index.Locator.GetInstance<IEvent>().RaiseEvent(String.Format("efocus:updateindex:{0}", _index.Name.ToLower()));
                context.Index.Locator.GetInstance<IEvent>().RaiseEvent(String.Format("efocus:updateindex:{0}:remote", _index.Name.ToLower()));
            }
        }

        public void Delete(IProviderUpdateContext context, IIndexableId indexableId, IndexingOptions indexingOptions = IndexingOptions.Default)
        {
            throw new NotImplementedException();
        }

        public void Delete(IProviderUpdateContext context, IIndexableUniqueId indexableUniqueId, IndexingOptions indexingOptions = IndexingOptions.Default)
        {
            throw new NotImplementedException();
        }

        public void StopIndexing()
        {
            CrawlingLog.Log.Warn(string.Format("[Index={0}] Stop Indexing Requested - Stopping, Pausing and Resuming is not supported by NCrawlerProviderCrawler", (object)this._index.Name), (Exception)null);
        }

        public void PauseIndexing()
        {
            CrawlingLog.Log.Warn(string.Format("[Index={0}] Pause Indexing Requested - Stopping, Pausing and Resuming is not supported by NCrawlerProviderCrawler", (object)this._index.Name), (Exception)null);
        }

        public void ResumeIndexing()
        {
            CrawlingLog.Log.Warn(string.Format("[Index={0}] Resume Indexing Requested - Stopping, Pausing and Resuming is not supported by NCrawlerProviderCrawler", (object)this._index.Name), (Exception)null);
        }

        private void RebuildAutoCompleteIndex()
        {
            //Rebuild AutoComplete index

            _logger.Info(string.Format("Creating autocomplete index for {0}", _index.Name));

            foreach (LuceneSwitchOnRebuildShard shard in _index.Shards)
            {
                string dir = shard.FullRebuildDirectory.GetPath().Split(new[] { _index.Name }, StringSplitOptions.None)[0] + _index.Name;
                AutoCompleteService autoCompleteService = new AutoCompleteService(dir.Substring(0, dir.IndexOf("__", System.StringComparison.Ordinal)) + "__" + _index.Name + "_autocomplete");
                autoCompleteService.BuildAutoCompleteIndex(FSDirectory.Open(dir), "_name");
            }

            _logger.Info(string.Format("Done creating autocomplete index for {0}", _index.Name));
        }

        private void UpdateIndex(LuceneUpdateContext updateContext, bool isrebuild = false)
        {
            if (_updateIndexRunning)
                return;
            lock (_updateIndexRunningLock)
            {
                try
                {
                    _updateIndexRunning = true;

                    Crawl(updateContext, isrebuild);

                    _logger.Info(string.Format("Search index {0} crawling done", _index.Name));
                }
                catch (Exception ex)
                {
                    if (_logger != null) _logger.Error(GetExceptionLog(ex).ToString());
                }
                finally
                {
                    _updateIndexRunning = false;
                }
            }
        }

        private void Crawl(LuceneUpdateContext context, bool isrebuild = false)
        {
            var correctedContext = context;
            if (context is LuceneFullRebuildContext)
            {
                correctedContext = context as LuceneFullRebuildContext;
            }
            if (_isrunning)
            {
                _logger.InfoFormat("Crawler is already running, aborting");
                return;
            }

            lock (_runninglock)
            {
                _isrunning = true;

                //_directoryHelper = new DirectoryHelper(_logger);
                //var dir = _directoryHelper.GetDirectoryName(_index);

                _cancelled = false;
                try
                {
                    //_directoryHelper.CreateDirectoryBackup(dir);

                    var runningContextId = ShortID.NewId();
                    var urls = GetTransformedUrls().ToList();
                    if (_logger != null)
                    {
                        urls.ForEach(url => _logger.InfoFormat("Starting url: {0}", url));
                    }

                    var documentProcessor = (_logger != null && _logger.IsDebugEnabled)
                        ? new LogHtmlDocumentProcessor(_logger, _indexFilters, _followFilters)
                        : new HtmlDocumentProcessor(_indexFilters, _followFilters);

                    using (
                        var c = new UpdateContextAwareCrawler(correctedContext, runningContextId, urls,
                            new LogLoggerBridge(_logger), documentProcessor, this))
                    {
                        if (_logger != null) _logger.Info(String.Format("Crawler started: Using {0} threads", MaximumThreadCount));

                        c.AdhereToRobotRules = AdhereToRobotRules;
                        c.MaximumThreadCount = MaximumThreadCount;
                        c.UriSensitivity = UriSensitivity;

                        if (MaximumCrawlDepth > 0)
                            c.MaximumCrawlDepth = MaximumCrawlDepth;

                        if (MaximumDocuments > 0)
                            c.MaximumCrawlCount = MaximumDocuments;

                        if (MaximumCrawlTime.TotalMinutes > 0)
                            c.MaximumCrawlTime = MaximumCrawlTime;

                        c.UseCookies = UseCookies;
                        c.ExcludeFilter = new[] { new RegexFilter(new Regex(RegexExcludeFilter)) };

                        c.AfterDownload += CrawlerAfterDownload;
                        c.PipelineException += CrawlerPipelineException;
                        c.DownloadException += CrawlerDownloadException;
                        c.Cancelled += CrawlerCancelled;

                        Event.RaiseEvent("SiteCrawler:Started", new CrawlStartedEventArgs(c));

                        c.Crawl();

                        Event.RaiseEvent("SiteCrawler:Finished", new CrawlFinishedEventArgs(c));
                    }
                }
                catch (Exception crawlException)
                {
                    if (_logger != null) _logger.Error(GetExceptionLog(crawlException).ToString());
                    //if (_directoryHelper.RestoreDirectoryBackup(dir))
                    //{
                    //    _cancelled = false;
                    //}
                }
                finally
                {
                    if (_logger != null) _logger.Info("Crawler finished");
                    _isrunning = false;
                    //if (!_cancelled)
                    //    _directoryHelper.DeleteBackupDirectory(dir);
                }
            }
        }

        //IPipelineStep.Process
        public void Process(Crawler crawler, PropertyBag propertyBag)
        {
            try
            {
                var updateCrawler = crawler as UpdateContextAwareCrawler;
                if (updateCrawler == null)
                {
                    if (_logger != null)
                        _logger.Info("Crawler is not an UpdateContextAwareCrawler, we can't deal with this crawler");
                    return;
                }

                Uri currentUri = ExtractCurrentUri(propertyBag);

                string id = currentUri.PathAndQuery;
                String depthString = CreateDepthString(propertyBag.Step.Depth);

                if (_logger != null)
                    _logger.InfoFormat("{0} | Process | HTTP-{1} | {2}", depthString, propertyBag.StatusCode, id);

                lock (updateCrawler.UpdateContext)
                {
                    //GetIndexWriter(updateCrawler.UpdateContext).DeleteDocuments(new Term(BuiltinFields.Path, id)); TODO: this must work in order for the index update function to work!!!
                    if (propertyBag.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        //this should have been done by NCrawler, but let's do it here... could move this to seperate crawlerrulesservice class, but then we'd have to download the content again
                        String message;
                        if (crawler.AdhereToRobotRules && EvaluateSkipConditions(propertyBag, updateCrawler, id, out message))
                        {
                            if (_logger != null) _logger.Info(String.Format("{0} | Skipped | {1} | {2}", depthString, message, id));
                            return;
                        }

                        var document = CreateDocument(propertyBag, updateCrawler.RunningContextId, id);
                        updateCrawler.UpdateContext.AddDocument(document);

                        //Raise event that the givven document is updated
                        Event.RaiseEvent("SiteCrawler:DocumentUpdated", new CrawlDocumentUpdatedEventArgs(updateCrawler, document));

                        if (_logger != null) _logger.InfoFormat("{0} | Add/Update | {1}", depthString, id);

                        var job = JobManager.GetJob(string.Format("Index_Update_IndexName={0}", _index.Name)) ?? Context.Job;
                        if (job != null) job.Status.Processed++;
                    }
                    else if (propertyBag.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        if (_logger != null) _logger.InfoFormat("Crawler encoutered 404 for [{0}]", id);
                        //Raise an event that the Document was not found 
                        Event.RaiseEvent("SiteCrawler:DocumentNotFound",
                                         new CrawlDocumentErrorEventArgs(updateCrawler, id, propertyBag));
                    }
                    else if (propertyBag.StatusCode == HttpStatusCode.ServiceUnavailable)
                    {
                        if (_logger != null)
                            _logger.WarnFormat(
                                "Crawler encountered status {0} ({1}) for document {2}, ABORTING CRAWLER!!",
                                propertyBag.StatusCode.ToString(), propertyBag.StatusDescription, id);
                        Event.RaiseEvent("SiteCrawler:DocumentError", new CrawlDocumentErrorEventArgs(updateCrawler, id, propertyBag));
                        //server is shutting down or is too busy, abort the indexing!!
                        crawler.Cancel();
                    }
                    else
                    {
                        if (_logger != null)
                            _logger.WarnFormat("Crawler encountered status {0} ({1}) for document {2}",
                                               propertyBag.StatusCode.ToString(), propertyBag.StatusDescription, id);
                        //Raise an event that the document request returned an error
                        Event.RaiseEvent("SiteCrawler:DocumentError", new CrawlDocumentErrorEventArgs(updateCrawler, id, propertyBag));
                        if (propertyBag.Step.Depth == 0)
                        {
                            if (_logger != null)
                                _logger.Warn("ABORTING CRAWLER DUE TO ERROR ON FIRST REQUEST");
                            crawler.Cancel();
                        }
                    }
                }
            }
            catch (Exception crawlExeption)
            {
                if (_logger != null) _logger.Error(GetExceptionLog(crawlExeption).ToString());

                throw;
            }
        }
    }
}
