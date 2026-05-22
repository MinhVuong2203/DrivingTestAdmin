namespace Backend.DTO
{
    public class PayOsPaymentResponse
    {
        public long OrderCode { get; set; }
        public string PaymentLinkId { get; set; } = string.Empty;
        public string CheckoutUrl { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int Amount { get; set; }
        public string PackageId { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
    }
}
