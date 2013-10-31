using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using Autofac;
using BoC.InversionOfControl;
using BoC.Logging;
using Efocus.Sitecore.LuceneWebSearch.SitecoreProcessors;
using HtmlAgilityPack;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using NCrawler;
using NCrawler.HtmlProcessor;
using NCrawler.Interfaces;
using NCrawler.Services;
using Sitecore;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Events;
using Sitecore.Links;
using Sitecore.Search;
using Sitecore.Search.Crawlers;
using Sitecore.SecurityModel;
using Sitecore.Events;
using Sitecore.Sites;
using Sitecore.Web;
using Module = Autofac.Module;

namespace Efocus.Sitecore.LuceneWebSearch
{
    public static class CustomFields
    {
        public const string Depth = "_crawldepth";
        public const string UpdateContextId = "_updatecontextid";
        public const string Description = "_description";
    }

    public class HashtagIndependentInMemoryCrawlerHistoryService : InMemoryCrawlerHistoryService
    {
        protected override void Add(string key)
        {
            key = (key ?? "").TrimEnd('#');
            base.Add(key);
        }
        protected override bool Exists(string key)
        {
            key = (key ?? "").TrimEnd('#');
            return base.Exists(key);
        }
        public override bool Register(string key)
        {
            key = (key ?? "").TrimEnd('#');
            return base.Register(key);
        }
    }

    public class SiteCrawler : BaseCrawler, ICrawler, IPipelineStep
    {
        //private IUrlProvider _urlProvider;
        private ILogger _logger;
        private Index _index;
        private readonly StringCollection _urls = new StringCollection();
        private readonly Dictionary<string, string> _indexFilters = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _followFilters = new Dictionary<string, string>();
        private readonly Object _runninglock = new Object();
        private Dictionary<string, string> _globalVariables;

        private bool _isrunning = false;
        private bool _updateIndexRunning = false;
        private object _updateIndexRunningLock = new object();
        private Item _indexTaskConfiguration;

#region configurable settings
        public bool AdhereToRobotRules { get; set; }
        public bool UseCookies { get; set; }
        public int MaximumThreadCount { get; set; }
        public string RegexExcludeFilter { get; set; }
        public string EventTrigger { get; set; }
        public UriComponents UriSensitivity { get; set; }

        /// Setup filter that removed all the text between [key] and [value]
        /// This can be custom tags like <!--BeginTextFilter--> and <!--EndTextFilter-->
        /// or whatever you prefer. This way you can control what text is extracted on every page
        /// Most cases you want just to filter the header information or menu text
        public IDictionary<string, string> IndexFilters
        {
            get { return _indexFilters; }
        }
        public virtual void AddIndexFilter(XmlNode node)
        {
            if (node != null && node.Attributes != null)
            {
                var key = node.Attributes["start"];
                var end = node.Attributes["end"];
                if (key != null && end != null)
                    IndexFilters.Add(key.Value, end.Value);
            }
        }

        /// Setup filter that tells the crawler not to follow links between tags
        /// that start with [key] and ends with [value]. This can be custom tags like
        /// <!--BeginNoFollow--> and <!--EndNoFollow--> or whatever you prefer.
        /// This was you can control what links the crawler should not follow
        public IDictionary<string, string> FollowFilters
        {
            get { return _followFilters; }
        }

        public virtual void AddFollowFilter(XmlNode node)
        {
            if (node != null && node.Attributes != null)
            {
                var key = node.Attributes["start"];
                var end = node.Attributes["end"];
                if (key != null && end != null)
                    FollowFilters.Add(key.Value, end.Value);
            }
        }

        public IList Urls
        {
            get { return _urls; }
        }
#endregion
        
