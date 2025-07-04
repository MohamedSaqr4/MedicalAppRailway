namespace MedicalApp.Ddtos
{
    public class DoctorResultDto
    {
        public int DoctorId { get; set; }
        public string Name { get; set; }
        public string Specialty { get; set; }
        public string Description { get; set; }
        public string Address { get; set; }
        public decimal ConsultationFee { get; set; }
        public List<AvailabilityDto> AvailableSlots { get; set; } = new();
    }

    public class AvailabilityDto
    {
        public int ScheduleId { get; set; }
        public string Day { get; set; } // e.g., "Monday"
        public string TimeSlot { get; set; } // e.g., "09:00 - 12:00"
        public DateTime NextAvailableDate { get; set; }
    }
}
