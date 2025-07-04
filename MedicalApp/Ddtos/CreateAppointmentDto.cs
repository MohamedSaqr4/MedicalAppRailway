using System.ComponentModel.DataAnnotations;

namespace MedicalApp.Ddtos
{
    public class CreateAppointmentDto
    {
        [Required]
        public int DoctorId { get; set; }

        [Required]
        public int ScheduleId { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Required]
        public string Type { get; set; } // online or offline
    }
}
