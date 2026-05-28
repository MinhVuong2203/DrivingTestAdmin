namespace Backend.DTO;

public class RecognizeTrafficSignRequest
{
    public string Base64Image { get; set; } = "";
    public string? MimeType { get; set; }
}
