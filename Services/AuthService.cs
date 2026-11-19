using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using PROWESS_CRM.Data;
using PROWESS_CRM.Helpers;
using PROWESS_CRM.Models;

namespace PROWESS_CRM.Services
{
    public interface IAuthService
    {
        Task<LoginResponse?> AuthenticateAsync(LoginRequest request);
        Task<UserDto?> GetUserProfileAsync(int userId);
        string EncryptUserPassword(string plainTextPassword);
        string DecryptUserPassword(string storedPasswordHash);
    }

    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IProductService _productService;
        private readonly IConfiguration _configuration;

        public AuthService(IUserRepository userRepository, IProductService productService, IConfiguration configuration)
        {
            _userRepository = userRepository;
            _productService = productService;
            _configuration = configuration;
        }

        public async Task<LoginResponse?> AuthenticateAsync(LoginRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.UsernameOrEmail) || string.IsNullOrWhiteSpace(request.Password))
            {
                return null;
            }

            // 1. Fetch user by Username or Email from DB
            var user = await _userRepository.GetByUsernameOrEmailAsync(request.UsernameOrEmail);
            if (user == null)
            {
                return null; // User not found
            }

            // 2. Multi-strategy Password Verification (SQL DecryptByPassPhrase / Plain Text, AES-256, BCrypt)
            bool isPasswordValid = false;
            string storedHash = user.PasswordHash ?? string.Empty;
            string cleanStored = storedHash.Replace("\0", "").Trim();

            try
            {
                // 2a. Direct plain text match (e.g. decrypted by SQL DecryptByPassPhrase "Admin@123" or "pranay@123")
                if (cleanStored == request.Password || storedHash == request.Password)
                {
                    isPasswordValid = true;
                }
                // 2b. BCrypt hash check
                else if (storedHash.StartsWith("$2a$") || storedHash.StartsWith("$2b$") || storedHash.StartsWith("$2y$"))
                {
                    isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, storedHash);
                }
                // 2c. AES-256 Decrypt check
                else if (!string.IsNullOrEmpty(storedHash))
                {
                    string decryptedStoredPassword = PasswordEncryptionHelper.Decrypt(storedHash);
                    isPasswordValid = (decryptedStoredPassword == request.Password || decryptedStoredPassword.Replace("\0", "").Trim() == request.Password);
                }
            }
            catch
            {
                // Fallback direct match
                isPasswordValid = (cleanStored == request.Password || storedHash == request.Password);
            }

            if (!isPasswordValid)
            {
                return null; // Invalid credentials
            }

            // 3. Generate JWT Bearer Token
            var tokenString = GenerateJwtToken(user, out DateTime expiresAt);

            // 4. Update LastLogin timestamp in DB asynchronously
            await _userRepository.UpdateLastLoginAsync(user.UserId);

            // 5. Fetch accessible products for this user
            var permittedProducts = (await _productService.GetProductsForUserAsync(user.UserId)).ToList();
            if (permittedProducts.Count == 0)
            {
                return new LoginResponse
                {
                    Success = false,
                    Message = "Access Denied: Your account does not have any product suites assigned. Please contact your administrator."
                };
            }

            string activeProduct;

            // Validate requested suite permissions if user specified a suite
            if (!string.IsNullOrWhiteSpace(request.SelectedProductCode) && 
                !string.Equals(request.SelectedProductCode, "AUTO", StringComparison.OrdinalIgnoreCase))
            {
                var matchedProduct = permittedProducts.FirstOrDefault(p =>
                    string.Equals(p.ProductCode, request.SelectedProductCode, StringComparison.OrdinalIgnoreCase) ||
                    (string.Equals(request.SelectedProductCode, "CRM", StringComparison.OrdinalIgnoreCase) && string.Equals(p.ProductCode, "ADMIN", StringComparison.OrdinalIgnoreCase)) ||
                    (string.Equals(request.SelectedProductCode, "ADMIN", StringComparison.OrdinalIgnoreCase) && string.Equals(p.ProductCode, "CRM", StringComparison.OrdinalIgnoreCase))
                );

                if (matchedProduct == null)
                {
                    var allowedNames = string.Join(", ", permittedProducts.Select(p => p.ProductName));
                    return new LoginResponse
                    {
                        Success = false,
                        Message = $"Access Denied: You do not have permission to access '{request.SelectedProductCode}'. Your account only has access to: {allowedNames}."
                    };
                }

                activeProduct = matchedProduct.ProductCode;
            }
            else
            {
                // Auto-detect default suite: prefer configured default suite, or first permitted
                var defaultProd = permittedProducts.FirstOrDefault(p => user.DefaultProductId.HasValue && p.ProductId == user.DefaultProductId.Value)
                                  ?? permittedProducts[0];
                activeProduct = defaultProd.ProductCode;
            }

            // 6. Construct and return safe LoginResponse payload
            return new LoginResponse
            {
                Success = true,
                Message = "Login successful.",
                Token = tokenString,
                TokenType = "Bearer",
                ExpiresAt = expiresAt,
                User = new UserDto
                {
                    UserId = user.UserId,
                    Username = user.Username,
                    Email = user.Email,
                    FullName = user.FullName,
                    Role = user.Role
                },
                PermittedProducts = permittedProducts,
                ActiveProductCode = activeProduct
            };
        }

        public async Task<UserDto?> GetUserProfileAsync(int userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return null;

            return new UserDto
            {
                UserId = user.UserId,
                Username = user.Username,
                Email = user.Email,
                FullName = user.FullName,
                Role = user.Role
            };
        }

        /// <summary>
        /// Encrypts a plain-text password using AES-256 for saving into Database
        /// </summary>
        public string EncryptUserPassword(string plainTextPassword)
        {
            return PasswordEncryptionHelper.Encrypt(plainTextPassword);
        }

        /// <summary>
        /// Decrypts a stored password from Database back to plain text
        /// </summary>
        public string DecryptUserPassword(string storedPasswordHash)
        {
            if (string.IsNullOrEmpty(storedPasswordHash)) return string.Empty;

            if (storedPasswordHash.StartsWith("$2a$") || storedPasswordHash.StartsWith("$2b$") || storedPasswordHash.StartsWith("$2y$"))
            {
                return "[One-way BCrypt Hash]";
            }

            return PasswordEncryptionHelper.Decrypt(storedPasswordHash);
        }

        private string GenerateJwtToken(UserMaster user, out DateTime expiresAt)
        {
            var jwtKey = _configuration["Jwt:Key"] ?? "PayrollCRM_SuperSecretKey_2026_MustBe32BytesLong!";
            var jwtIssuer = _configuration["Jwt:Issuer"] ?? "PayrollCRM_API";
            var jwtAudience = _configuration["Jwt:Audience"] ?? "PayrollCRM_Frontend";
            var expiryMinutes = double.TryParse(_configuration["Jwt:ExpiryMinutes"], out var mins) ? mins : 480;

            var keyBytes = Encoding.UTF8.GetBytes(jwtKey);
            var securityKey = new SymmetricSecurityKey(keyBytes);
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.Username ?? string.Empty),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim(ClaimTypes.Role, user.Role ?? "User"),
                new Claim("FullName", user.FullName ?? string.Empty)
            };

            expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);

            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtAudience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
