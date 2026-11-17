namespace Payroll_CRM_API.Models
{
    public class ModuleMaster
    {
        public int ModuleId { get; set; }
        public string ModuleName { get; set; } = string.Empty;
        public string ShortName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public char IsActive { get; set; } = 'Y';
    }
}
