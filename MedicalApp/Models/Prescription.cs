using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace MedicalApp.Models
{
    public class Prescription
    {
        [Key]
        public int PrescriptionId { get; set; }

        [Required]
        [ForeignKey("PatientId")]
        public Patient Patient { get; set; }
        public int PatientId { get; set; }

        [Required]
        [ForeignKey("PharmacyId")]
        public Pharmacy Pharmacy { get; set; }
        public int PharmacyId { get; set; }

        [Required]
        public string PrescriptionDetails { get; set; }

        public string? AdditionalNotes { get; set; }

        [Required]
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        [Required]
        public string Status { get; set; } = "Pending";
    }
}
