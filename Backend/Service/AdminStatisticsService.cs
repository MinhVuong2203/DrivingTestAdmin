using Backend.DTO;
using Backend.Models;
using Backend.Repository;
using Backend.Service.Interface;

namespace Backend.Service
{
    public class AdminStatisticsService : IAdminStatisticsService
    {
        private readonly AdminStatisticsRepository _repository;

        public AdminStatisticsService(AdminStatisticsRepository repository)
        {
            _repository = repository;
        }

        public async Task<AdminStatisticsResponse> GetStatistics(DateTime? from, DateTime? to, string? range)
        {
            var (fromUtc, toUtc) = ResolveRange(from, to, range);

            var usersTask = _repository.GetUsers();
            var paymentOrdersTask = _repository.GetPaymentOrders();
            var postsTask = _repository.GetPosts();
            var commentsTask = _repository.GetComments();

            await Task.WhenAll(usersTask, paymentOrdersTask, postsTask, commentsTask);

            var users = usersTask.Result;
            var paidOrders = paymentOrdersTask.Result
                .Where(order => string.Equals(order.Status, "PAID", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var posts = postsTask.Result
                .Where(post => !post.isDeleted && post.status)
                .ToList();
            var comments = commentsTask.Result
                .Where(comment => !comment.isDeleted && comment.status)
                .ToList();

            var newUsers = users
                .Where(user => IsInRange(user.createdAt, fromUtc, toUtc))
                .ToList();
            var newPaidOrders = paidOrders
                .Where(order => IsInRange(order.PaidAt ?? order.CreatedAt, fromUtc, toUtc))
                .ToList();
            var newPosts = posts
                .Where(post => IsInRange(post.createdAt, fromUtc, toUtc))
                .ToList();
            var newComments = comments
                .Where(comment => IsInRange(comment.createdAt, fromUtc, toUtc))
                .ToList();

            return new AdminStatisticsResponse
            {
                Range = new AdminStatisticsRange
                {
                    From = fromUtc,
                    To = toUtc
                },
                Overview = new AdminStatisticsOverview
                {
                    TotalUsers = users.Count,
                    NewUsers = newUsers.Count,
                    TotalVipPayments = paidOrders.Count,
                    NewVipPayments = newPaidOrders.Count,
                    TotalVipRevenue = paidOrders.Sum(order => (long)order.Amount),
                    NewVipRevenue = newPaidOrders.Sum(order => (long)order.Amount),
                    TotalPosts = posts.Count,
                    NewPosts = newPosts.Count,
                    TotalComments = comments.Count,
                    NewComments = newComments.Count
                },
                Charts = new AdminStatisticsCharts
                {
                    Users = BuildDailyChart(newUsers, user => user.createdAt),
                    VipPayments = BuildDailyChart(
                        newPaidOrders,
                        order => order.PaidAt ?? order.CreatedAt,
                        order => order.Amount),
                    Posts = BuildDailyChart(newPosts, post => post.createdAt),
                    Comments = BuildDailyChart(newComments, comment => comment.createdAt)
                },
                Details = new AdminStatisticsDetails
                {
                    Users = newUsers
                        .OrderByDescending(user => user.createdAt)
                        .Take(8)
                        .Select(user => new AdminStatisticsUserDetail
                        {
                            Uid = user.uid,
                            DisplayName = user.displayName ?? "",
                            Email = user.email ?? "",
                            Status = user.status ?? "",
                            Role = user.role ?? "",
                            PhotoUrl = user.photoURL ?? "",
                            CreatedAt = user.createdAt
                        })
                        .ToList(),
                    VipPayments = newPaidOrders
                        .OrderByDescending(order => order.PaidAt ?? order.CreatedAt)
                        .Take(8)
                        .Select(order => new AdminStatisticsVipPaymentDetail
                        {
                            Id = order.Id,
                            OrderCode = order.OrderCode,
                            UserId = order.UserId,
                            PackageId = order.PackageId,
                            PackageName = order.PackageName,
                            Amount = order.Amount,
                            PaidAt = order.PaidAt ?? order.CreatedAt
                        })
                        .ToList(),
                    Posts = newPosts
                        .OrderByDescending(post => post.createdAt)
                        .Take(8)
                        .Select(post => new AdminStatisticsPostDetail
                        {
                            PostId = post.postId,
                            AuthorId = post.authorId,
                            AuthorName = post.authorName,
                            Content = Truncate(post.content, 120),
                            ImageUrl = post.imageUrl,
                            LikeCount = post.likeCount,
                            CommentCount = post.commentCount,
                            CreatedAt = post.createdAt
                        })
                        .ToList(),
                    Comments = newComments
                        .OrderByDescending(comment => comment.createdAt)
                        .Take(8)
                        .Select(comment => new AdminStatisticsCommentDetail
                        {
                            CommentId = comment.commentId,
                            PostId = comment.postId,
                            AuthorId = comment.authorId,
                            AuthorName = comment.authorName,
                            Content = Truncate(comment.content, 120),
                            LikeCount = comment.likeCount,
                            CreatedAt = comment.createdAt
                        })
                        .ToList()
                }
            };
        }

        private static string Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "";
            }

            var trimmed = value.Trim();
            return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength] + "...";
        }

