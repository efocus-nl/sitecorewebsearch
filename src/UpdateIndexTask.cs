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
                            var index = SearchManager.GetIndex(indexName);
                            if (index != null)
                            {
                                IndexAction action;
                                Enum.TryParse(item["Action"], true, out action);

                                switch (action)
                                {
                                    case IndexAction.Update:
                                        Event.RaiseEvent("efocus:updateindex:" + index.Name.ToLower(), index);
                                        Event.RaiseEvent("efocus:updateindex:" + index.Name.ToLower() + ":remote", index);
                                        break;
                                    default:
                                        Event.RaiseEvent("efocus:rebuildindex:" + index.Name.ToLower() + ":remote");
                                        index.Rebuild();
                                        break;
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