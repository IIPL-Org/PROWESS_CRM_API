//using Microsoft.AspNetCore.Mvc;
//using PROWESS_CRM.Data;
//using PROWESS_CRM.Models;

//namespace PROWESS_CRM.Controllers
//{
//    [ApiController]
//    [Route("api/[controller]")]
//    [Produces("application/json")]
//    public class UserController : ControllerBase
//    {
//        private readonly IUserRepository _userRepository;
//        private readonly ILogger<UserController> _logger;

//        public UserController(IUserRepository userRepository, ILogger<UserController> logger)
//        {
//            _userRepository = userRepository;
//            _logger = logger;
//        }

//        /// <summary>
//        /// Retrieves all Users from dbo.UserMaster (SELECT * FROM UserMaster).
//        /// </summary>
//        [HttpGet("FetchAllUsers")]
//        [ProducesResponseType(typeof(ApiResponse<IEnumerable<UserMaster>>), StatusCodes.Status200OK)]
//        public async Task<IActionResult> FetchAllUsers()
//        {
//            try
//            {
//                var users = await _userRepository.GetAllUsersAsync();
//                return Ok(ApiResponse<IEnumerable<UserMaster>>.SuccessResponse(users, "Users retrieved successfully from UserMaster."));
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error fetching all users from UserMaster");
//                return StatusCode(StatusCodes.Status500InternalServerError,
//                    ApiResponse<IEnumerable<UserMaster>>.FailureResponse("An internal error occurred while retrieving users."));
//            }
//        }

//        /// <summary>
//        /// Creates a new User in dbo.UserMaster.
//        /// </summary>
//        [HttpPost("CreateUser")]
//        [ProducesResponseType(typeof(ApiResponse<UserMaster>), StatusCodes.Status201Created)]
//        [ProducesResponseType(typeof(ApiResponse<UserMaster>), StatusCodes.Status400BadRequest)]
//        public async Task<IActionResult> CreateUser([FromBody] UserMaster user)
//        {
//            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(user.Username))
//            {
//                return BadRequest(ApiResponse<UserMaster>.FailureResponse("Username and valid user details are required."));
//            }

//            try
//            {
//                var existing = await _userRepository.GetByUsernameOrEmailAsync(user.Username);
//                if (existing != null)
//                {
//                    return BadRequest(ApiResponse<UserMaster>.FailureResponse($"User with username '{user.Username}' already exists."));
//                }

//                var createdUser = await _userRepository.CreateUserAsync(user);
//                return CreatedAtAction(nameof(FetchAllUsers), new { id = createdUser.UserId }, ApiResponse<UserMaster>.SuccessResponse(createdUser, "User created successfully."));
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error creating user {Username}", user.Username);
//                return StatusCode(StatusCodes.Status500InternalServerError,
//                    ApiResponse<UserMaster>.FailureResponse("An internal error occurred while creating the user."));
//            }
//        }

//        /// <summary>
//        /// Updates an existing User in dbo.UserMaster using HTTP POST.
//        /// </summary>
//        [HttpPost("UpdateUser")]
//        [ProducesResponseType(typeof(ApiResponse<UserMaster>), StatusCodes.Status200OK)]
//        [ProducesResponseType(typeof(ApiResponse<UserMaster>), StatusCodes.Status400BadRequest)]
//        public async Task<IActionResult> UpdateUser([FromBody] UserMaster user)
//        {
//            if (user == null || user.UserId <= 0)
//            {
//                return BadRequest(ApiResponse<UserMaster>.FailureResponse("Valid UserId is required for update."));
//            }

//            try
//            {
//                bool updated = await _userRepository.UpdateUserAsync(user);
//                if (!updated)
//                {
//                    return NotFound(ApiResponse<UserMaster>.FailureResponse($"User with ID {user.UserId} not found."));
//                }

//                var updatedUser = await _userRepository.GetByIdAsync(user.UserId);
//                return Ok(ApiResponse<UserMaster>.SuccessResponse(updatedUser!, "User updated successfully."));
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error updating user ID {UserId}", user.UserId);
//                return StatusCode(StatusCodes.Status500InternalServerError,
//                    ApiResponse<UserMaster>.FailureResponse("An internal error occurred while updating the user."));
//            }
//        }
//    }
//}

using Microsoft.AspNetCore.Mvc;
using PROWESS_CRM.Data;
using PROWESS_CRM.Models;

namespace PROWESS_CRM.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class UserController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly ILogger<UserController> _logger;

        public UserController(IUserRepository userRepository, ILogger<UserController> logger)
        {
            _userRepository = userRepository;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves all Users from dbo.UserMaster (SELECT * FROM UserMaster).
        /// </summary>
        [HttpGet("FetchAllUsers")]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<UserMaster>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> FetchAllUsers()
        {
            try
            {
                var users = await _userRepository.GetAllUsersAsync();
                return Ok(ApiResponse<IEnumerable<UserMaster>>.SuccessResponse(users, "Users retrieved successfully from UserMaster."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all users from UserMaster");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<IEnumerable<UserMaster>>.FailureResponse($"An internal error occurred while retrieving users. Details: {ex.Message} | Inner: {ex.InnerException?.Message}"));
            }
        }

        /// <summary>
        /// Creates a new User in dbo.UserMaster.
        /// </summary>
        [HttpPost("CreateUser")]
        [ProducesResponseType(typeof(ApiResponse<UserMaster>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<UserMaster>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateUser([FromBody] UserMaster user)
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(user.Username))
            {
                return BadRequest(ApiResponse<UserMaster>.FailureResponse("Username and valid user details are required."));
            }

            try
            {
                var existing = await _userRepository.GetByUsernameOrEmailAsync(user.Username);
                if (existing != null)
                {
                    return BadRequest(ApiResponse<UserMaster>.FailureResponse($"User with username '{user.Username}' already exists."));
                }

                var createdUser = await _userRepository.CreateUserAsync(user);
                return CreatedAtAction(nameof(FetchAllUsers), new { id = createdUser.UserId }, ApiResponse<UserMaster>.SuccessResponse(createdUser, "User created successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user {Username}", user.Username);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<UserMaster>.FailureResponse("An internal error occurred while creating the user."));
            }
        }

        /// <summary>
        /// Updates an existing User in dbo.UserMaster using HTTP POST.
        /// </summary>
        [HttpPost("UpdateUser")]
        [ProducesResponseType(typeof(ApiResponse<UserMaster>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<UserMaster>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateUser([FromBody] UserMaster user)
        {
            if (user == null || user.UserId <= 0)
            {
                return BadRequest(ApiResponse<UserMaster>.FailureResponse("Valid UserId is required for update."));
            }

            try
            {
                bool updated = await _userRepository.UpdateUserAsync(user);
                if (!updated)
                {
                    return NotFound(ApiResponse<UserMaster>.FailureResponse($"User with ID {user.UserId} not found."));
                }

                var updatedUser = await _userRepository.GetByIdAsync(user.UserId);
                return Ok(ApiResponse<UserMaster>.SuccessResponse(updatedUser!, "User updated successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user ID {UserId}", user.UserId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<UserMaster>.FailureResponse("An internal error occurred while updating the user."));
            }
        }
    }
}
