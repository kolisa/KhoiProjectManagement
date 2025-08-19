using KhoiProjectManagementApi.Services;

namespace KhoiProjectManagementApi.BackgroundServices
{
    public class OverdueTaskCheckerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OverdueTaskCheckerService> _logger;

        public OverdueTaskCheckerService(IServiceProvider serviceProvider, ILogger<OverdueTaskCheckerService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                    await notificationService.CheckOverdueTasksAsync();
                    _logger.LogInformation("Overdue tasks checked at {Time}", DateTime.UtcNow);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking overdue tasks");
                }

                // Check every hour
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }
}
