namespace KhoiProjectManagement.Application.Abstractions
{
    // Narrow port letting Application-layer settings changes take effect on the live Quartz scheduler
    // immediately, without Application ever depending on the Quartz package or knowing cron syntax -
    // implemented by KhoiProjectManagement.Quartz's JobRescheduler, which is the only place that turns
    // a day/hour/minute into a cron string.
    public interface IJobRescheduler
    {
        // Enables/reschedules or disables the weekly system-overview email's trigger to match the given
        // day-of-week + time-of-day (server-local, same as the trigger's default timezone). Called both
        // from SystemOverviewEmailSettingsService.UpdateAsync (an admin change) and once at startup
        // (Program.cs) to bring the live trigger in sync with whatever is currently stored in the DB.
        Task ApplySystemOverviewEmailScheduleAsync(bool enabled, DayOfWeek dayOfWeek, int hour, int minute);
    }
}
