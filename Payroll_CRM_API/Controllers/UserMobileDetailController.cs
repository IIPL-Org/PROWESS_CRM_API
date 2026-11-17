using Microsoft.AspNetCore.Mvc;
using Payroll_CRM_API.Models;
using Payroll_CRM_API.Services;

namespace Payroll_CRM_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class UserMobileDetailController : ControllerBase
    {
        private readonly IUserMobileDetailService _service;
        private readonly ILogger<UserMobileDetailController> _logger;

        public UserMobileDetailController(IUserMobileDetailService service, ILogger<UserMobileDetailController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>
        /// Creates a new User Mobile Detail entry.
        /// </summary>
        [HttpPost("CreateUserMobileDetail")]
        [ProducesResponseType(typeof(ApiResponse<UserMobileDetailResponseDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<UserMobileDetailResponseDto>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateUserMobileDetail([FromBody] CreateUserMobileDetailDto dto, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(ApiResponse<UserMobileDetailResponseDto>.FailureResponse("Validation failed.", errors));
            }

            try
            {
                var result = await _service.CreateUserMobileDetailAsync(dto, cancellationToken);
                var response = ApiResponse<UserMobileDetailResponseDto>.SuccessResponse(result, "User Mobile Detail created successfully.");

                return CreatedAtAction(nameof(FetchUserMobileDetailByEmpId), new { empId = result.EmpId }, response);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Validation error creating User Mobile Detail for Client ID {ClientId}, Emp ID {EmpId}", dto.ClientId, dto.EmpId);
                return BadRequest(ApiResponse<UserMobileDetailResponseDto>.FailureResponse(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating User Mobile Detail for Client ID {ClientId}, Emp ID {EmpId}", dto.ClientId, dto.EmpId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<UserMobileDetailResponseDto>.FailureResponse("An internal error occurred while saving user mobile details."));
            }
        }

        /// <summary>
        /// Updates/Saves an existing User Mobile Detail record using HTTP POST (IIS compatible).
        /// </summary>
        [HttpPost("UpdateUserMobileDetail")]
        [ProducesResponseType(typeof(ApiResponse<UserMobileDetailResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<UserMobileDetailResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<UserMobileDetailResponseDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateUserMobileDetail([FromBody] UpdateUserMobileDetailDto dto, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(ApiResponse<UserMobileDetailResponseDto>.FailureResponse("Validation failed.", errors));
            }

            try
            {
                bool updated = await _service.UpdateUserMobileDetailAsync(dto, cancellationToken);
                if (!updated)
                {
                    return NotFound(ApiResponse<UserMobileDetailResponseDto>.FailureResponse($"User Mobile Detail record for Client ID {dto.ClientId} and Emp ID {dto.EmpId} not found."));
                }

                var updatedRecord = dto.UMobId.HasValue && dto.UMobId.Value > 0
                    ? await _service.GetByIdAsync(dto.UMobId.Value, cancellationToken)
                    : await _service.GetByEmpIdAsync(dto.EmpId, cancellationToken);

                return Ok(ApiResponse<UserMobileDetailResponseDto>.SuccessResponse(updatedRecord!, "User Mobile Detail status updated successfully."));
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Validation error updating User Mobile Detail for Client ID {ClientId}, Emp ID {EmpId}", dto.ClientId, dto.EmpId);
                return BadRequest(ApiResponse<UserMobileDetailResponseDto>.FailureResponse(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating User Mobile Detail for Client ID {ClientId}, Emp ID {EmpId}", dto.ClientId, dto.EmpId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<UserMobileDetailResponseDto>.FailureResponse("An internal error occurred while updating user mobile details."));
            }
        }

        /// <summary>
        /// Fetches User Mobile Detail by Emp_Id.
        /// </summary>
        [HttpGet("FetchUserMobileDetailByEmpId/{empId:int}")]
        [ProducesResponseType(typeof(ApiResponse<UserMobileDetailResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<UserMobileDetailResponseDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> FetchUserMobileDetailByEmpId(int empId, CancellationToken cancellationToken)
        {
            try
            {
                var detail = await _service.GetByEmpIdAsync(empId, cancellationToken);
                if (detail == null)
                {
                    return NotFound(ApiResponse<UserMobileDetailResponseDto>.FailureResponse($"User Mobile Detail with Emp ID {empId} not found."));
                }

                return Ok(ApiResponse<UserMobileDetailResponseDto>.SuccessResponse(detail, "User Mobile Detail retrieved successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching User Mobile Detail for Emp ID {EmpId}", empId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<UserMobileDetailResponseDto>.FailureResponse("An internal error occurred while fetching user mobile details."));
            }
        }

        /// <summary>
        /// Fetches all User Mobile Details associated with a specific Client ID.
        /// </summary>
        [HttpGet("FetchUserMobileDetailsByClientId/{clientId:int}")]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<UserMobileDetailResponseDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> FetchUserMobileDetailsByClientId(int clientId, [FromQuery] string? status, CancellationToken cancellationToken)
        {
            try
            {
                var details = await _service.GetByClientIdAsync(clientId, status, cancellationToken);
                return Ok(ApiResponse<IEnumerable<UserMobileDetailResponseDto>>.SuccessResponse(details, "User Mobile Details for Client retrieved successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching User Mobile Details for Client ID {ClientId}", clientId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<IEnumerable<UserMobileDetailResponseDto>>.FailureResponse("An internal error occurred while fetching user mobile details."));
            }
        }

        /// <summary>
        /// Fetches all User Mobile Details.
        /// </summary>
        [HttpGet("FetchAllUserMobileDetails")]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<UserMobileDetailResponseDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> FetchAllUserMobileDetails(CancellationToken cancellationToken)
        {
            try
            {
                var details = await _service.GetAllAsync(cancellationToken);
                return Ok(ApiResponse<IEnumerable<UserMobileDetailResponseDto>>.SuccessResponse(details, "All User Mobile Details retrieved successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all User Mobile Details");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<IEnumerable<UserMobileDetailResponseDto>>.FailureResponse("An internal error occurred while fetching all user mobile details."));
            }
        }

        /// <summary>
        /// Validates if user mobile number exists and is active along with active Client master, returning client HRMS links.
        /// </summary>
        [HttpPost("User_Validation")]
        [ProducesResponseType(typeof(ApiResponse<UserValidationResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<UserValidationResponseDto>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UserValidation([FromBody] UserValidationRequestDto dto, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(ApiResponse<UserValidationResponseDto>.FailureResponse("Validation failed.", errors));
            }

            try
            {
                var result = await _service.ValidateUserMobileAsync(dto.MobileNo, cancellationToken);
                return Ok(ApiResponse<UserValidationResponseDto>.SuccessResponse(result, result.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating user mobile {MobileNo}", dto.MobileNo);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<UserValidationResponseDto>.FailureResponse("An internal error occurred during user validation."));
            }
        }
    }
}
