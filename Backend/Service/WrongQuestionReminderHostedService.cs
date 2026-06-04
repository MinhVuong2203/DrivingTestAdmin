using Backend.Service.Interface;
using Microsoft.Extensions.Options;

namespace Backend.Service
{
    public class WrongQuestionReminderHostedService : BackgroundService
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptionsMonitor<WrongQuestionReminderOptions> _options;
        private readonly ILogger<WrongQuestionReminderHostedService> _logger;
        private string? _lastRunKey;
        private string? _lastLoggedScheduleKey;

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
                    await Task.Delay(PollInterval, stoppingToken);
                    continue;
                }

                var timeZone = ResolveTimeZone(options.TimeZoneId);
                var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);
                LogScheduleIfChanged(options, now);

                if (!IsScheduledMinute(now, options))
                {
                    await Task.Delay(PollInterval, stoppingToken);
                    continue;
                }

                var runKey = $"{now:yyyy-MM-dd}:{options.Hour:D2}:{options.Minute:D2}";
                if (runKey == _lastRunKey)
                {
                    await Task.Delay(PollInterval, stoppingToken);
                    continue;
                }

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
                    _lastRunKey = runKey;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Wrong question reminder job failed.");
                }

                await Task.Delay(PollInterval, stoppingToken);
            }
        }

        private void LogScheduleIfChanged(
            WrongQuestionReminderOptions options,
            DateTimeOffset now)
        {
            var next = GetNextRun(options, now);
            var scheduleKey =
                $"{options.TimeZoneId}:{options.Hour:D2}:{options.Minute:D2}:{next:yyyy-MM-dd HH:mm}";

            if (scheduleKey == _lastLoggedScheduleKey)
            {
                return;
            }

            _lastLoggedScheduleKey = scheduleKey;
            _logger.LogInformation(
                "Wrong question reminder scheduler enabled. NextRunLocal={NextRunLocal}, TimeZone={TimeZone}",
                next,
                options.TimeZoneId);
        }

        private static bool IsScheduledMinute(
            DateTimeOffset now,
            WrongQuestionReminderOptions options)
        {
            return now.Hour == options.Hour && now.Minute == options.Minute;
        }

        private static DateTimeOffset GetNextRun(
            WrongQuestionReminderOptions options,
            DateTimeOffset now)
        {
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

            return next;
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
