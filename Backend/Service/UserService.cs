using Backend.DTO;
using Backend.Repository;
using Backend.Service.Interface;

namespace Backend.Service
{
    public class UserService : IUserService
    {
        private readonly UserRepository _repo;
        public UserService(UserRepository repo)
        {
            _repo = repo;
        }

        public Task Create(User user)
        {
            return _repo.Create(user);
        }

        public Task Delete(string id)
        {
            return _repo.Delete(id);
        }

        public Task<List<User>> GetAll()
        {
            return _repo.GetAll();
        }

        public Task<UserPageResult> GetPage(
            UserPageRequest request,
            string? currentAdminUid,
            bool currentAdminIsImportant)
        {
            return _repo.GetPage(request, currentAdminUid, currentAdminIsImportant);
        }

        public Task<User?> GetById(string id)
        {
            return _repo.GetById(id);
        }

        public Task Update(string id, User user)
        {
            return _repo.Update(id, user);
        }

        public Task UpdateStatus(string id, string status, int? lockDays)
        {
            return _repo.UpdateStatus(id, status, lockDays);
        }

        public Task UpdateRole(string id, string role)
        {
            return _repo.UpdateRole(id, role);
        }
    }
}
