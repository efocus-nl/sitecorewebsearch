#eFocus websearch module for Sitecore

##About
If you work with Sitecore 7, you probably know that Sitecore 7 has greatly improved the search capabilities. But still, there is no native way to search through your generated html. There are several paid solutions like the dtSearch module for sitecore, but still, they all feel not to deepily integrated with Sitecore.

At eFocus we created our custom Websearch module, which crawls your website (or any website) and then adds the html content to your Sitecore Lucene index. It’s a total integrated Sitecore solution, so configuring and using it is a piece of cake.

The project uses (a slightly modified version of) the awesome NCrawler for the crawling part.

##Installation
The installation is straight forward, just place the dll's in the package in your bin folder, and the config file in your App_Config/Include folder.

If you wish to use the scheduled task to update your index, you can import the sitecore package in your environment also.

The binaries and sitecore package can be downloaded

##Configuration

As so many Sitecore modules, this module also allows you to configure it through an extra config file in App_Config/Includes.

A sample configuration would be App_Config/Includes/efocus.websearch.config
```
<configuration xmlns:patch="http://www.sitecore.net/xmlconfig/">
  <sitecore>
    <pipelines>
      <httpRequestBegin>
        <processor type="Efocus.LuceneWebSearch.SitecoreProcessors.AddHeadersHttpRequestProcessor, Efocus.LuceneWebSearch" patch:after="processor[@type='Sitecore.Pipelines.HttpRequest.ItemResolver, Sitecore.Kernel']" />
      </httpRequestBegin>
    </pipelines>
    <search>
      <configuration>
        <indexes>
          <index id="crawledcontent" type="Sitecore.Search.Index, Sitecore.Kernel">
            <param desc="name">$(id)</param>
            <param desc="folder">__crawledcontent</param>
            <Analyzer ref="search/analyzer"/>
            <locations hint="list:AddCrawler">
              <web type="Efocus.Sitecore.LuceneWebSearch.SiteCrawler, Efocus.Sitecore.LuceneWebSearch">
                <Urls hint="list">
                  <url>http://www.mysite.com</url>
                  <url>$(some-variable)</url>
                </Urls>
                <Triggers hint="list">
                  <Trigger>nothingnow</Trigger>
                </Triggers>
                <Tags>crawled</Tags>
                <Boost>1</Boost>
                <AdhereToRobotRules>true</AdhereToRobotRules>
                <MaximumThreadCount>2</MaximumThreadCount>
                <RegexExcludeFilter>(\.jpg|\.css|\.js|\.gif|\.jpeg|\.png|\.ico)</RegexExcludeFilter>
                <IndexFilters hint="raw:AddIndexFilter">
                  <filter start="&lt;!--BEGIN-NOINDEX--&gt;" end="&lt;!--END-NOINDEX--&gt;" />
                </IndexFilters>
                <FollowFilters hint="raw:AddFollowFilter">
                  <filter start="&lt;!--BEGIN-NOFOLLOW--&gt;" end="&lt;!--END-NOFOLLOW--&gt;" />
                  <!-- remove <a rel="nofollow"> tags -->
                  <!-- <a[^><]+?rel="[^"><"]*nofollow[^"><"]*"(.*?)> -->
                  <filter start="&lt;a[^&gt;&lt;]+?rel=&quot;[^&quot;&gt;&lt;&quot;]*nofollow[^&quot;&gt;&lt;&quot;]*?&quot;" end="&gt;" />
                  <!-- remove entire documents that have <meta name="robots" content="noindex" /> (regex = <meta name="robots" content="[^"><"]*nofollow[^"><"]*?") -->
                  <filter start="&lt;meta name=&quot;robots&quot; content=&quot;[^&quot;&gt;&lt;&quot;]*nofollow[^&quot;&gt;&lt;&quot;]*?&quot;" end="&lt;/html&gt;" />
                </FollowFilters>
              </web>
            </locations>
          </index>
        </indexes>
      </configuration>
    </search>
  </sitecore>
</configuration>
```
The httpRequestBegin pipeline processor will add some headers to all requests of your website, so the crawler knows what sitecoreid's are used for the current request
Explanation of the crawler options:
* Urls: add as many url's as you like. These url's will be used as a starting point for the crawling (the crawling will only happen on the same domain as the starting url)
* Triggers: the crawler task will subscribe to all sitecore-events that you enter here. Important note: if you don't specify any triggers, the module will subscribe to "publish:end" and "publish:end:remote". So if you never want the module to automatically start crawling, you can enter a dummy as in the above sample
* Tags: these tags (comma seperated), will be added to all index-entries
* Boost: the lucene-boost value for each entry in this index
* AdhereToRobotRules: Should the crawler follow the rules of the site beeing crawled (robots.txt, meta robots=noindex, canonical tags)
* MaximumThreadCount: the number of simultaneous requests the crawler should use to crawl your website
* RegexExcludeFilter: regular expression to not filter a request (if the url matches the regex)
* IndexFilters: in here you can add filters with a start and end regular expression. Before adding content to the index, anything between start and end will be removed first. So using the above example, if you have this html:
```
<b>you can index me</b>
<!-- BEGIN-NOINDEX -->
but not me!
<!-- END-NOINDEX -->
<i>and here is fine also</i>
```
"but not me!" is not added to the index

