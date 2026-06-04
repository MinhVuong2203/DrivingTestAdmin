using Backend.Models;
using Google.Cloud.Firestore;

namespace Backend.Repository
{
    public class AdminStatisticsRepository
    {
        private readonly FirestoreDb _db;

        public AdminStatisticsRepository(FirestoreDb db)
        {
            _db = db;
        }

        public async Task<List<User>> GetUsers()
        {
            var snapshot = await _db.Collection("users").GetSnapshotAsync();

            return snapshot.Documents
                .Select(document =>
                {
                    var user = document.ConvertTo<User>();
                    user.uid = document.Id;
                    return user;
                })
                .ToList();
        }

        public async Task<List<PaymentOrder>> GetPaymentOrders()
        {
            var snapshot = await _db.Collection("payment_orders").GetSnapshotAsync();

            return snapshot.Documents
                .Select(document =>
                {
                    var order = document.ConvertTo<PaymentOrder>();
                    order.Id = document.Id;
                    return order;
                })
                .ToList();
        }

        public async Task<List<Post>> GetPosts()
        {
            var snapshot = await _db.Collection("posts").GetSnapshotAsync();

            return snapshot.Documents
                .Select(document =>
                {
                    var post = document.ConvertTo<Post>();
                    post.postId = document.Id;
                    return post;
                })
                .ToList();
        }

        public async Task<List<Comment>> GetComments()
        {
            var snapshot = await _db.CollectionGroup("comments").GetSnapshotAsync();

            return snapshot.Documents
                .Select(document =>
                {
                    var comment = document.ConvertTo<Comment>();
                    comment.commentId = document.Id;
                    comment.postId = document.Reference.Parent.Parent?.Id ?? comment.postId;
                    return comment;
                })
                .ToList();
        }
    }
}
