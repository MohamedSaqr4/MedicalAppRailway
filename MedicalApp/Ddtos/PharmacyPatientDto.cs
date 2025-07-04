namespace MedicalApp.Ddtos
{
    public class PharmacyPatientDto
    {
        public int PatientId { get; set; }
        public string FullName { get; set; }
        public string Address { get; set; }
        public string PhoneNumber { get; set; }
        public DateTime LastOrderDate { get; set; }
        public string LastPrescription { get; set; }
    }
}
