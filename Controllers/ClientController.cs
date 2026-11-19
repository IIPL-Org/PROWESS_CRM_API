using Microsoft.AspNetCore.Mvc;
using PROWESS_CRM.Models;
using PROWESS_CRM.Services;

namespace PROWESS_CRM.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ClientController : ControllerBase
    {
        private readonly IClientService _clientService;
        private readonly ILogger<ClientController> _logger;

        public ClientController(IClientService clientService, ILogger<ClientController> logger)
        {
            _clientService = clientService;
            _logger = logger;
        }

        /// <summary>
        /// Creates a new Client in Client_Master.
        /// </summary>
        [HttpPost("CreateClient")]
        [ProducesResponseType(typeof(ApiResponse<ClientResponseDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<ClientResponseDto>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateClient([FromBody] CreateClientDto dto, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(ApiResponse<ClientResponseDto>.FailureResponse("Validation failed.", errors));
            }

            try
            {
                if (await _clientService.IsClientNameExistsAsync(dto.ClientName, cancellationToken: cancellationToken))
                {
                    return BadRequest(ApiResponse<ClientResponseDto>.FailureResponse($"Client with name '{dto.ClientName}' already exists."));
                }

                var result = await _clientService.CreateClientAsync(dto, cancellationToken);
                var response = ApiResponse<ClientResponseDto>.SuccessResponse(result, "Client created successfully.");

                return CreatedAtAction(nameof(FetchClientDetails), new { id = result.ClientId }, response);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Validation error creating client {ClientName}", dto.ClientName);
                return BadRequest(ApiResponse<ClientResponseDto>.FailureResponse(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating client {ClientName}", dto.ClientName);
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    ApiResponse<ClientResponseDto>.FailureResponse("An internal error occurred while creating the client."));
            }
        }

        /// <summary>
        /// Updates an existing Client in Client_Master using HTTP POST (IIS compatible).
        /// </summary>
        [HttpPost("UpdateClient")]
        [ProducesResponseType(typeof(ApiResponse<ClientResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ClientResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<ClientResponseDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateClient([FromBody] UpdateClientDto dto, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(ApiResponse<ClientResponseDto>.FailureResponse("Validation failed.", errors));
            }

            try
            {
                if (await _clientService.IsClientNameExistsAsync(dto.ClientName, excludeClientId: dto.ClientId, cancellationToken: cancellationToken))
                {
                    return BadRequest(ApiResponse<ClientResponseDto>.FailureResponse($"Another client with name '{dto.ClientName}' already exists."));
                }

                bool updated = await _clientService.UpdateClientAsync(dto, cancellationToken);
                if (!updated)
                {
                    return NotFound(ApiResponse<ClientResponseDto>.FailureResponse($"Client with ID {dto.ClientId} not found."));
                }

                var updatedClient = await _clientService.GetClientByIdAsync(dto.ClientId, cancellationToken);
                return Ok(ApiResponse<ClientResponseDto>.SuccessResponse(updatedClient!, "Client updated successfully."));
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Validation error updating client ID {ClientId}", dto.ClientId);
                return BadRequest(ApiResponse<ClientResponseDto>.FailureResponse(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating client ID {ClientId}", dto.ClientId);
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    ApiResponse<ClientResponseDto>.FailureResponse("An internal error occurred while updating the client."));
            }
        }

        /// <summary>
        /// Assigns modules to a specific client.
        /// </summary>
        [HttpPost("AssignModules")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AssignModules([FromBody] AssignModulesDto dto, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(ApiResponse<bool>.FailureResponse("Validation failed.", errors));
            }

            try
            {
                bool assigned = await _clientService.AssignModulesAsync(dto, cancellationToken);
                if (!assigned)
                {
                    return NotFound(ApiResponse<bool>.FailureResponse($"Client with ID {dto.ClientId} not found."));
                }

                return Ok(ApiResponse<bool>.SuccessResponse(true, "Modules assigned successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning modules to client ID {ClientId}", dto.ClientId);
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    ApiResponse<bool>.FailureResponse("An internal error occurred while assigning modules."));
            }
        }

        [HttpPost("AssignESModules")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AssignESModules([FromBody] AssignESModulesDto dto, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(ApiResponse<bool>.FailureResponse("Validation failed.", errors));
            }

            try
            {
                bool assigned = await _clientService.AssignESModulesAsync(dto, cancellationToken);
                if (!assigned)
                {
                    return NotFound(ApiResponse<bool>.FailureResponse($"Client with ID {dto.ClientId} not found."));
                }

                return Ok(ApiResponse<bool>.SuccessResponse(true, "ESPortal modules assigned successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning ESPortal modules to client ID {ClientId}", dto.ClientId);
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    ApiResponse<bool>.FailureResponse("An internal error occurred while assigning ESPortal modules."));
            }
        }

        /// <summary>
        /// Retrieves Client details by ID from Client_Master.
        /// </summary>
        [HttpGet("FetchClientDetails/{id:int}")]
        [ProducesResponseType(typeof(ApiResponse<ClientResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ClientResponseDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> FetchClientDetails(int id, CancellationToken cancellationToken)
        {
            try
            {
                var client = await _clientService.GetClientByIdAsync(id, cancellationToken);
                if (client == null)
                {
                    return NotFound(ApiResponse<ClientResponseDto>.FailureResponse($"Client with ID {id} not found."));
                }

                return Ok(ApiResponse<ClientResponseDto>.SuccessResponse(client, "Client details retrieved successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching client ID {ClientId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    ApiResponse<ClientResponseDto>.FailureResponse("An internal error occurred while retrieving the client."));
            }
        }

        /// <summary>
        /// Retrieves all Clients from Client_Master.
        /// </summary>
        [HttpGet("FetchAllClients")]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<ClientResponseDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> FetchAllClients(CancellationToken cancellationToken)
        {
            try
            {
                var clients = await _clientService.GetAllClientsAsync(cancellationToken);
                return Ok(ApiResponse<IEnumerable<ClientResponseDto>>.SuccessResponse(clients, "Clients retrieved successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all clients");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    ApiResponse<IEnumerable<ClientResponseDto>>.FailureResponse("An internal error occurred while fetching clients."));
            }
        }



        /// Assigns Prowess ERP modules to a specific client.
        /// </summary>
        [HttpPost("AssignProwessModules")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AssignProwessModules([FromBody] AssignProwessModulesDto dto, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(ApiResponse<bool>.FailureResponse("Validation failed.", errors));
            }

            try
            {
                bool assigned = await _clientService.AssignProwessModulesAsync(dto, cancellationToken);
                if (!assigned)
                {
                    return NotFound(ApiResponse<bool>.FailureResponse($"Client with ID {dto.ClientId} not found."));
                }

                return Ok(ApiResponse<bool>.SuccessResponse(true, "Prowess modules assigned successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning Prowess modules to client ID {ClientId}", dto.ClientId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<bool>.FailureResponse("An internal error occurred while assigning Prowess modules."));
            }
        }














    }
}