FollowFilters: Works quite the same as IndexFilters, only these are applied just before gathering all ```<a>``` tags to crawl. So using the above sample, if you have this html:
```
<b>you can index me</b>
<a href="follow.aspx">follow me</a>
<!-- BEGIN-NOFOLLOW -->
<a href="follownot.aspx">but not me!</a>
<!-- END-NOFOLLOW -->
<a href="follownot" rel="nofollow">me neither</a>
```
The links 'but not me' and 'me neither' are not crawled
Also, if you add a <meta name="robots" content="nofollow" /> to your <head> , not any link on the entire page will be crawled 

##Usage
For getting results from your indexed content, you can use the Efocus.Sitecore.LuceneWebSearch.Searcher class.
Eg, if your using an MVC based website, you could implement the following action:
```
public class SearchController
{
        public ActionResult SearchResults(string q, int page = 1)
        {
            var pageSize = 25;
            var model = new SearchResultsViewModel { BaseSearchResultsModel = searchResults };
            var searcher = new Searcher("crawledcontent", _sitecoreContextProvider.GetCurrentContextLanguage());
            var query = new BooleanQuery(true);

            if (!searcher.Languages.Contains(_languageService.FallbackLanguage))
                searcher.Languages.Add(_languageService.FallbackLanguage);

            int totalResults;
            var results = searcher.Query(q, out totalResults, null, pageSize * (page - 1), pageSize);

            var model = new {
              Results = results.Select(r => new SearchResultViewModel
              {
                Url = r.PathAndQuery,
                DisplayName = HttpUtility.HtmlDecode(r.Title),
                Introduction = r.Item.GetIntroduction().TakeWords(searchResults.IntroductionWords, " ...")
              }),
              Title = string.Format(searchResults.TitleFormat, totalResults > 0 ? totalResults.ToString(CultureInfo.InvariantCulture) : zeroResultsText, q),
              Introduction = string.Format(searchResults.IntroductionFormat, _paginatorHelper.GetSkip() + 1, resultsTo, totalResults, q)
            }


            return View(model);
        }
}
```

And then output the results in your view .cshtml
```
<div class="searchResults">
    @if (Model.Results == null || !Model.Results.Any())
    {
        @Html.Raw(Model.BaseSearchResultsModel.NoResultText)
    }

    @if (Model.Results != null && Model.Results.Any())
    {
        <div class="searchResultCount">
          @Html.DisplayFor(m => m.Introduction)
        </div>
              
        <ol class="searchResultsList">
            @foreach (var result in Model.Results)
            {
                <li>
                    <a href="@result.Url" class="blockLink">
                        @Html.DisplayFor(m => result.DisplayName, "H2")
                        <div class="description">
                            @Html.DisplayFor(m => result.Introduction)
                        </div>
                    </a>
                </li>
            }
        </ol>
    }
             
</div>
```
