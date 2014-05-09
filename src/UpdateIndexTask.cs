using System;
using Efocus.Sitecore.LuceneWebSearch.Enums;
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
                            var index = SearchManager.GetIndex(indexName) ?? new Index(indexName, String.Format("__{0}", indexName)); ;
                            if ("update".Equals(item["Action"], StringComparison.InvariantCultureIgnoreCase))
                            {
                                Event.RaiseEvent(String.Format("efocus:updateindex:{0}", index.Name.ToLower()));
                                Event.RaiseEvent(String.Format("efocus:updateindex:{0}:remote", index.Name.ToLower()));
                            }
                            else
                            {
                                Event.RaiseEvent(String.Format("efocus:rebuildindex:{0}", index.Name.ToLower()));
                                Event.RaiseEvent(String.Format("efocus:rebuildindex:{0}:remote", index.Name.ToLower()));
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