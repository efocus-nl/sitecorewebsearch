using System;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Events;
using Sitecore.Search;
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
                            var index = SearchManager.GetIndex(indexName);
                            if (index != null)
                            {
                                if ("update".Equals(item["Action"]))
                                {
                                    Event.RaiseEvent("efocus:updateindex:" + index.Name.ToLower(), index);
                                }
                                else
                                {
                                    index.Rebuild();
                                }
                            }
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