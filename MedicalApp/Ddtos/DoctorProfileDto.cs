using System.ComponentModel.DataAnnotations;

namespace MedicalApp.Ddtos
{
    public class DoctorProfileDto
    {
        [Required]
        public string Specialty { get; set; }

        [Required]
        [Range(0, 10000)]
        public decimal ConsultationFee { get; set; }

        [Required]
        [StringLength(1000)]
        public string Description { get; set; }

        [Required]
        [ValidateScheduleList]
        public List<DoctorScheduleDto> Schedules { get; set; } = new();
    }

    public class DoctorScheduleDto
    {
        [Required]
        public string DayOfWeek { get; set; } // "Monday", "Tuesday", etc.

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }
    }

    // Custom validation attribute for schedules
    public class ValidateScheduleListAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext context)
        {
            var schedules = value as List<DoctorScheduleDto>;
            if (schedules == null || !schedules.Any())
            {
                return new ValidationResult("At least one schedule entry is required");
            }

            // Check for overlapping schedules
            for (int i = 0; i < schedules.Count; i++)
            {
                for (int j = i + 1; j < schedules.Count; j++)
                {
                    if (schedules[i].DayOfWeek == schedules[j].DayOfWeek &&
                        schedules[i].StartTime < schedules[j].EndTime &&
                        schedules[i].EndTime > schedules[j].StartTime)
                    {
                        return new ValidationResult($"Schedule {i + 1} overlaps with schedule {j + 1}");
                    }
                }
            }

            return ValidationResult.Success;
        }
    }
}
