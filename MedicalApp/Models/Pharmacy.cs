using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace MedicalApp.Models
{
    public class Pharmacy
    {
        [Key]
        public int PharmacyId { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; }
        public int UserId { get; set; }
        public ICollection<Prescription> Prescriptions { get; set; }
    }
}
