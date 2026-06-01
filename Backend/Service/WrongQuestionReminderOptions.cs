namespace Backend.Service
{
    public class WrongQuestionReminderOptions
    {
        public bool Enabled { get; set; } = false;
        public int Hour { get; set; } = 8;
        public int Minute { get; set; } = 0;
        public string TimeZoneId { get; set; } = "Asia/Bangkok";
    }
}
