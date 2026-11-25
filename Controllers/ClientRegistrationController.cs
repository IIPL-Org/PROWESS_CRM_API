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

            if (dto.CreatedBy <= 0)
            {
                return BadRequest(ApiResponse<ClientRegistrationResponseDto>.FailureResponse("CreatedBy user ID is required."));
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
        /// Fetches the client list for the Customer Master search/list table with primary contact and branch counts.
        /// Supports optional filtering by SearchQuery, Industry, State, Status, and Stage.
        /// </summary>
        [HttpGet("Search")]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<ClientSearchRowDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Search([FromQuery] ClientSearchFilterDto? filter, CancellationToken cancellationToken)
        {
            var list = await _registrationService.SearchClientsAsync(filter, cancellationToken);
            return Ok(ApiResponse<IEnumerable<ClientSearchRowDto>>.SuccessResponse(list));
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
        /// Advances the activation workflow step or toggles hold state for a subscribed product under a client.
        /// Records individual milestone sign-offs in dbo.Client_Activation_Step_History.
        /// </summary>
        [HttpPost("AdvanceActivation")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> AdvanceActivation([FromBody] AdvanceActivationStepDto dto, CancellationToken cancellationToken)
        {
            if (dto.ClientId <= 0 || string.IsNullOrWhiteSpace(dto.ProductCode))
            {
                return BadRequest(ApiResponse<bool>.FailureResponse("ClientId and ProductCode are required."));
            }

            try
            {
                var success = await _registrationService.AdvanceActivationStepAsync(dto, cancellationToken);
                return Ok(ApiResponse<bool>.SuccessResponse(success, "Activation step updated successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to advance activation step for Client {ClientId}, Product {ProductCode}", dto.ClientId, dto.ProductCode);
                return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<bool>.FailureResponse($"Failed to advance activation: {ex.Message}"));
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

        /// <summary>
        /// Updates a technical configuration field and records the previous and new value in dbo.Client_Technical_History
        /// </summary>
        [HttpPost("UpdateTechnicalConfig")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateTechnicalConfig([FromBody] UpdateTechnicalConfigDto dto, CancellationToken cancellationToken)
        {
            if (dto.ClientId <= 0 || string.IsNullOrWhiteSpace(dto.ProductCode))
            {
                return BadRequest(ApiResponse<bool>.FailureResponse("ClientId and ProductCode are required."));
            }

            if (dto.CreatedBy <= 0)
            {
                return BadRequest(ApiResponse<bool>.FailureResponse("CreatedBy user ID is required."));
            }

            try
            {
                var success = await _registrationService.UpdateTechnicalConfigAsync(dto, cancellationToken);
                return Ok(ApiResponse<bool>.SuccessResponse(success, "Technical configuration updated and recorded in history."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update technical config for Client {ClientId}, Product {ProductCode}", dto.ClientId, dto.ProductCode);
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    ApiResponse<bool>.FailureResponse($"Failed to update technical configuration: {ex.Message}"));
            }
        }

        /// <summary>
        /// Uploads a physical document file, saves it to C: drive, and stores metadata + path in dbo.Client_Document.
        /// </summary>
        [HttpPost("UploadDocument")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ApiResponse<ClientDocumentDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ClientDocumentDto>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UploadDocument([FromForm] UploadClientDocumentDto dto, CancellationToken cancellationToken)
        {
            if (dto.ClientId <= 0)
            {
                return BadRequest(ApiResponse<ClientDocumentDto>.FailureResponse("Valid ClientId is required."));
            }

            if (dto.File == null || dto.File.Length == 0)
            {
                return BadRequest(ApiResponse<ClientDocumentDto>.FailureResponse("No file was attached. Please select a file to upload."));
            }

            if (dto.CreatedBy <= 0)
            {
                return BadRequest(ApiResponse<ClientDocumentDto>.FailureResponse("CreatedBy user ID is required."));
            }

            try
            {
                var result = await _registrationService.UploadDocumentAsync(dto, dto.CreatedBy, cancellationToken);
                return Ok(ApiResponse<ClientDocumentDto>.SuccessResponse(result, "Document uploaded and saved to server successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload document for ClientId={ClientId}", dto.ClientId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<ClientDocumentDto>.FailureResponse($"Failed to upload document: {ex.Message}"));
            }
        }

        /// <summary>
        /// Retrieves all uploaded active documents for a given client from dbo.Client_Document.
        /// </summary>
        [HttpGet("GetDocumentsByClient/{clientId:int}")]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<ClientDocumentDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetDocumentsByClient(int clientId, CancellationToken cancellationToken)
        {
            var list = await _registrationService.GetDocumentsByClientIdAsync(clientId, cancellationToken);
            return Ok(ApiResponse<IEnumerable<ClientDocumentDto>>.SuccessResponse(list));
        }

        /// <summary>
        /// Downloads or previews the physical document from C: drive by its DocumentId.
        /// </summary>
        [HttpGet("DownloadDocument/{documentId:int}")]
        public async Task<IActionResult> DownloadDocument(int documentId, CancellationToken cancellationToken)
        {
            var fileData = await _registrationService.GetDocumentFileAsync(documentId, cancellationToken);
            if (fileData == null)
            {
                return NotFound(ApiResponse<string>.FailureResponse($"Document with ID {documentId} not found on server disk."));
            }

            return File(fileData.Value.FileBytes, fileData.Value.ContentType, fileData.Value.DownloadFileName);
        }

        /// <summary>
        /// Deactivates a document record.
        /// </summary>
        [HttpDelete("DeleteDocument/{documentId:int}")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        public async Task<IActionResult> DeleteDocument(int documentId, [FromQuery] int userId, CancellationToken cancellationToken = default)
        {
            if (userId <= 0)
            {
                return BadRequest(ApiResponse<bool>.FailureResponse("User ID is required."));
            }
            var success = await _registrationService.DeleteDocumentAsync(documentId, userId, cancellationToken);
            return Ok(ApiResponse<bool>.SuccessResponse(success, "Document deleted successfully."));
        }
    }
}

