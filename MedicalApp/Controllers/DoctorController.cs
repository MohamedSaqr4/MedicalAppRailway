using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MedicalApp.Data;
using MedicalApp.Ddtos;
using MedicalApp.Models;
using System.Security.Claims;

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
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                _logger.LogWarning("User ID claim missing or invalid");
                return 0;
            }

            var doctor = _context.Doctors.FirstOrDefault(d => d.UserId == userId);
            return doctor?.DoctorId ?? 0;
        }

        [HttpGet("my-profile")]
        public async Task<ActionResult<DoctorProfileDto>> GetMyProfile()
        {
            var doctorId = GetCurrentDoctorId();
            if (doctorId == 0) return Unauthorized();

            var doctor = await _context.Doctors
                .Include(d => d.User)
                .Include(d => d.Schedules)
                .AsNoTracking()
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

        [HttpPut("my-profile")]
        public async Task<IActionResult> UpdateMyProfile([FromBody] DoctorProfileDto profileDto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var doctorId = GetCurrentDoctorId();
            if (doctorId == 0) return Unauthorized();

            var doctor = await _context.Doctors
                .Include(d => d.Schedules)
                .FirstOrDefaultAsync(d => d.DoctorId == doctorId);

            if (doctor == null) return NotFound();

            // Update basic profile fields
            doctor.Specialty = profileDto.Specialty;
            doctor.ConsultationFee = profileDto.ConsultationFee;
            doctor.Description = profileDto.Description;

            // Remove old schedules
            _context.DoctorSchedules.RemoveRange(doctor.Schedules);
            await _context.SaveChangesAsync();

            // Add new schedules
            var newSchedules = profileDto.Schedules.Select(s => new DoctorSchedule
            {
                DoctorId = doctorId,
                DayOfWeek = s.DayOfWeek,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                IsAvailable = true
            }).ToList();

            await _context.DoctorSchedules.AddRangeAsync(newSchedules);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Profile updated successfully" });
        }

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
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Schedule)
                .Include(a => a.Payment)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(a => a.Status == status);

            if (!string.IsNullOrWhiteSpace(type))
                query = query.Where(a => a.Type == type);

            if (fromDate.HasValue)
                query = query.Where(a => a.Date.Date >= fromDate.Value.Date);

            if (toDate.HasValue)
                query = query.Where(a => a.Date.Date <= toDate.Value.Date);

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
                    PaymentStatus = a.Payment.Status,
                    PaidAmount = a.Payment.Amount
                })
                .ToListAsync();

            return appointments;
        }
    }
}
