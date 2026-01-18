using GRP_03_27.Services;

namespace GRP_03_27.Services
{
    public class NotificationBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<NotificationBackgroundService> _logger;
        private readonly TimeSpan _period = TimeSpan.FromHours(1); // Run every hour

        public NotificationBackgroundService(IServiceProvider serviceProvider, ILogger<NotificationBackgroundService> logger)
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

                    // Create overdue notifications
                    await notificationService.CreateOverdueNotificationAsync();

                    // Create low stock notifications
                    await notificationService.CreateLowStockNotificationAsync();

                    _logger.LogInformation("Background notification checks completed at {Time}", DateTime.UtcNow);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during background notification processing");
                }

                await Task.Delay(_period, stoppingToken);
            }
        }
    }
}
