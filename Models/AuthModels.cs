using System.ComponentModel.DataAnnotations;

namespace PROWESS_CRM.Models
{
    /// <summary>
    /// Payload sent by frontend when attempting login
    /// </summary>
    public class LoginRequest
    {
        [Required(ErrorMessage = "Username or Email is required.")]
        public string UsernameOrEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Optional product suite chosen by user from login dropdown (e.g. 'HRMS', 'PAYROLL', 'CRM')
        /// </summary>
        public string? SelectedProductCode { get; set; }
    }

    /// <summary>
    /// Response returned on successful login containing JWT token and user profile
    /// </summary>
    public class LoginResponse
    {
        public bool Success { get; set; } = true;
        public string Message { get; set; } = "Login successful.";
        public string Token { get; set; } = string.Empty; // Bearer JWT token
        public string TokenType { get; set; } = "Bearer";
        public DateTime ExpiresAt { get; set; }
        public UserDto User { get; set; } = new UserDto();

        /// <summary>
        /// List of product suites accessible to the user
        /// </summary>
        public List<ProductDto> PermittedProducts { get; set; } = new List<ProductDto>();

        /// <summary>
        /// Currently active product suite code
        /// </summary>
        public string ActiveProductCode { get; set; } = "ADMIN";
    }

    /// <summary>
    /// Safe User profile payload sent to frontend (excludes password hash)
    /// </summary>
    public class UserDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}
