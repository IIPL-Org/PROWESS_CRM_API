namespace PROWESS_CRM.Models
{
    /// <summary>
    /// Represents a Product Suite within the company (e.g., HRMS, Payroll, ESPortal, CRM)
    /// </summary>
    public class ProductMaster
    {
        public int ProductId { get; set; }
        public string ProductCode { get; set; } = string.Empty; // 'CRM', 'HRMS', 'PAYROLL', 'ESPORTAL'
        public string ProductName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? IconName { get; set; }
        public string ThemeColor { get; set; } = "#3b82f6";
        public int SortOrder { get; set; } = 0;
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Lightweight DTO for product selection on login and topbar switcher
    /// </summary>
    public class ProductDto
    {
        public int ProductId { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? IconName { get; set; }
        public string ThemeColor { get; set; } = "#3b82f6";
    }
}

