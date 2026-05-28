namespace Backend.DTO;
public class VipDto
{
    public string? name { get; set; }
    public DateTime? startDate { get; set; }
    public DateTime? endDate { get; set; }
}

public class UserDto
{
    public string uid { get; set; } = "";
    public string? displayName { get; set; }
    public string? email { get; set; }
    public string? role { get; set; }
    public string? status { get; set; }
    public DateTime? unlockAt { get; set; }
    public string? photoURL { get; set; }
    public DateTime? createdAt { get; set; }
    public VipDto? vip { get; set; }
}
