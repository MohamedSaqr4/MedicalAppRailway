using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace MedicalApp.Models
{
    public class Doctor
    {
        [Key]
        public int DoctorId { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; }
        public int UserId { get; set; }

        [Required]
        [StringLength(100)]
        public string Specialty { get; set; } = "General"; // Default value

        [Column(TypeName = "decimal(18,2)")]
        public decimal ConsultationFee { get; set; } = 0;

        [StringLength(1000)]
        public string Description { get; set; } = string.Empty;

        public List<DoctorSchedule> Schedules { get; set; } = new();
        public List<Appointment> Appointments { get; set; } = new();
    }
}
