namespace Backend.DTO;

public class UpdateUserStatusRequest
{
    public string Status { get; set; } = "";
    public int? LockDays { get; set; }
}
