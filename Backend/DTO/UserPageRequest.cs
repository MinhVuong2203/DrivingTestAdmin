namespace Backend.DTO;

public class UserPageRequest
{
    public int PageSize { get; set; } = 10;
    public string? Cursor { get; set; }
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? Role { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? SortField { get; set; } = "createdAt";
    public string? SortDirection { get; set; } = "desc";
}

public class UserPageResult
{
    public List<User> Items { get; set; } = new();
    public int PageSize { get; set; }
    public string? NextCursor { get; set; }
    public bool HasNextPage { get; set; }
}