        public SiteCrawler()
        {
            AdhereToRobotRules = true;
            UseCookies = true;
            MaximumThreadCount = 2;
            RegexExcludeFilter = @"(\.jpg|\.css|\.js|\.gif|\.jpeg|\.png|\.ico)";
            UriSensitivity = UriComponents.UserInfo;

            NCrawlerModule.Register(builder =>
                {
                    builder.Register((c, p) =>
                        {
                            NCrawlerModule.Setup(); // Return to standard setup
                            return new HashtagIndependentInMemoryCrawlerHistoryService();
                        }).As<ICrawlerHistory>().InstancePerDependency();

                    builder.Register(c => new SitecoreLogger(IoC.Resolver.Resolve<ILogger>()))
                           .As<ILog>().InstancePerDependency();
                }
            );
        }

        public void Initialize(Index index)
        {
            //_urlProvider = IoC.Resolver.Resolve<IUrlProvider>();
            _logger = IoC.Resolver.Resolve<ILogger>();
            _index = index;
            _logger.Info("Crawler initialized");

            var updateHandler = new EventHandler(UpdateIndex);
            Event.Subscribe("efocus:updateindex:" + index.Name.ToLower(), updateHandler);
            //Event.Subscribe("efocus:updateindex:" + index.Name.ToLower() + ":remote", (sender, args) => UpdateIndex());
            var rebuildHandler = new EventHandler(RebuildIndex);
            Event.Subscribe("efocus:rebuildindex:" + index.Name.ToLower() + ":remote", rebuildHandler);
        }

        protected virtual void RebuildIndex(object sender, EventArgs args)
        {
            var customArgs = Event.ExtractParameter(args, 0) as CustomEventArgs;

            if (customArgs != null && customArgs.Item != null)
                _indexTaskConfiguration = customArgs.Item;
            else
            {
                _logger.InfoFormat("IndexTaskConfiguration item could not be found, aborting");
                return;
            }

            if (string.IsNullOrEmpty(_indexTaskConfiguration["Index"]))
            {
                _logger.InfoFormat("Index is not defined, aborting");
                return;
            }

            var index = SearchManager.GetIndex(_indexTaskConfiguration["Index"]);
            index.Rebuild();
        }

        protected virtual void UpdateIndex(object sender, EventArgs args)
        {
            var customArgs = Event.ExtractParameter(args, 0) as CustomEventArgs;

            if (customArgs != null && customArgs.Item != null)
                _indexTaskConfiguration = customArgs.Item;
            else
            {
                _logger.InfoFormat("IndexTaskConfiguration item could not be found, aborting");
                return;
            }

            if (string.IsNullOrEmpty(_indexTaskConfiguration["Index"]))
            {
                _logger.InfoFormat("Index is not defined, aborting");
                return;
            }

            if (_updateIndexRunning)
                return;
            lock (_updateIndexRunningLock)
            {
                if (_updateIndexRunning)
                    return;
                _updateIndexRunning = true;
                try
                {
                    using (var updateContext = this._index.CreateUpdateContext())
                    {
                        Crawl(updateContext);
                        updateContext.Optimize();
                        updateContext.Commit();
                    }
                }
                finally
                {
                    _updateIndexRunning = false;
                }
            }
        }

