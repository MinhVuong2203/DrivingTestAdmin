using Backend.Models;
using Backend.Repository;
using Backend.Service.Interface;

namespace Backend.Service
{
    public class CommentService : ICommentService
    {
        private readonly CommentRepository _commentRepository;

        public CommentService(CommentRepository commentRepository)
        {
            _commentRepository = commentRepository;
        }

        public Task<List<Comment>> GetByPostId(string postId)
        {
            return _commentRepository.GetByPostId(postId);
        }

        public Task<Comment> Create(string postId, Comment comment)
        {
            if (string.IsNullOrWhiteSpace(comment.content))
            {
                throw new ArgumentException(
                    "Nội dung bình luận không được để trống"
                );
            }

            return _commentRepository.Create(postId, comment);
        }

        public Task<bool> Delete(
            string postId,
            string commentId,
            string currentUserId,
            bool isAdmin)
        {
            return _commentRepository.Delete(
                postId,
                commentId,
                currentUserId,
                isAdmin
            );
        }
    }
}