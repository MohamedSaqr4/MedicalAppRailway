namespace MedicalApp.Ddtos
{
    public class AppointmentDto
    {
        public int AppointmentId { get; set; }
        public string PatientName { get; set; }
        public string PatientPhone { get; set; }
        public DateTime AppointmentDate { get; set; }
        public string TimeSlot { get; set; }
        public string Status { get; set; } // booked, completed, cancelled
        public string Type { get; set; } // online, offline
        public string? PaymentStatus { get; set; } // only for online
        public decimal? PaidAmount { get; set; } // only for online
    }
}
