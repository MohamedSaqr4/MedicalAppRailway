using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace MedicalApp.Models
{
    public class Appointment
    {
        [Key]
        public int AppointmentId { get; set; }

        [ForeignKey("PatientId")]
        public Patient Patient { get; set; }
        public int PatientId { get; set; }

        [ForeignKey("DoctorId")]
        public Doctor Doctor { get; set; }
        public int DoctorId { get; set; }

        [ForeignKey("ScheduleId")]
        public DoctorSchedule Schedule { get; set; }
        public int ScheduleId { get; set; }

        public DateTime Date { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } // booked, completed, cancelled

        [Required]
        [StringLength(10)]
        public string Type { get; set; } // online, offline

        [ForeignKey("PaymentId")]
        public Payment? Payment { get; set; }
        public int? PaymentId { get; set; }

    }
}
