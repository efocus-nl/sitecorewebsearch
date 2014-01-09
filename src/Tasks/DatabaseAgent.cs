using System;
using System.Collections;
using BoC.InversionOfControl;
using BoC.Logging;
using Sitecore;
using Sitecore.Jobs;
using Sitecore.Tasks;

namespace Efocus.Sitecore.Glass.Commons.Tasks
{
    public class DatabaseAgent : global::Sitecore.Tasks.DatabaseAgent
    {
        private readonly string _databasename;

        public DatabaseAgent(string databaseName, string scheduleRoot) : base(databaseName, scheduleRoot)
        {
            _databasename = databaseName;
        }

        public new void Run()
        {
            var logger = IoC.Resolver.Resolve<ILogger>();

            logger.InfoFormat("Scheduling.DatabaseAgent started. Database: {0}", _databasename);
            Job job = Context.Job;
            ScheduleItem[] schedules = GetSchedules();
            logger.InfoFormat("Examining schedules (count: {0})", (object)schedules.Length);
            if (IsValidJob(job))
                job.Status.Total = schedules.Length;
            foreach (var scheduleItem in schedules)
            {
                try
                {
                    if (scheduleItem.IsDue)
                    {
                        logger.InfoFormat("Starting: {0} {1}", scheduleItem.Name, (scheduleItem.Asynchronous ? " (asynchronously)" : string.Empty));
                        scheduleItem.Execute();
                        logger.InfoFormat("Ended: {0}", scheduleItem.Name);
                    }
                    else
                        logger.InfoFormat("Not due: {0}", scheduleItem.Name);
                    if (scheduleItem.AutoRemove)
                    {
                        if (scheduleItem.Expired)
                        {
                            logger.InfoFormat("Schedule is expired. Auto removing schedule item: {0}", scheduleItem.Name);
                            scheduleItem.Remove();
                        }
                    }
                }
                catch
                {
                }
                if (IsValidJob(job))
                    ++job.Status.Processed;
            }
        }

        private ScheduleItem[] GetSchedules()
        {
            var obj = Database.Items[ScheduleRoot];
            if (obj == null)
                return new ScheduleItem[0];
            var arrayList = new ArrayList();
            foreach (var innerItem in obj.Axes.GetDescendants())
            {
                if (innerItem.TemplateID == TemplateIDs.Schedule || innerItem.TemplateID.Guid.Equals(new Guid("{b6754c83-75fc-47ea-acf1-d3368941de5f}")))
                    arrayList.Add(new ScheduleItem(innerItem));
            }
            return arrayList.ToArray(typeof(ScheduleItem)) as ScheduleItem[];
        }

        private bool IsValidJob(Job job)
        {
            if (job != null)
                return job.Category == "schedule";
            return false;
        }
    }
}
