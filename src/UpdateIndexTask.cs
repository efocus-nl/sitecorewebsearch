using System;
using BoC.InversionOfControl;
using Efocus.Sitecore.LuceneWebSearch.Events;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Eventing;
using Sitecore.Tasks;


namespace Efocus.Sitecore.LuceneWebSearch
{
    public class UpdateIndexTask
    {
        public void RunScheduledTask(Item[] itemArray, CommandItem commandItem, ScheduleItem scheduledItem)
        {
            //TODO: Document this: datasource should be an item with properties "Index" => name of the index and optional "Action" => 'rebuild' or 'update'
            if (itemArray != null && itemArray.Length > 0)
            {
                foreach (var item in itemArray)
                {
                    var indexName = item["Index"];
                    if (!String.IsNullOrEmpty(indexName))
                    {
                        try
                        {
                            var crawledIndexEvent = IoC.Resolver != null ? IoC.Resolver.Resolve<CrawlIndexEvent>() : new CrawlIndexEvent();
                            crawledIndexEvent.IndexName = indexName;

                            //look what kind of action needs to be taken
                            crawledIndexEvent.Method = "update".Equals(item["Action"], StringComparison.InvariantCultureIgnoreCase) ? CrawlMethod.Update : CrawlMethod.Rebuild;

                            //Queue the event to other servers
                            //TODO: Add option to put settings in schedule/command item
                            EventManager.QueueEvent(crawledIndexEvent, Properties.Settings.Default.RaiseCrawlEventOnRemoteQueue, Properties.Settings.Default.RaiseCrawlEventOnLocalQueue);
                        }
                        catch (Exception exception)
                        {
                            Log.Error(String.Format("Unable to update index {0}", indexName), exception, this);
                        }
                    }

                }
            }
        }
    }
}