using Backend.DTO;
using System.Text.Json;

namespace Backend.Service.Interface
{
    public interface IPayOsPaymentService
    {
        Task<PayOsPaymentResponse> CreatePaymentAsync(CreatePayOsPaymentRequest request);
        Task<bool> HandleWebhookAsync(JsonElement payload);
        Task<bool> SyncPaymentStatusAsync(long orderCode);
    }
}
