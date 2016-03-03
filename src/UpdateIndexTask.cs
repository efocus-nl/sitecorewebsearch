using System;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Events;
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
                            if ("update".Equals(item["Action"], StringComparison.InvariantCultureIgnoreCase))
                            {
                                //SwitchOnRebuildIndex doesn't know an update function, so always do rebuild index
                                //Event.RaiseEvent(String.Format("efocus:updateindex:{0}", indexName.ToLower()));
                                //Event.RaiseEvent(String.Format("efocus:updateindex:{0}:remote", indexName.ToLower()));
                                Event.RaiseEvent(String.Format("efocus:rebuildindex:{0}", indexName.ToLower()));
                                Event.RaiseEvent(String.Format("efocus:rebuildindex:{0}:remote", indexName.ToLower()));
                            }
                            else
                            {
                                Event.RaiseEvent(String.Format("efocus:rebuildindex:{0}", indexName.ToLower()));
                                Event.RaiseEvent(String.Format("efocus:rebuildindex:{0}:remote", indexName.ToLower()));
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