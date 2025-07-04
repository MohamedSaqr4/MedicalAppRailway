using System.ComponentModel.DataAnnotations;

namespace MedicalApp.Ddtos
{
    public class DoctorSetupDto
    {
        [Required]
        public string specialty { get; set; }

        [Required]
        [Range(0, 10000)]
        public decimal consultationFee { get; set; }
    }
}
