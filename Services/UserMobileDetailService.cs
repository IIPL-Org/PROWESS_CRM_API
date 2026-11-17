using PROWESS_CRM.Data;
using PROWESS_CRM.Models;

namespace PROWESS_CRM.Services
{
    public interface IUserMobileDetailService
    {
        Task<UserMobileDetailResponseDto> CreateUserMobileDetailAsync(CreateUserMobileDetailDto dto, CancellationToken cancellationToken = default);
        Task<bool> UpdateUserMobileDetailAsync(UpdateUserMobileDetailDto dto, CancellationToken cancellationToken = default);
        Task<UserMobileDetailResponseDto?> GetByIdAsync(int uMobId, CancellationToken cancellationToken = default);
        Task<UserMobileDetailResponseDto?> GetByEmpIdAsync(int empId, CancellationToken cancellationToken = default);
        Task<IEnumerable<UserMobileDetailResponseDto>> GetByClientIdAsync(int clientId, string? status = null, CancellationToken cancellationToken = default);
        Task<IEnumerable<UserMobileDetailResponseDto>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<UserValidationResponseDto> ValidateUserMobileAsync(string mobileNo, CancellationToken cancellationToken = default);
        Task<bool> IsActiveEmpIdExistsAsync(int clientId, int empId, int? excludeUMobId = null, CancellationToken cancellationToken = default);
        Task<bool> IsActiveMobileNumberExistsAsync(string mobileNo, int? excludeUMobId = null, CancellationToken cancellationToken = default);
    }

    public class UserMobileDetailService : IUserMobileDetailService
    {
        private readonly IUserMobileDetailRepository _repository;

        public UserMobileDetailService(IUserMobileDetailRepository repository)
        {
            _repository = repository;
        }

        public async Task<UserMobileDetailResponseDto> CreateUserMobileDetailAsync(CreateUserMobileDetailDto dto, CancellationToken cancellationToken = default)
        {
            if (!await _repository.IsClientActiveAsync(dto.ClientId, cancellationToken))
            {
                throw new InvalidOperationException($"Client ID '{dto.ClientId}' does not exist or is inactive.");
            }

            char isActiveChar = char.ToUpper(dto.IsActive);
            if (isActiveChar == 'Y')
            {
                if (await _repository.IsActiveEmpIdExistsAsync(dto.ClientId, dto.EmpId, null, cancellationToken))
                {
                    throw new InvalidOperationException($"An active user mobile record already exists for Employee ID '{dto.EmpId}' under Client ID '{dto.ClientId}'.");
                }

                if (await _repository.IsActiveMobileNumberExistsAsync(dto.ClientMobile, null, cancellationToken))
                {
                    throw new InvalidOperationException($"An active user record already exists for Mobile Number '{dto.ClientMobile.Trim()}'.");
                }
            }

            var entity = new UserMobileDetail
            {
                ClientId = dto.ClientId,
                EmpId = dto.EmpId,
                ClientMobile = dto.ClientMobile.Trim(),
                IsActive = isActiveChar,
                CreatedBy = dto.CreatedBy,
                CreatedDate = DateTime.Now
            };

            int newId = await _repository.CreateAsync(entity, cancellationToken);
            var createdEntity = await _repository.GetByIdAsync(newId, cancellationToken);

            if (createdEntity == null)
            {
                throw new Exception("Failed to retrieve created User Mobile Detail record.");
            }

            return MapToResponseDto(createdEntity);
        }