        private void Crawl(IndexUpdateContext context)
        {
            lock (_runninglock)
            {
                if (_isrunning)
                {
                    _logger.InfoFormat("Crawler is already running, aborting");
                    return;
                }
                _isrunning = true;
            }
            try
            {
                //now, we're going to delete everything not added in this crawl (deleting it during an update instead of resetting/rebuilding the index keeps the index alive for searching while indexing)
                GetIndexWriter(context).DeleteAll();

                var runningContextId = ShortID.NewId();
                var urls = GetTransformedUrls();
                foreach (var url in urls)
                {
                    _logger.InfoFormat("Starting url: {0}", url);
                    ID loginPageId;

                    //Let the crawler login first, if the required values are available
                    if (!string.IsNullOrEmpty(_indexTaskConfiguration["UserFieldName"]) && !string.IsNullOrEmpty(_indexTaskConfiguration["User"]) &&
                        !string.IsNullOrEmpty(_indexTaskConfiguration["PassFieldName"]) && !string.IsNullOrEmpty(_indexTaskConfiguration["Pass"]) &&
                        !string.IsNullOrEmpty(_indexTaskConfiguration["LoginPage"]) && ID.TryParse(_indexTaskConfiguration["LoginPage"], out loginPageId) &&
                        _indexTaskConfiguration.Database.GetItem(loginPageId) != null)
                    {
                        var postData = new NameValueCollection
                        {
                            {_indexTaskConfiguration["UserFieldName"], _indexTaskConfiguration["User"]},
                            {_indexTaskConfiguration["PassFieldName"], _indexTaskConfiguration["Pass"]},
                        };

                        Item loginPage = _indexTaskConfiguration.Database.GetItem(loginPageId);
                        SiteInfo loginSiteInfo = null;
                        if (Factory.GetSiteInfoList().Any(x => loginPage.Paths.FullPath.StartsWith(x.RootPath)))
                            loginSiteInfo = Factory.GetSiteInfoList().OrderByDescending(x => x.RootPath.Count(f => f == '/')).First(x => loginPage.Paths.FullPath.StartsWith(x.RootPath));

                        SiteContext loginSiteContex = null;
                        if (loginSiteInfo != null)
                            loginSiteContex = Factory.GetSite(loginSiteInfo.Name);

                        CookieContainer authorizedCookies = null;
                        if(loginSiteContex != null)
                            authorizedCookies = GetAuthorizationCookie(new Uri(LinkManager.GetItemUrl(loginPage, new UrlOptions() { Site = loginSiteContex })), postData);
                        else
                            authorizedCookies = GetAuthorizationCookie(new Uri(LinkManager.GetItemUrl(loginPage)), postData);

                        var modules = new Module[] { new CustomDownloaderModule(authorizedCookies) };
                        NCrawlerModule.Setup(modules);
                        _logger.InfoFormat(authorizedCookies.Count > 2
                            ? "Crawler is logged in, performing a secured search"
                            : "Crawler could not login, going to crawl without");
                    }
                    else
                        _logger.InfoFormat("Required values for the crawler to login are not met, going to crawl without");

                    using (var c = new UpdateContextAwareCrawler(context, runningContextId, new Uri(url), new HtmlDocumentProcessor(_indexFilters, _followFilters), this))
                    {
                        _logger.Info("Crawler started: Using 2 threads");
                        c.AdhereToRobotRules = AdhereToRobotRules;
                        c.MaximumThreadCount = MaximumThreadCount;
                        c.UriSensitivity = UriSensitivity;
                        c.UseCookies = UseCookies;
                        c.ExcludeFilter = new[]
                        {
                            new RegexFilter(new Regex(RegexExcludeFilter))
                        };

                        c.Crawl();
                    }
                }
            }
            finally
            {
                _logger.Info("Crawler finished");
                lock (_runninglock)
                {
                    _isrunning = false;

                }
            }
        }

        protected IEnumerable<string> GetTransformedUrls()
        {
            return _urls.Cast<string>().Select(s =>
                {
                    var url = s;
                    if (string.IsNullOrEmpty(url)) return null;

                    if (url.Contains("$("))
                    {
                        foreach (var variable in GlobalVariables)
                        {
                            url = url.Replace("$(" + variable.Key + ")", variable.Value);
                        }
                    }
                    if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                        url = "http://" + url;
                    return url;
                }).Where(s => !string.IsNullOrEmpty(s));
        }


        public void Add(IndexUpdateContext context)
        {
            _logger.InfoFormat("Crawler rebuild called, going to crawl {0} urls", _urls.Count);
            Crawl(context);
        }
        
