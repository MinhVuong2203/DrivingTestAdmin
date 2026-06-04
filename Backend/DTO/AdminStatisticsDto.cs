namespace Backend.DTO
{
    public class AdminStatisticsResponse
    {
        public AdminStatisticsOverview Overview { get; set; } = new();
        public AdminStatisticsCharts Charts { get; set; } = new();
        public AdminStatisticsDetails Details { get; set; } = new();
        public AdminStatisticsRange Range { get; set; } = new();
    }

    public class AdminStatisticsOverview
    {
        public int TotalUsers { get; set; }
        public int NewUsers { get; set; }
        public int TotalVipPayments { get; set; }
        public int NewVipPayments { get; set; }
        public long TotalVipRevenue { get; set; }
        public long NewVipRevenue { get; set; }
        public int TotalPosts { get; set; }
        public int NewPosts { get; set; }
        public int TotalComments { get; set; }
        public int NewComments { get; set; }
    }

    public class AdminStatisticsCharts
    {
        public List<AdminStatisticsChartPoint> Users { get; set; } = new();
        public List<AdminStatisticsChartPoint> VipPayments { get; set; } = new();
        public List<AdminStatisticsChartPoint> Posts { get; set; } = new();
        public List<AdminStatisticsChartPoint> Comments { get; set; } = new();
    }

    public class AdminStatisticsChartPoint
    {
        public string Date { get; set; } = "";
        public int Count { get; set; }
        public long Revenue { get; set; }
    }

    public class AdminStatisticsRange
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
    }

    public class AdminStatisticsDetails
    {
        public List<AdminStatisticsUserDetail> Users { get; set; } = new();
        public List<AdminStatisticsVipPaymentDetail> VipPayments { get; set; } = new();
        public List<AdminStatisticsPostDetail> Posts { get; set; } = new();
        public List<AdminStatisticsCommentDetail> Comments { get; set; } = new();
    }

    public class AdminStatisticsUserDetail
    {
        public string Uid { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Status { get; set; } = "";
        public string Role { get; set; } = "";
        public string PhotoUrl { get; set; } = "";
        public DateTime? CreatedAt { get; set; }
    }

    public class AdminStatisticsVipPaymentDetail
    {
        public string Id { get; set; } = "";
        public long OrderCode { get; set; }
        public string UserId { get; set; } = "";
        public string PackageId { get; set; } = "";
        public string PackageName { get; set; } = "";
        public int Amount { get; set; }
        public DateTime? PaidAt { get; set; }
    }

    public class AdminStatisticsPostDetail
    {
        public string PostId { get; set; } = "";
        public string AuthorId { get; set; } = "";
        public string AuthorName { get; set; } = "";
        public string Content { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public int LikeCount { get; set; }
        public int CommentCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AdminStatisticsCommentDetail
    {
        public string CommentId { get; set; } = "";
        public string PostId { get; set; } = "";
        public string AuthorId { get; set; } = "";
        public string AuthorName { get; set; } = "";
        public string Content { get; set; } = "";
        public int LikeCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
