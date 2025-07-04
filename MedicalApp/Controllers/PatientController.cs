using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MedicalApp.Data;
using MedicalApp.Ddtos;
using MedicalApp.Models;
using System.Security.Claims;
using MedicalApp.Data;
using MedicalApp.Ddtos;

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

        // GET: api/patient/search-doctors
        [HttpGet("search-doctors")]
        public async Task<ActionResult<List<DoctorResultDto>>> SearchDoctors(
            [FromQuery] DoctorSearchDto searchDto)
        {
            var query = _context.Doctors
                .Include(d => d.User)
                .Include(d => d.Schedules)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(searchDto.Specialty))
            {
                query = query.Where(d => d.Specialty.Contains(searchDto.Specialty));
            }

            if (!string.IsNullOrEmpty(searchDto.Location))
            {
                query = query.Where(d => d.User.Address.Contains(searchDto.Location));
            }

            var doctors = await query
                .Select(d => new DoctorResultDto
                {
                    DoctorId = d.DoctorId,
                    Name = d.User.FullName,
                    Specialty = d.Specialty,
                    Description = d.Description,
                    Address = d.User.Address,
                    ConsultationFee = d.ConsultationFee,
                    AvailableSlots = d.Schedules
                        .Where(s => s.IsAvailable)
                        .OrderBy(s => s.DayOfWeek)
                        .Select(s => new AvailabilityDto
                        {
                            ScheduleId = s.ScheduleId,
                            Day = s.DayOfWeek,
                            TimeSlot = $"{s.StartTime:hh\\:mm} - {s.EndTime:hh\\:mm}",
                            NextAvailableDate = GetNextAvailableDate(s.DayOfWeek)
                        })
                        .ToList()
                })
                .ToListAsync();

            return doctors;
        }

        private DateTime GetNextAvailableDate(string dayOfWeek)
        {
            var today = DateTime.Today;
            var targetDay = Enum.Parse<DayOfWeek>(dayOfWeek);
            int daysToAdd = ((int)targetDay - (int)today.DayOfWeek + 7) % 7;
            return today.AddDays(daysToAdd);
        }

        [Authorize(Roles = "patient")]
        [HttpPost("book-appointment")]
        public async Task<ActionResult<AppointmentDto>> BookAppointment(
    [FromBody] CreateAppointmentDto appointmentDto)
        {
            // Validate model
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Get current patient
            var patientId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var patient = await _context.Patients
                .FirstOrDefaultAsync(p => p.PatientId == patientId);

            if (patient == null) return Unauthorized();

            // Verify doctor and schedule exist
            var doctorSchedule = await _context.DoctorSchedules
                .Include(s => s.Doctor)
                .FirstOrDefaultAsync(s => s.ScheduleId == appointmentDto.ScheduleId
                                      && s.DoctorId == appointmentDto.DoctorId);

            if (doctorSchedule == null)
            {
                return BadRequest("Doctor schedule not found");
            }

            // Check schedule availability
            var existingAppointment = await _context.Appointments
                .AnyAsync(a => a.ScheduleId == appointmentDto.ScheduleId
                            && a.Date == appointmentDto.Date
                            && a.Status != "cancelled");

            if (existingAppointment)
            {
                return BadRequest("Time slot already booked");
            }

            // Create new appointment
            var appointment = new Appointment
            {
                PatientId = patientId,
                DoctorId = appointmentDto.DoctorId,
                ScheduleId = appointmentDto.ScheduleId,
                Date = appointmentDto.Date,
                Type = appointmentDto.Type,
                Status = "booked" // Initial status
            };

            // Handle online payment if needed
            if (appointmentDto.Type == "online")
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

            // Return the created appointment
            return CreatedAtAction(nameof(GetAppointment), new { id = appointment.AppointmentId },
                new AppointmentDto
                {
                    AppointmentId = appointment.AppointmentId,
                    PatientName = patient.User.FullName,
                    PatientPhone = patient.User.PhoneNumber,
                    AppointmentDate = appointment.Date,
                    TimeSlot = $"{doctorSchedule.StartTime:hh\\:mm} - {doctorSchedule.EndTime:hh\\:mm}",
                    Status = appointment.Status,
                    Type = appointment.Type,
                    PaymentStatus = appointment.Payment?.Status,
                    PaidAmount = appointment.Payment?.Amount
                });
        }

        [HttpGet("appointments/{id}")]
        public async Task<ActionResult<AppointmentDto>> GetAppointment(int id)
        {
            var patientId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .Include(a => a.Schedule)
                .Include(a => a.Payment)
                .FirstOrDefaultAsync(a => a.AppointmentId == id
                                      && a.PatientId == patientId);

            if (appointment == null) return NotFound();

            return new AppointmentDto
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
                // Same mapping as in BookAppointment
            };
        }

        [HttpGet("search-pharmacies")]
        public async Task<ActionResult<List<PharmacyResultDto>>> SearchPharmacies(
      [FromQuery] PharmacySearchDto searchDto)
        {
            var query = _context.Pharmacies
                .Include(p => p.User)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchDto.Address))
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

            return pharmacies;
        }

        [Authorize(Roles = "patient")]
        [HttpPost("order-from-pharmacy")]
        public async Task<IActionResult> OrderFromPharmacy([FromBody] PatientOrderDto orderDto)
        {
            // Validate model
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Get current patient
            var patientId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var patient = await _context.Patients
                .FirstOrDefaultAsync(p => p.PatientId == patientId);

            if (patient == null) return Unauthorized();

            // Verify pharmacy exists
            var pharmacy = await _context.Pharmacies
                .FirstOrDefaultAsync(p => p.PharmacyId == orderDto.PharmacyId);

            if (pharmacy == null)
            {
                return BadRequest("Pharmacy not found");
            }

            // Create new prescription order
            var prescription = new Prescription
            {
                PatientId = patientId,
                PharmacyId = orderDto.PharmacyId,
                PrescriptionDetails = orderDto.PrescriptionDetails,
                AdditionalNotes = orderDto.AdditionalNotes,
                Status = "Pending"
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
