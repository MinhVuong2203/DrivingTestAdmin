namespace Backend.DTO
{
    public class WrongQuestionReminderSendResult
    {
        public int EligibleUsers { get; set; }
        public int UsersWithTokens { get; set; }
        public int TokenCount { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
    }
}
