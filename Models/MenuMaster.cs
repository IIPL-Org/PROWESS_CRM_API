namespace PROWESS_CRM.Models
{
    /// <summary>
    /// Maps to dbo.MenuMaster table for dynamic navigation sidebar
    /// </summary>
    public class MenuMaster
    {
        public int MenuId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Route { get; set; } = string.Empty;
        public string IconName { get; set; } = string.Empty; // react-icons name, e.g. "FiHome"
        public int? ParentId { get; set; }
        public int SortOrder { get; set; } = 0;

        /// <summary>
        /// Comma-separated list of allowed roles, e.g. "Admin,HR,Manager"
        /// </summary>
        public string Roles { get; set; } = "Admin,HR,Manager,User";

        public bool IsActive { get; set; } = true;
        public string? Category { get; set; } = "Main";

        /// <summary>
        /// Specific product suite code, e.g., 'HRMS', 'PAYROLL', 'CRM', 'ESPORTAL', or null if shared
        /// </summary>
        public string? ProductCode { get; set; }

        /// <summary>
        /// When true, this menu item is common across all products (e.g. Clients, Users, Settings)
        /// </summary>
        public bool IsCommon { get; set; } = false;
    }
}
