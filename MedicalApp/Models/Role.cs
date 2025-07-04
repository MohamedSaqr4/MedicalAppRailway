using Microsoft.AspNetCore.Identity;

namespace MedicalApp.Models
{
    public class Role : IdentityRole<int>  // Inherit from IdentityRole<int>
    {
        // You can add custom role properties here if needed
        // Example:
        // public string Description { get; set; }
    }
}
