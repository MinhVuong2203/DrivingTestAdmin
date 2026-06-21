namespace Backend.DTOs
{
    public class VideoUploadResponse
    {
        public string VideoUrl { get; set; } = "";

        public string OriginalVideoUrl { get; set; } = "";

        public string VideoPublicId { get; set; } = "";

        public double Duration { get; set; }

        public long Bytes { get; set; }

        public string OriginalFormat { get; set; } = "";

        public string DeliveryFormat { get; set; } = "mp4";

        public int Width { get; set; }

        public int Height { get; set; }
    }
}
