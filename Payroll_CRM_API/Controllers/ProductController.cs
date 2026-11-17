using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Payroll_CRM_API.Models;
using Payroll_CRM_API.Services;

namespace Payroll_CRM_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ProductController : ControllerBase
    {
        private readonly IProductService _productService;
        private readonly ILogger<ProductController> _logger;

        public ProductController(IProductService productService, ILogger<ProductController> logger)
        {
            _productService = productService;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves all active product suites for login dropdown and public selection
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<ProductDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllActiveProducts(CancellationToken cancellationToken)
        {
            try
            {
                var products = await _productService.GetAllActiveProductsAsync(cancellationToken);
                return Ok(ApiResponse<IEnumerable<ProductDto>>.SuccessResponse(products, "Active products retrieved successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active products");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<IEnumerable<ProductDto>>.FailureResponse("An error occurred while fetching products.", new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Retrieves product suites accessible to the currently authenticated user
        /// </summary>
        [HttpGet("my-products")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<ProductDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMyProducts(CancellationToken cancellationToken)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(ApiResponse<IEnumerable<ProductDto>>.FailureResponse("Invalid token claims."));
            }

            try
            {
                var products = await _productService.GetProductsForUserAsync(userId, cancellationToken);
                return Ok(ApiResponse<IEnumerable<ProductDto>>.SuccessResponse(products, "User products retrieved successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving products for userId {UserId}", userId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<IEnumerable<ProductDto>>.FailureResponse("An error occurred while fetching user products."));
            }
        }
    }
}

