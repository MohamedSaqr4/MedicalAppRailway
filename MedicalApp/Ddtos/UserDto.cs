namespace MedicalApp.Ddtos
{
    public class UserDto
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Address { get; set; }
        public string Role { get; set; }
        public string Token { get; set; }

        // Doctor-specific
        public string Specialty { get; set; } = "General";
        public decimal ConsultationFee { get; set; } = 0;
    }
}
