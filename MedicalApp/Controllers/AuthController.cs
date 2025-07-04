using Microsoft.AspNetCore.Mvc;
using MedicalApp.Ddtos;
using MedicalApp.Models;
using Microsoft.AspNetCore.Identity;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MedicalApp.Data;
using MedicalApp.Ddtos;
using MedicalApp.Models;
using Microsoft.AspNetCore.Authorization;

namespace MedicalApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<Role> _roleManager;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;

        public AuthController(
            UserManager<User> userManager,
            RoleManager<Role> roleManager,
            IConfiguration configuration,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _configuration = configuration;
            _context = context;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDto registerDto)
        {
            // Validate role
            if (registerDto.Role != "patient" && registerDto.Role != "doctor" && registerDto.Role != "pharmacy")
            {
                return BadRequest("Invalid role specified");
            }

            // Check if user exists
            if (await _userManager.FindByEmailAsync(registerDto.Email) != null)
            {
                return BadRequest("Email already exists");
            }

            // Create user
            var user = new User
            {
                FullName = registerDto.FullName,
                UserName = registerDto.Email,
                Email = registerDto.Email,
                PhoneNumber = registerDto.PhoneNumber,
                Address = registerDto.Address,
                Role = registerDto.Role
            };

            var result = await _userManager.CreateAsync(user, registerDto.Password);

            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }

            // Add to role
            if (!await _roleManager.RoleExistsAsync(registerDto.Role))
            {
                await _roleManager.CreateAsync(new Role { Name = registerDto.Role });
            }

            await _userManager.AddToRoleAsync(user, registerDto.Role);

            // Create role-specific entity
            switch (registerDto.Role)
            {
                case "patient":
                    _context.Patients.Add(new Patient { UserId = user.Id });
                    break;

                case "doctor":
                    // Create doctor with default values (to be updated later)
                    _context.Doctors.Add(new Doctor
                    {
                        UserId = user.Id,
                        Specialty = "General", // Default value
                        ConsultationFee = 0    // Default value
                    });
                    break;

                case "pharmacy":
                    _context.Pharmacies.Add(new Pharmacy { UserId = user.Id });
                    break;
            }

            await _context.SaveChangesAsync();

            return Ok(new { Message = "User registered successfully" });
        }

        [HttpPost("login")]
        public async Task<ActionResult<UserDto>> Login(LoginDto loginDto)
        {
            var user = await _userManager.FindByEmailAsync(loginDto.Email);

            if (user == null || !await _userManager.CheckPasswordAsync(user, loginDto.Password))
            {
                return Unauthorized("Invalid email or password");
            }

            var userRoles = await _userManager.GetRolesAsync(user);

            var authClaims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            };

           /* foreach (var userRole in userRoles)
            {
                authClaims.Add(new Claim(ClaimTypes.Role, userRole));
            }*/

            var authSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                expires: DateTime.Now.AddDays(_configuration.GetValue<int>("Jwt:ExpireDays")),
                claims: authClaims,
                signingCredentials: new SigningCredentials(
                    authSigningKey, SecurityAlgorithms.HmacSha256)
            );

            var userDto = new UserDto
            {
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Address = user.Address,
                Role = user.Role,
                Token = new JwtSecurityTokenHandler().WriteToken(token),

            };

            // Add doctor-specific info if user is a doctor
            if (user.Role == "doctor")
            {
                var doctor = await _context.Doctors
                    .FirstOrDefaultAsync(d => d.UserId == user.Id);

                if (doctor != null)
                {
                    userDto.Specialty = doctor.Specialty ?? "General";
                    userDto.ConsultationFee = doctor.ConsultationFee;  // Direct assignment now works
                }
            }

            return Ok(userDto);
        }
    }
}
