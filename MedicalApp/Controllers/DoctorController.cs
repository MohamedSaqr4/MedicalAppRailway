using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MedicalApp.Data;
using MedicalApp.Ddtos;
using MedicalApp.Models;
using System.Security.Claims;
using MedicalApp.Data;
using MedicalApp.Ddtos;
using MedicalApp.Models;

namespace MedicalApp.Controllers
{
    [Authorize(Roles = "doctor")]
    [Route("api/[controller]")]
    [ApiController]
    public class DoctorController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DoctorController> _logger;


        public DoctorController(ApplicationDbContext context, ILogger<DoctorController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private int GetCurrentDoctorId()
        {
            // 1. Get User ID from claims
            var userIdClaim = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")
                           ?? User.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                _logger.LogWarning("User ID not found or invalid in claims");
                return 0;
            }

            // 2. Check Doctor Role
            var roleClaim = User.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
                         ?? User.FindFirst(ClaimTypes.Role);

            if (roleClaim == null || roleClaim.Value != "doctor")
            {
                _logger.LogWarning("User doesn't have doctor role");
                return 0;
            }

            // 3. Get DoctorId from database
            return _context.Doctors
                .Where(d => d.UserId == userId)
                .Select(d => d.DoctorId)
                .FirstOrDefault();
        }

        // GET: api/doctor/my-profile
        [HttpGet("my-profile")]
        public async Task<ActionResult<DoctorProfileDto>> GetMyProfile()
        {
            var doctorId = GetCurrentDoctorId();
            if (doctorId == 0) return Unauthorized();

            var doctor = await _context.Doctors
                .Include(d => d.User)
                .Include(d => d.Schedules)
                .FirstOrDefaultAsync(d => d.DoctorId == doctorId);

            if (doctor == null) return NotFound();

            return new DoctorProfileDto
            {
                Specialty = doctor.Specialty,
                ConsultationFee = doctor.ConsultationFee,
                Description = doctor.Description,
                Schedules = doctor.Schedules.Select(s => new DoctorScheduleDto
                {
                    DayOfWeek = s.DayOfWeek,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime
                }).ToList()
            };
        }

        // PUT: api/doctor/my-profile
        [Authorize(Roles = "doctor")]
        [HttpPut("my-profile")]
        public async Task<IActionResult> UpdateMyProfile([FromBody] DoctorProfileDto profileDto) // Changed from [FromForm]
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var doctorId = GetCurrentDoctorId();
            if (doctorId == 0) return Unauthorized();

            var doctor = await _context.Doctors
                .Include(d => d.Schedules)
                .FirstOrDefaultAsync(d => d.DoctorId == doctorId);

            if (doctor == null) return NotFound();

            // Update profile info (no picture handling)
            doctor.Specialty = profileDto.Specialty;
            doctor.ConsultationFee = profileDto.ConsultationFee;
            doctor.Description = profileDto.Description;

            // Update schedules
            _context.DoctorSchedules.RemoveRange(doctor.Schedules);

            foreach (var schedule in profileDto.Schedules)
            {
                doctor.Schedules.Add(new DoctorSchedule
                {
                    DayOfWeek = schedule.DayOfWeek,
                    StartTime = schedule.StartTime,
                    EndTime = schedule.EndTime,
                    IsAvailable = true
                });
            }

            await _context.SaveChangesAsync();
            return Ok(new { Message = "Profile updated successfully" });
        }

        // GET: api/doctor/appointments
        [HttpGet("appointments")]
        public async Task<ActionResult<List<AppointmentDto>>> GetAppointments(
            [FromQuery] string? status = null,
            [FromQuery] string? type = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null)
        {
            var doctorId = GetCurrentDoctorId();
            if (doctorId == 0) return Unauthorized();

            var query = _context.Appointments
                .Where(a => a.DoctorId == doctorId)
                .Include(a => a.Patient)
                    .ThenInclude(p => p.User)
                .Include(a => a.Payment)
                .Include(a => a.Schedule)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(a => a.Status == status);
            }

            if (!string.IsNullOrEmpty(type))
            {
                query = query.Where(a => a.Type == type);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(a => a.Date >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                query = query.Where(a => a.Date <= toDate.Value.Date);
            }

            var appointments = await query
                .OrderBy(a => a.Date)
                .ThenBy(a => a.Schedule.StartTime)
                .Select(a => new AppointmentDto
                {
                    AppointmentId = a.AppointmentId,
                    PatientName = a.Patient.User.FullName,
                    PatientPhone = a.Patient.User.PhoneNumber,
                    AppointmentDate = a.Date,
                    TimeSlot = $"{a.Schedule.StartTime:hh\\:mm} - {a.Schedule.EndTime:hh\\:mm}",
                    Status = a.Status,
                    Type = a.Type,
                    PaymentStatus = a.Payment != null ? a.Payment.Status : null,
                    PaidAmount = a.Payment != null ? a.Payment.Amount : null
                })
                .ToListAsync();

            return appointments;
        }
    }
}
