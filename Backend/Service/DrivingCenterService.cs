using Backend.DTO;
using Backend.Models;
using Backend.Repository;
using Backend.Service.Interface;

namespace Backend.Service
{
    public class DrivingCenterService : IDrivingCenterService
    {
        private readonly DrivingCenterRepository _drivingCenterRepository;

        public DrivingCenterService(DrivingCenterRepository drivingCenterRepository)
        {
            _drivingCenterRepository = drivingCenterRepository;
        }

        public async Task<List<DrivingCenter>> GetAll()
        {
            return await _drivingCenterRepository.GetAll();
        }

        public async Task<List<DrivingCenter>> Search(string? keyword)
        {
            return await _drivingCenterRepository.Search(keyword);
        }

        public async Task<DrivingCenterSearchResult> SearchPaged(string? keyword, int page, int pageSize)
        {
            return await _drivingCenterRepository.SearchPaged(keyword, page, pageSize);
        }

        public async Task<DrivingCenter?> GetById(string id)
        {
            return await _drivingCenterRepository.GetById(id);
        }
    }
}
