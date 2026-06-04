using Backend.DTO;

namespace Backend.Service.Interface
{
    public interface IUserService
    {
        public Task<List<User>> GetAll();

        public Task<UserPageResult> GetPage(UserPageRequest request);

        public Task<User?> GetById(string id);

        public Task Create(User user);

        public Task Update(string id, User user);

        public Task Delete(string id);

        Task UpdateStatus(string id, string status, int? lockDays);

        Task UpdateRole(string id, string role);
    }
}
