using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace MedicalApp.Models
{
    public class Payment
    {
        [Key]
        public int PaymentId { get; set; }

        [Column(TypeName = "decimal(10, 2)")]
        public decimal Amount { get; set; }

        [Required]
        [StringLength(100)]
        public string TransactionId { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } // pending, completed, failed

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Navigation property back to Appointment (1:1)
        public Appointment? Appointment { get; set; }
    }
}
