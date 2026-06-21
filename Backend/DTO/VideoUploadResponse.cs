namespace Backend.DTO
{
    public class VideoUploadResponse
    {
        public string VideoUrl { get; set; } = "";
        public string VideoPublicId { get; set; } = "";
        public double Duration { get; set; }
        public long Bytes { get; set; }
        public string Format { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
    }
}