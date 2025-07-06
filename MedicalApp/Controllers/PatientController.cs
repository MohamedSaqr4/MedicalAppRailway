using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MedicalApp.Data;
using MedicalApp.Ddtos;
using MedicalApp.Models;
using System.Security.Claims;

namespace MedicalApp.Controllers
{
    [Authorize(Roles = "patient")]
    [Route("api/[controller]")]
    [ApiController]
    public class PatientController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PatientController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("search-doctors")]
        public async Task<ActionResult<List<DoctorResultDto>>> SearchDoctors([FromQuery] DoctorSearchDto searchDto)
        {
            var query = _context.Doctors
                .Include(d => d.User)
                .Include(d => d.Schedules)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchDto.Specialty))
            {
                query = query.Where(d => d.Specialty.Contains(searchDto.Specialty));
            }

            if (!string.IsNullOrWhiteSpace(searchDto.Location))
            {
                query = query.Where(d => d.User.Address.Contains(searchDto.Location));
            }

            var doctorList = await query
                .Select(d => new
                {
                    d.DoctorId,
                    d.User.FullName,
                    d.Specialty,
                    d.Description,
                    d.User.Address,
                    d.ConsultationFee,
                    Schedules = d.Schedules
                        .Where(s => s.IsAvailable)
                        .OrderBy(s => s.DayOfWeek)
                        .Select(s => new
                        {
                            s.ScheduleId,
                            s.DayOfWeek,
                            s.StartTime,
                            s.EndTime
                        }).ToList()
                })
                .ToListAsync();

            var doctors = doctorList.Select(d => new DoctorResultDto
            {
                DoctorId = d.DoctorId,
                Name = d.FullName,
                Specialty = d.Specialty,
                Description = d.Description,
                Address = d.Address,
                ConsultationFee = d.ConsultationFee,
                AvailableSlots = d.Schedules.Select(s => new AvailabilityDto
                {
                    ScheduleId = s.ScheduleId,
                    Day = s.DayOfWeek,
                    TimeSlot = $"{s.StartTime:hh\\:mm} - {s.EndTime:hh\\:mm}",
                    NextAvailableDate = GetNextAvailableDate(s.DayOfWeek)
                }).ToList()
            }).ToList();

            return Ok(doctors);
        }

        private static DateTime GetNextAvailableDate(string dayOfWeek)
        {
            var today = DateTime.Today;
            if (!Enum.TryParse<DayOfWeek>(dayOfWeek, out var targetDay))
                targetDay = DayOfWeek.Monday;

            int daysToAdd = ((int)targetDay - (int)today.DayOfWeek + 7) % 7;
            return today.AddDays(daysToAdd);
        }

        [HttpPost("book-appointment")]
        public async Task<ActionResult<AppointmentDto>> BookAppointment([FromBody] CreateAppointmentDto appointmentDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
            {
                return Unauthorized("Invalid patient claim");
            }

            var patient = await _context.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (patient == null)
                return Unauthorized("Patient not found");

            var doctorSchedule = await _context.DoctorSchedules
                .Include(s => s.Doctor)
                .FirstOrDefaultAsync(s => s.ScheduleId == appointmentDto.ScheduleId && s.DoctorId == appointmentDto.DoctorId);

            if (doctorSchedule == null)
            {
                return BadRequest("Doctor schedule not found");
            }

            bool isBooked = await _context.Appointments
                .AnyAsync(a => a.ScheduleId == appointmentDto.ScheduleId &&
                               a.Date.Date == appointmentDto.Date.Date &&
                               a.Status != "cancelled");

            if (isBooked)
            {
                return BadRequest("Time slot already booked");
            }

            var appointment = new Appointment
            {
                PatientId = patient.PatientId,
                DoctorId = appointmentDto.DoctorId,
                ScheduleId = appointmentDto.ScheduleId,
                Date = appointmentDto.Date.Date,
                Type = appointmentDto.Type,
                Status = "booked"
            };

            if (appointmentDto.Type?.ToLower() == "online")
            {
                var payment = new Payment
                {
                    Amount = doctorSchedule.Doctor.ConsultationFee,
                    Status = "pending",
                    TransactionId = Guid.NewGuid().ToString()
                };

                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();

                appointment.PaymentId = payment.PaymentId;
            }

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            var appointmentDtoResponse = new AppointmentDto
            {
                AppointmentId = appointment.AppointmentId,
                PatientName = patient.User?.FullName ?? "N/A",
                PatientPhone = patient.User?.PhoneNumber ?? "N/A",
                AppointmentDate = appointment.Date,
                TimeSlot = $"{doctorSchedule.StartTime:hh\\:mm} - {doctorSchedule.EndTime:hh\\:mm}",
                Status = appointment.Status,
                Type = appointment.Type,
                PaymentStatus = appointment.Payment?.Status,
                PaidAmount = appointment.Payment?.Amount
            };

            return CreatedAtAction(nameof(GetAppointment), new { id = appointment.AppointmentId }, appointmentDtoResponse);
        }

        [HttpGet("appointments/{id}")]
        public async Task<ActionResult<AppointmentDto>> GetAppointment(int id)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
            {
                return Unauthorized("Invalid patient claim");
            }

            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.UserId == userId);
            if (patient == null)
                return Unauthorized("Patient not found");

            var appointment = await _context.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor)
                .Include(a => a.Schedule)
                .Include(a => a.Payment)
                .FirstOrDefaultAsync(a => a.AppointmentId == id && a.PatientId == patient.PatientId);

            if (appointment == null)
            {
                return NotFound();
            }

            var appointmentDto = new AppointmentDto
            {
                AppointmentId = appointment.AppointmentId,
                PatientName = appointment.Patient.User.FullName,
                PatientPhone = appointment.Patient.User.PhoneNumber,
                AppointmentDate = appointment.Date,
                TimeSlot = $"{appointment.Schedule.StartTime:hh\\:mm} - {appointment.Schedule.EndTime:hh\\:mm}",
                Status = appointment.Status,
                Type = appointment.Type,
                PaymentStatus = appointment.Payment?.Status,
                PaidAmount = appointment.Payment?.Amount
            };

            return Ok(appointmentDto);
        }

        [HttpGet("search-pharmacies")]
        public async Task<ActionResult<List<PharmacyResultDto>>> SearchPharmacies([FromQuery] PharmacySearchDto searchDto)
        {
            var query = _context.Pharmacies.Include(p => p.User).AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchDto.Address))
            {
                query = query.Where(p => p.User.Address.Contains(searchDto.Address));
            }

            var pharmacies = await query
                .Select(p => new PharmacyResultDto
                {
                    PharmacyId = p.PharmacyId,
                    Name = p.User.FullName,
                    Address = p.User.Address,
                    Phone = p.User.PhoneNumber
                })
                .ToListAsync();

            return Ok(pharmacies);
        }

        [HttpPost("order-from-pharmacy")]
        public async Task<IActionResult> OrderFromPharmacy([FromBody] PatientOrderDto orderDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
            {
                return Unauthorized("Invalid patient claim");
            }

            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.UserId == userId);
            if (patient == null)
            {
                return Unauthorized("Patient not found");
            }

            var pharmacy = await _context.Pharmacies
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.PharmacyId == orderDto.PharmacyId);

            if (pharmacy == null)
            {
                return BadRequest("Pharmacy not found");
            }

            var prescription = new Prescription
            {
                PatientId = patient.PatientId,
                PharmacyId = pharmacy.PharmacyId,
                PrescriptionDetails = orderDto.PrescriptionDetails,
                AdditionalNotes = orderDto.AdditionalNotes,
                Status = "Pending",
                OrderDate = DateTime.UtcNow
            };

            _context.Prescriptions.Add(prescription);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                Message = "Prescription order submitted successfully",
                OrderId = prescription.PrescriptionId,
                PharmacyName = pharmacy.User.FullName
            });
        }
    }
}
