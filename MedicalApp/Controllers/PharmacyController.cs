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
            // Verify pharmacy authentication
            var pharmacyUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var pharmacyExists = await _context.Pharmacies
                .AnyAsync(p => p.UserId == pharmacyUserId);

            if (!pharmacyExists) return Unauthorized();

            // Return all patients in the system
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

            return patients;
        }
        [HttpGet("patients-with-orders")]
        public async Task<ActionResult<List<PharmacyPatientDto>>> GetPatientsWithOrders()
        {
            var pharmacyUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var pharmacy = await _context.Pharmacies
                .FirstOrDefaultAsync(p => p.UserId == pharmacyUserId);

            if (pharmacy == null) return Unauthorized();

            var patients = await _context.Prescriptions
                .Where(p => p.PharmacyId == pharmacy.PharmacyId)
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

            return patients;
        }
    }
}