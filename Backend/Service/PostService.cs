using Backend.Repository;
using Backend.Service.Interface;

namespace Backend.Service
{
    public class PostService : IPostService
    {
        private readonly PostRepository _repo;

        public PostService(PostRepository repo)
        {
            _repo = repo;
        }

        public Task Create(Post post)
        {
            return _repo.Create(post);
        }

        public Task Delete(string id)
        {
            return _repo.Delete(id);
        }

        public Task<List<Post>> GetAll()
        {
            return _repo.GetAll();
        }

        public Task<List<Post>> GetByAuthorID(string authorId)
        {
            return _repo.GetByAuthorID(authorId);
        }

        public Task<Post?> GetById(string id)
        {
            return _repo.GetById(id);
        }

        public Task Update(string id, Post post)
        {
            return _repo.Update(id, post);
        }

        public Task LikePost(string postId, string userId)
        {
            return _repo.LikePost(postId, userId);
        }

        public Task UnlikePost(string postId, string userId)
        {
            return _repo.UnlikePost(postId, userId);
        }

        public Task<bool> IsLiked(string postId, string userId)
        {
            return _repo.IsLiked(postId, userId);
        }
    }
}