        //IPipelineStep.Process
        public void Process(Crawler crawler, PropertyBag propertyBag)
        {
            var updateCrawler = crawler as UpdateContextAwareCrawler;
            if (updateCrawler == null)
            {
                _logger.Info("Crawler is not an UpdateContextAwareCrawler, we can't deal with this crawler");
                return;
            }

            string id = propertyBag.Step.Uri.PathAndQuery;

            GetIndexWriter(updateCrawler.UpdateContext).DeleteDocuments(new Term(BuiltinFields.Path, id));
            if (propertyBag.StatusCode == System.Net.HttpStatusCode.OK)
            {
                //this should have been done by NCrawler, but let's do it here... could move this to seperate crawlerrulesservice class, but then we'd have to download the content again
                if (crawler.AdhereToRobotRules)
                {
                    var htmlDocProp = propertyBag["HtmlDoc"];
                    if (htmlDocProp != null && htmlDocProp.Value != null)
                    {
                        var htmlDoc = htmlDocProp.Value as HtmlDocument;
                        if (htmlDoc != null)
                        {
                            var metaNodes = htmlDoc.DocumentNode.SelectNodes("//meta");
                            if (metaNodes != null)
                                foreach (var metaNode in metaNodes)
                                {
                                    var name = metaNode.GetAttributeValue("name", string.Empty);
                                    var content = metaNode.GetAttributeValue("content", string.Empty);
                                    if (("robots".Equals(name, StringComparison.InvariantCultureIgnoreCase) || "internal-index".Equals(name, StringComparison.InvariantCultureIgnoreCase))
                                        && !string.IsNullOrEmpty(content) && content.ToLowerInvariant().Contains("noindex"))
                                    {
                                        _logger.InfoFormat("Skipping {0}, encountered meta tag {1}='{2}'", id, name, content);
                                        return;
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
                                    _logger.InfoFormat("Skipping {0}, encountered canonical tag to '{1}'", id, canonicalLink);
                                    return;
                                }
                            }
                        }
                    }
                }

                updateCrawler.UpdateContext.AddDocument(CreateDocument(propertyBag, updateCrawler.RunningContextId, id));
                _logger.InfoFormat("Add/Update [{0}]", id);

            }
            else if (propertyBag.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.InfoFormat("Crawler encoutered 404 for [{0}]",id);
            }
            else
            {
                _logger.WarnFormat("Crawler encountered status {0} ({1}) for document {2}", propertyBag.StatusCode.ToString(),  propertyBag.StatusDescription, id);
            }
        }

        protected virtual Document CreateDocument(PropertyBag propertyBag, ShortID runningContextId, string documentId)
        {
            var document = new Document();
            this.AddCommonFields(document, propertyBag, runningContextId, documentId);
            this.AddContent(document, propertyBag);
            return document;
        }

