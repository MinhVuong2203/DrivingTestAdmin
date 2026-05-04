using Backend.Models;

namespace Backend.Service.Interface
{
    public interface IVipPackageService
    {
        Task<List<VipPackage>> GetAllPackagesAsync();
        Task<VipPackage?> GetPackageByIdAsync(string id);
        Task<List<VipPackage>> GetActivePackagesAsync();
        Task<VipPackage> CreatePackageAsync(VipPackage package);
        Task<bool> UpdatePackageAsync(string id, VipPackage package);
        Task<bool> DeletePackageAsync(string id);
        Task<List<VipPackage>> SearchPackagesAsync(string keyword);
        Task<bool> ToggleActiveStatusAsync(string id);
    }
}