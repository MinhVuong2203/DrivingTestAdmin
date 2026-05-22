using Backend.Models;

namespace Backend.Service.Interface
{
    public interface ICommentService
    {
        Task<List<Comment>> GetByPostId(string postId);
        Task<Comment> Create(string postId, Comment comment);
        Task<bool> Delete(string postId, string commentId, string currentUserId, bool isAdmin);
    }
}
