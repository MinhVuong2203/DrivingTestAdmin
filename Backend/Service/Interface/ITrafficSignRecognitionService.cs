namespace Backend.Service.Interface
{
    public interface ITrafficSignRecognitionService
    {
        Task<string> RecognizeTrafficSign(string base64Image, string? mimeType = null);
    }
}
