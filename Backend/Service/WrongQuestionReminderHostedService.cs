using Backend.Service.Interface;
using Microsoft.Extensions.Options;

namespace Backend.Service
{
    public class WrongQuestionReminderHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptionsMonitor<WrongQuestionReminderOptions> _options;
        private readonly ILogger<WrongQuestionReminderHostedService> _logger;

        public WrongQuestionReminderHostedService(
            IServiceScopeFactory scopeFactory,
            IOptionsMonitor<WrongQuestionReminderOptions> options,
            ILogger<WrongQuestionReminderHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var options = _options.CurrentValue;

                if (!options.Enabled)
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                    continue;
                }

                var delay = GetDelayUntilNextRun(options);
                await Task.Delay(delay, stoppingToken);

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var service = scope.ServiceProvider
                        .GetRequiredService<IWrongQuestionReminderService>();

                    var result = await service.SendToEligibleUsersAsync(stoppingToken);
                    _logger.LogInformation(
                        "Wrong question reminders sent. EligibleUsers={EligibleUsers}, Tokens={Tokens}, Success={Success}, Failure={Failure}",
                        result.EligibleUsers,
                        result.TokenCount,
                        result.SuccessCount,
                        result.FailureCount);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Wrong question reminder job failed.");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private static TimeSpan GetDelayUntilNextRun(
            WrongQuestionReminderOptions options)
        {
            var timeZone = ResolveTimeZone(options.TimeZoneId);
            var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);
            var next = new DateTimeOffset(
                now.Year,
                now.Month,
                now.Day,
                options.Hour,
                options.Minute,
                0,
                now.Offset);

            if (next <= now)
            {
                next = next.AddDays(1);
            }

            return next.ToUniversalTime() - DateTimeOffset.UtcNow;
        }

        private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
        }
    }
}
