using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace MedicalApp.Models
{
    public class User : IdentityUser<int>
    {
        [Required]
        [StringLength(100)]
        public string FullName { get; set; }

        [Required]
        public string Address { get; set; }

        [Required]
        public string Role { get; set; } // patient, doctor, pharmacy
    }
}
