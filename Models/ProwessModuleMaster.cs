using System.ComponentModel.DataAnnotations;

namespace PROWESS_CRM.Models
{
    public class ProwessModuleMaster
    {
        public int ModuleId { get; set; }
        public string ModuleName { get; set; } = string.Empty;
        public int? SerialCode { get; set; }
        public DateTime? CreatedDate { get; set; }
        public char IsActive { get; set; } = 'Y';
        public char? Flag { get; set; }
        public string? Pathname { get; set; }
        public string? IconsName { get; set; }
        public string? ModulePrefix { get; set; }
        public string ShortName { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class AssignProwessModulesDto
    {
        [Required(ErrorMessage = "Client ID is required.")]
        public int ClientId { get; set; }

        public List<int> ModuleIds { get; set; } = new List<int>();
    }
}

