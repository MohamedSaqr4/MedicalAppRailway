using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using MedicalApp.Data;
using MedicalApp.Ddtos;

namespace MedicalApp.Controllers
{
    [Authorize(Roles = "pharmacy")]
    [Route("api/[controller]")]
    [ApiController]
    public class PharmacyController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PharmacyController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/pharmacy/patients
        [HttpGet("patients")]
        public async Task<ActionResult<List<PatientDto>>> GetAllPatients()
        {
            // Get pharmacy UserId from token claims
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int pharmacyUserId))
            {
                return Unauthorized("Invalid user claim");
            }

            // Check if pharmacy exists for current user
            bool pharmacyExists = await _context.Pharmacies.AnyAsync(p => p.UserId == pharmacyUserId);
            if (!pharmacyExists)
            {
                return Unauthorized("Pharmacy not found for user");
            }

            // Fetch patients with their user info
            var patients = await _context.Patients
                .Include(p => p.User)
                .OrderBy(p => p.User.FullName)
                .Select(p => new PatientDto
                {
                    PatientId = p.PatientId,
                    FullName = p.User.FullName,
                    Address = p.User.Address,
                    PhoneNumber = p.User.PhoneNumber
                })
                .ToListAsync();

            return Ok(patients);
        }

        // GET: api/pharmacy/patients-with-orders
        [HttpGet("patients-with-orders")]
        public async Task<ActionResult<List<PharmacyPatientDto>>> GetPatientsWithOrders()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int pharmacyUserId))
            {
                return Unauthorized("Invalid user claim");
            }

            var pharmacy = await _context.Pharmacies
                .FirstOrDefaultAsync(p => p.UserId == pharmacyUserId);

            if (pharmacy == null)
            {
                return Unauthorized("Pharmacy not found for user");
            }

            var patientsWithOrders = await _context.Prescriptions
                .Where(p => p.PharmacyId == pharmacy.PharmacyId)
                .Include(p => p.Patient)
                    .ThenInclude(patient => patient.User)
                .GroupBy(p => p.PatientId)
                .Select(g => new PharmacyPatientDto
                {
                    PatientId = g.Key,
                    FullName = g.First().Patient.User.FullName,
                    Address = g.First().Patient.User.Address,
                    PhoneNumber = g.First().Patient.User.PhoneNumber,
                    LastOrderDate = g.Max(p => p.OrderDate),
                    LastPrescription = g.OrderByDescending(p => p.OrderDate)
                                        .First().PrescriptionDetails
                })
                .OrderBy(p => p.FullName)
                .ToListAsync();

            return Ok(patientsWithOrders);
        }
    }
}
