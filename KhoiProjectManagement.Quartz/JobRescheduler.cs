using KhoiProjectManagement.Application.Abstractions;
using Quartz;

namespace KhoiProjectManagement.Quartz
{
    // Implements Application's IJobRescheduler port - the only place a day/hour/minute setting turns
    // into Quartz cron syntax. Currently only handles SystemOverviewEmail's trigger; extend with
    // another method (not a generic "any job" API) if a second job ever needs this.
    public class JobRescheduler : IJobRescheduler
    {
        private static readonly JobKey SystemOverviewJobKey = new("SystemOverviewEmail");
        private static readonly TriggerKey SystemOverviewTriggerKey = new("SystemOverviewEmail-trigger");

        // Quartz's own day-of-week cron field, indexed by System.DayOfWeek's int value (Sunday=0).
        private static readonly string[] QuartzDayNames = { "SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT" };

        private readonly ISchedulerFactory _schedulerFactory;

        public JobRescheduler(ISchedulerFactory schedulerFactory)
        {
            _schedulerFactory = schedulerFactory;
        }

        public async Task ApplySystemOverviewEmailScheduleAsync(bool enabled, DayOfWeek dayOfWeek, int hour, int minute)
        {
            var scheduler = await _schedulerFactory.GetScheduler();

            if (!enabled)
            {
                if (await scheduler.CheckExists(SystemOverviewTriggerKey))
                {
                    await scheduler.UnscheduleJob(SystemOverviewTriggerKey);
                }
                return;
            }

            // Seconds minutes hours day-of-month month day-of-week - see Quartz's CronExpression docs.
            // hour/minute are validated 0-23/0-59 by UpdateSystemOverviewEmailSettingsDtoValidator
            // before this is ever called, so the resulting expression is always well-formed.
            var cron = $"0 {minute} {hour} ? * {QuartzDayNames[(int)dayOfWeek]}";
            var trigger = TriggerBuilder.Create()
                .ForJob(SystemOverviewJobKey)
                .WithIdentity(SystemOverviewTriggerKey)
                .WithCronSchedule(cron, x => x.WithMisfireHandlingInstructionDoNothing())
                .Build();

            // The job itself is always registered durably at startup (see Program.cs) even when
            // currently disabled, so this should normally be a no-op - kept as a safety net.
            if (!await scheduler.CheckExists(SystemOverviewJobKey))
            {
                var job = JobBuilder.Create<SystemOverviewEmailJob>().WithIdentity(SystemOverviewJobKey).StoreDurably().Build();
                await scheduler.AddJob(job, replace: true);
            }

            if (await scheduler.CheckExists(SystemOverviewTriggerKey))
            {
                await scheduler.RescheduleJob(SystemOverviewTriggerKey, trigger);
            }
            else
            {
                await scheduler.ScheduleJob(trigger);
            }
        }
    }
}
