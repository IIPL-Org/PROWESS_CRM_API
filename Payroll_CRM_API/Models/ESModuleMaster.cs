using System.ComponentModel.DataAnnotations;

namespace Payroll_CRM_API.Models
{
    public class ESModuleMaster
    {
        public int ModuleId { get; set; }
        public string ModuleName { get; set; } = string.Empty;
        public string ShortName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public char IsActive { get; set; } = 'Y';
    }

    public class AssignESModulesDto
    {
        [Required(ErrorMessage = "Client ID is required.")]
        public int ClientId { get; set; }

        public List<int> ModuleIds { get; set; } = new List<int>();
    }
}