        public async Task<bool> UpdateUserMobileDetailAsync(UpdateUserMobileDetailDto dto, CancellationToken cancellationToken = default)
        {
            UserMobileDetail? existing = null;
            if (dto.UMobId.HasValue && dto.UMobId.Value > 0)
            {
                existing = await _repository.GetByIdAsync(dto.UMobId.Value, cancellationToken);
            }

            if (existing == null)
            {
                existing = await _repository.GetByClientAndEmpIdAsync(dto.ClientId, dto.EmpId, cancellationToken);
            }

            if (existing == null)
            {
                return false;
            }

            if (!await _repository.IsClientActiveAsync(dto.ClientId, cancellationToken))
            {
                throw new InvalidOperationException($"Client ID '{dto.ClientId}' does not exist or is inactive.");
            }

            char isActiveChar = char.ToUpper(dto.IsActive);
            if (isActiveChar == 'Y')
            {
                if (await _repository.IsActiveEmpIdExistsAsync(dto.ClientId, dto.EmpId, existing.UMobId, cancellationToken))
                {
                    throw new InvalidOperationException($"An active user mobile record already exists for Employee ID '{dto.EmpId}' under Client ID '{dto.ClientId}'.");
                }

                if (await _repository.IsActiveMobileNumberExistsAsync(existing.ClientMobile, existing.UMobId, cancellationToken))
                {
                    throw new InvalidOperationException($"An active user record already exists for Mobile Number '{existing.ClientMobile}'.");
                }
            }

            //return await _repository.UpdateStatusAsync(existing.UMobId, isActiveChar, dto.ModifiedBy, DateTime.Now, cancellationToken);

            return await _repository.UpdateStatusAsync(dto.ClientId,dto.EmpId,isActiveChar,dto.ModifiedBy,DateTime.Now,cancellationToken);

        }

        public async Task<UserMobileDetailResponseDto?> GetByIdAsync(int uMobId, CancellationToken cancellationToken = default)
        {
            var entity = await _repository.GetByIdAsync(uMobId, cancellationToken);
            return entity == null ? null : MapToResponseDto(entity);
        }

        public async Task<UserMobileDetailResponseDto?> GetByEmpIdAsync(int empId, CancellationToken cancellationToken = default)
        {
            var entity = await _repository.GetByEmpIdAsync(empId, cancellationToken);
            return entity == null ? null : MapToResponseDto(entity);
        }

        public async Task<IEnumerable<UserMobileDetailResponseDto>> GetByClientIdAsync(int clientId, string? status = null, CancellationToken cancellationToken = default)
        {
            var list = await _repository.GetByClientIdAsync(clientId, status, cancellationToken);
            return list.Select(MapToResponseDto);
        }

        public async Task<IEnumerable<UserMobileDetailResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var list = await _repository.GetAllAsync(cancellationToken);
            return list.Select(MapToResponseDto);
        }

        public async Task<UserValidationResponseDto> ValidateUserMobileAsync(string mobileNo, CancellationToken cancellationToken = default)
        {
            var result = await _repository.ValidateUserMobileAsync(mobileNo.Trim(), cancellationToken);
            if (result != null)
            {
                return result;
            }

            return new UserValidationResponseDto
            {
                Status = "N",
                Message = "User mobile number does not exist or user/client is inactive.",
                IsExist = false
            };
        }

        public async Task<bool> IsActiveEmpIdExistsAsync(int clientId, int empId, int? excludeUMobId = null, CancellationToken cancellationToken = default)
        {
            return await _repository.IsActiveEmpIdExistsAsync(clientId, empId, excludeUMobId, cancellationToken);
        }

        public async Task<bool> IsActiveMobileNumberExistsAsync(string mobileNo, int? excludeUMobId = null, CancellationToken cancellationToken = default)
        {
            return await _repository.IsActiveMobileNumberExistsAsync(mobileNo, excludeUMobId, cancellationToken);
        }

        private static UserMobileDetailResponseDto MapToResponseDto(UserMobileDetail entity)
        {
            return new UserMobileDetailResponseDto
            {
                UMobId = entity.UMobId,
                ClientId = entity.ClientId,
                EmpId = entity.EmpId,
                ClientMobile = entity.ClientMobile,
                IsActive = entity.IsActive,
                CreatedBy = entity.CreatedBy,
                CreatedDate = entity.CreatedDate,
                ModifiedBy = entity.ModifiedBy,
                ModifiedDate = entity.ModifiedDate
            };
        }
    }
}
