using System.ComponentModel.DataAnnotations;

namespace MedicalApp.Ddtos
{
    public class PatientOrderDto
    {
        [Required]
        public int PharmacyId { get; set; }

        [Required]
        public string PrescriptionDetails { get; set; }

        public string? AdditionalNotes { get; set; }
    }
}