        protected virtual void AddContent(Document document, PropertyBag propertyBag)
        {
            document.Add(CreateTextField(BuiltinFields.Content, propertyBag.Text));
            document.Add(CreateTextField(BuiltinFields.Content, propertyBag.Title));

            if (propertyBag["Meta"] != null)
            {
                var metaTags = propertyBag["Meta"].Value as string[];
                foreach (var metaTag in metaTags)
                {
                    if (metaTag.StartsWith("keywords:", StringComparison.InvariantCultureIgnoreCase))
                    {
                        document.Add(CreateTextField(BuiltinFields.Content, metaTag.Substring("keywords:".Length)));
                    }
                    else if (metaTag.StartsWith("description:", StringComparison.InvariantCultureIgnoreCase))
                    {
                        string description = metaTag.Substring("description:".Length);
                        document.Add(CreateTextField(BuiltinFields.Content, description));
                        document.Add(CreateTextField(CustomFields.Description, description));
                        document.Add(CreateDataField(CustomFields.Description, description));
                    }
                }
            }
            if (document.Get(CustomFields.Description) == null)
            {
                document.Add(CreateDataField(CustomFields.Description, ""));
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
                            var db = Factory.GetDatabase(itemUri.DatabaseName);
                            var item = db.GetItem(new DataUri(itemUri));
                            AddVersionIdentifiers(item, document);
                            AddSpecialFields(item, document);
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
            document.Add(this.CreateTextField(BuiltinFields.Name, propertyBag.Title));
            document.Add(this.CreateDataField(BuiltinFields.Name, propertyBag.Title));
            document.Add(this.CreateValueField(CustomFields.UpdateContextId, runningContextId.ToString().ToLower()));
            document.Add(this.CreateDataField(CustomFields.Depth, propertyBag.Step.Depth.ToString()));
            document.Add(this.CreateValueField(BuiltinFields.Path, id));
            document.Add(this.CreateDataField(BuiltinFields.Path, id));
            document.Add(this.CreateTextField(BuiltinFields.Tags, this.Tags));
            document.Add(this.CreateDataField(BuiltinFields.Tags, this.Tags));
            document.Add(CreateDataField(BuiltinFields.Group, id));
            document.Boost = this.Boost;
        }

        protected virtual void AddVersionIdentifiers(Item item, Document document)
        {
            Assert.ArgumentNotNull(item, "item");
            Assert.ArgumentNotNull(document, "document");
            document.Add(CreateValueField(BuiltinFields.Database, item.Database.Name));
            var itemId = GetItemId(item.ID, item.Language.ToString(), item.Version.ToString());
            document.Add(CreateValueField(BuiltinFields.ID, itemId));
            document.Add(CreateDataField(BuiltinFields.ID, itemId));
            document.Add(CreateValueField(BuiltinFields.Language, item.Language.ToString()));
            document.Add(CreateTextField(BuiltinFields.Template, ShortID.Encode(item.TemplateID)));
            document.Add(CreateDataField(BuiltinFields.Url, item.Uri.ToString()));
        }

        protected virtual void AddSpecialFields(Item item, Document document)
        {
            Assert.ArgumentNotNull((object)document, "document");
            Assert.ArgumentNotNull((object)item, "item");
            string displayName = item.Appearance.DisplayName;
            Assert.IsNotNull((object)displayName, "Item's display name is null.");
            document.Add(this.CreateTextField(BuiltinFields.Name, item.Name));
            document.Add(this.CreateTextField(BuiltinFields.Name, displayName));
            document.Add(this.CreateValueField(BuiltinFields.Icon, item.Appearance.Icon));
            document.Add(this.CreateTextField(BuiltinFields.Creator, item.Statistics.CreatedBy));
            document.Add(this.CreateTextField(BuiltinFields.Editor, item.Statistics.UpdatedBy));
            document.Add(this.CreateTextField(BuiltinFields.AllTemplates, this.GetAllTemplates(item)));
            document.Add(this.CreateTextField(BuiltinFields.TemplateName, item.TemplateName));
            if (this.IsHidden(item))
                document.Add(this.CreateValueField(BuiltinFields.Hidden, "1"));
            document.Add(this.CreateValueField(BuiltinFields.Created, item[FieldIDs.Created]));
            document.Add(this.CreateValueField(BuiltinFields.Updated, item[FieldIDs.Updated]));
            document.Add(this.CreateTextField(BuiltinFields.Path, this.GetItemPath(item)));
            document.Add(this.CreateTextField(BuiltinFields.Links, this.GetItemLinks(item)));
            if (this.Tags.Length <= 0)
                return;
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

        protected string GetItemPath(Item item)
        {
            Assert.ArgumentNotNull((object)item, "item");
            return new Regex("[{}-]", RegexOptions.Compiled).Replace(item.Paths.LongID.Replace('/', ' '), string.Empty);
        }
        
        #region Helpers
        readonly System.Reflection.FieldInfo _writerField = typeof(IndexUpdateContext).GetField("_writer", BindingFlags.Instance | BindingFlags.GetField | BindingFlags.NonPublic);
        public IndexWriter GetIndexWriter(IndexUpdateContext updateContext)
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

        private static CookieContainer GetAuthorizationCookie(Uri loginPage, NameValueCollection postData)
        {
            CookieContainer cookies;

            using (var client = new CookiesAwareWebClient())
            {
                client.IgnoreRedirects = false;
                //Load Page via get request to initialize cookies...
                client.DownloadData(loginPage);
                //Add cookies to the outbound request.
                client.OutboundCookies.Add(client.InboundCookies);
                client.UploadValues(loginPage, "POST", postData);
                //Add latest cookies (includes the authorization to the cookie collection)
                client.OutboundCookies.Add(client.InboundCookies);
                cookies = client.OutboundCookies;
            }

            return cookies;
        }
        #endregion
    }
}
