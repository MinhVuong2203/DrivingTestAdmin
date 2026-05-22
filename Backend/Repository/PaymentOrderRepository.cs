using Backend.Models;
using Google.Cloud.Firestore;

namespace Backend.Repository
{
    public class PaymentOrderRepository
    {
        private readonly FirestoreDb _db;

        public PaymentOrderRepository(FirestoreDb db)
        {
            _db = db;
        }

        public async Task<PaymentOrder> CreateAsync(PaymentOrder order)
        {
            var docRef = _db.Collection("payment_orders").Document(order.OrderCode.ToString());
            order.Id = docRef.Id;
            order.CreatedAt = DateTime.UtcNow;

            await docRef.SetAsync(order, SetOptions.Overwrite);
            return order;
        }

        public async Task<PaymentOrder?> GetByOrderCodeAsync(long orderCode)
        {
            var doc = await _db.Collection("payment_orders")
                .Document(orderCode.ToString())
                .GetSnapshotAsync();

            if (!doc.Exists) return null;

            var order = doc.ConvertTo<PaymentOrder>();
            order.Id = doc.Id;
            return order;
        }

        public async Task MarkPaidAsync(long orderCode)
        {
            await _db.Collection("payment_orders")
                .Document(orderCode.ToString())
                .UpdateAsync(new Dictionary<string, object>
                {
                    { "status", "PAID" },
                    { "paid_at", DateTime.UtcNow }
                });
        }

        public async Task UpdateStatusAsync(long orderCode, string status)
        {
            await _db.Collection("payment_orders")
                .Document(orderCode.ToString())
                .UpdateAsync("status", status);
        }
    }
}
