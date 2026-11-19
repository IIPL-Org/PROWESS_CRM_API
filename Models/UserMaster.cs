namespace PROWESS_CRM.Models
{
    /// <summary>
    /// Maps to dbo.UserMaster table for authentication and user profile storage
    /// </summary>
    public class UserMaster
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = "User"; // Admin, HR, Manager, User
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? LastLogin { get; set; }
        public DateTime? LastLoginAt { get; set; }

        /// <summary>
        /// List of assigned Product Suite IDs from dbo.User_Products (e.g. [1, 2, 3, 4])
        /// </summary>
        public List<int> AssignedProductIds { get; set; } = new List<int>();

        /// <summary>
        /// Default Product Suite ID for login redirection (maps to dbo.User_Products.Is_Default)
        /// </summary>
        public int? DefaultProductId { get; set; }
    }
}
