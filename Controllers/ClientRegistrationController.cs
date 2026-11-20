using Microsoft.AspNetCore.Mvc;
using PROWESS_CRM.Models;
using PROWESS_CRM.Services;

namespace PROWESS_CRM.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ClientRegistrationController : ControllerBase
    {
        private readonly IClientRegistrationService _registrationService;
        private readonly ILogger<ClientRegistrationController> _logger;

        public ClientRegistrationController(
            IClientRegistrationService registrationService,
            ILogger<ClientRegistrationController> logger)
        {
            _registrationService = registrationService;
            _logger = logger;
        }

        /// <summary>
        /// Saves or updates a Client Registration across Client_Master, Client_Contact, Client_Branch, and Client_Commercial within a single DB transaction.
        /// Rolls back automatically if any table insert/update fails.
        /// </summary>
        [HttpPost("Save")]
        [ProducesResponseType(typeof(ApiResponse<ClientRegistrationResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ClientRegistrationResponseDto>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Save([FromBody] ClientRegistrationRequestDto dto, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(ApiResponse<ClientRegistrationResponseDto>.FailureResponse("Validation failed.", errors));
            }

            try
            {
                var result = await _registrationService.SaveClientRegistrationAsync(dto, cancellationToken);
                return Ok(ApiResponse<ClientRegistrationResponseDto>.SuccessResponse(result, "Client registration saved successfully."));
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Validation error saving client registration for {ClientName}", dto.ClientName);
                return BadRequest(ApiResponse<ClientRegistrationResponseDto>.FailureResponse(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving client registration for {ClientName} (transaction rolled back)", dto.ClientName);
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    ApiResponse<ClientRegistrationResponseDto>.FailureResponse($"Failed to save client registration (transaction rolled back): {ex.Message}"));
            }
        }

        /// <summary>
        /// Fetches full registration details for a client across all 4 tables (Client_Master, Client_Contact, Client_Branch, Client_Commercial).
        /// </summary>
        [HttpGet("GetById/{id:int}")]
        [ProducesResponseType(typeof(ApiResponse<ClientRegistrationResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ClientRegistrationResponseDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
        {
            var result = await _registrationService.GetClientRegistrationByIdAsync(id, cancellationToken);
            if (result == null)
            {
                return NotFound(ApiResponse<ClientRegistrationResponseDto>.FailureResponse($"Client with ID {id} not found."));
            }

            return Ok(ApiResponse<ClientRegistrationResponseDto>.SuccessResponse(result));
        }

        /// <summary>
        /// Fetches all registered clients from dbo.Client_Master.
        /// </summary>
        [HttpGet("GetAll")]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<ClientRegistrationResponseDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        {
            var list = await _registrationService.GetAllClientRegistrationsAsync(cancellationToken);
            return Ok(ApiResponse<IEnumerable<ClientRegistrationResponseDto>>.SuccessResponse(list));
        }

        /// <summary>
        /// Mode-wise dropdown endpoint. Pass compulsory ?mode=CUSTOMER_TYPE, INDUSTRY_CATEGORY, BUSINESS_SEGMENT,
        /// COMPANY_SIZE, CUSTOMER_SOURCE, CUSTOMER_STATUS, GST_REGISTRATION_TYPE, COUNTRY, STATE,
        /// CONTACT_CATEGORY, BRANCH_TYPE, PAYMENT_TERMS, CURRENCY, INVOICE_FREQUENCY, TAX_CATEGORY,
        /// or RELATIONSHIP_MANAGER.
        /// </summary>
        [HttpGet("Dropdowns")]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<ClientDropdownOptionDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<ClientDropdownOptionDto>>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetDropdowns([FromQuery] string mode, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(mode))
            {
                return BadRequest(ApiResponse<IEnumerable<ClientDropdownOptionDto>>.FailureResponse("The 'mode' query parameter is compulsory and cannot be empty."));
            }

            try
            {
                var options = await _registrationService.GetDropdownOptionsAsync(mode, cancellationToken);
                return Ok(ApiResponse<IEnumerable<ClientDropdownOptionDto>>.SuccessResponse(options));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<IEnumerable<ClientDropdownOptionDto>>.FailureResponse(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching dropdown options for mode={Mode}", mode);
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    ApiResponse<IEnumerable<ClientDropdownOptionDto>>.FailureResponse($"Failed to load dropdown options: {ex.Message}"));
            }
        }

        /// <summary>
        /// Generates a unique, non-sequential, and unguessable 10-character PAN-style Customer Code
        /// (e.g. ABCP7482KM) based on the customer name and entity type with checksum validation.
        /// </summary>
        [HttpGet("GenerateCustomerCode")]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GenerateCustomerCode([FromQuery] string clientName, [FromQuery] string? customerType = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(clientName))
            {
                return BadRequest(ApiResponse<string>.FailureResponse("The 'clientName' query parameter is required to generate a customer code."));
            }

            try
            {
                var code = await _registrationService.GenerateUniqueCustomerCodeAsync(clientName, customerType, cancellationToken);
                return Ok(ApiResponse<string>.SuccessResponse(code, "Customer code generated successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating customer code for {ClientName}", clientName);
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    ApiResponse<string>.FailureResponse($"Failed to generate customer code: {ex.Message}"));
            }
        }
    }
}