        private static (DateTime From, DateTime To) ResolveRange(DateTime? from, DateTime? to, string? range)
        {
            var now = DateTime.UtcNow;
            var toUtc = NormalizeEnd(to) ?? now;
            var fromUtc = NormalizeStart(from);

            if (fromUtc.HasValue)
            {
                return (fromUtc.Value, toUtc);
            }

            var days = ParseRangeDays(range);
            return (toUtc.Date.AddDays(-(days - 1)), toUtc);
        }

        private static int ParseRangeDays(string? range)
        {
            if (string.IsNullOrWhiteSpace(range))
            {
                return 30;
            }

            var normalized = range.Trim().ToLowerInvariant();
            if (normalized is "today" or "1d")
            {
                return 1;
            }

            if (normalized.EndsWith('d')
                && int.TryParse(normalized[..^1], out var days)
                && days > 0)
            {
                return Math.Min(days, 366);
            }

            return 30;
        }

        private static DateTime? NormalizeStart(DateTime? value)
        {
            if (!value.HasValue)
            {
                return null;
            }

            var dateTime = EnsureUtc(value.Value);
            return dateTime.TimeOfDay == TimeSpan.Zero ? dateTime.Date : dateTime;
        }

        private static DateTime? NormalizeEnd(DateTime? value)
        {
            if (!value.HasValue)
            {
                return null;
            }

            var dateTime = EnsureUtc(value.Value);
            return dateTime.TimeOfDay == TimeSpan.Zero ? dateTime.Date.AddDays(1) : dateTime;
        }

        private static DateTime EnsureUtc(DateTime value)
        {
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
        }

        private static bool IsInRange(DateTime? value, DateTime from, DateTime to)
        {
            if (!value.HasValue)
            {
                return false;
            }

            var dateTime = EnsureUtc(value.Value);
            return dateTime >= from && dateTime < to;
        }

        private static List<AdminStatisticsChartPoint> BuildDailyChart<T>(
            IEnumerable<T> items,
            Func<T, DateTime?> getDate,
            Func<T, int>? getRevenue = null)
        {
            return items
                .Select(item => new
                {
                    Date = getDate(item),
                    Revenue = getRevenue?.Invoke(item) ?? 0
                })
                .Where(item => item.Date.HasValue)
                .GroupBy(item => EnsureUtc(item.Date!.Value).Date)
                .OrderBy(group => group.Key)
                .Select(group => new AdminStatisticsChartPoint
                {
                    Date = group.Key.ToString("yyyy-MM-dd"),
                    Count = group.Count(),
                    Revenue = group.Sum(item => (long)item.Revenue)
                })
                .ToList();
        }
    }
}
