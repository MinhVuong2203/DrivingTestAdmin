namespace Backend.Service.Interface
{
    public interface IPostService
    {
        public Task<List<Post>> GetAll();
        public Task<Post> GetById(string id);
        public Task<List<Post>> GetByAuthorID(string authorId);
        public Task Create(Post post);
        public Task Update(string id, Post post);
        public Task Delete(string id);
        public Task LikePost(string postId, string userId);
        public Task UnlikePost(string postId, string userId);
        Task<bool> IsLiked(string postId, string userId);
        Task<List<Post>> GetPostsPaged(int limit, DateTime? lastCreatedAt);
    }
}
