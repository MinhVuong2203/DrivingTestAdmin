using Backend.Models;
using Backend.Repository;
using Backend.Service.Interface;

namespace Backend.Service
{
    public class VipPackageService : IVipPackageService
    {
        private readonly VipPackageRepository _repository;

        public VipPackageService(VipPackageRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<VipPackage>> GetAllPackagesAsync()
        {
            return await _repository.GetAllAsync();
        }

        public async Task<VipPackage?> GetPackageByIdAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            return await _repository.GetByIdAsync(id);
        }

        public async Task<List<VipPackage>> GetActivePackagesAsync()
        {
            return await _repository.GetActivePackagesAsync();
        }

        public async Task<VipPackage> CreatePackageAsync(VipPackage package)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(package.VipName))
                throw new ArgumentException("Tên gói VIP không được để trống");

            if (package.VipPrice <= 0)
                throw new ArgumentException("Giá gói VIP phải lớn hơn 0");

            if (package.VipTime <= 0)
                throw new ArgumentException("Thời gian hiệu lực phải lớn hơn 0");

            return await _repository.CreateAsync(package);
        }

        public async Task<bool> UpdatePackageAsync(string id, VipPackage package)
        {
            if (string.IsNullOrEmpty(id))
                return false;

            // Validation
            if (string.IsNullOrWhiteSpace(package.VipName))
                throw new ArgumentException("Tên gói VIP không được để trống");

            if (package.VipPrice <= 0)
                throw new ArgumentException("Giá gói VIP phải lớn hơn 0");

            if (package.VipTime <= 0)
                throw new ArgumentException("Thời gian hiệu lực phải lớn hơn 0");

            package.Id = id; // Đảm bảo ID không bị thay đổi
            return await _repository.UpdateAsync(id, package);
        }

        public async Task<bool> DeletePackageAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;

            return await _repository.DeleteAsync(id);
        }

        public async Task<List<VipPackage>> SearchPackagesAsync(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return await GetAllPackagesAsync();

            return await _repository.SearchAsync(keyword);
        }

        public async Task<bool> ToggleActiveStatusAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;

            var package = await _repository.GetByIdAsync(id);
            if (package == null)
                return false;

            return await _repository.UpdateActiveStatusAsync(id, !package.IsActive);
        }
    }
}