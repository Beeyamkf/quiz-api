using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using QuizAPI.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly ITeacherRepository _repo;
    private readonly IConfiguration _config;

    public AuthController(ITeacherRepository repo, IConfiguration config)
    {
        _repo = repo;
        _config = config;
    }
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterTeacherRequest req)
    {
        try
        {
            var existing = await _repo.GetByEmailAsync(req.Email);

            if (existing != null)
                return BadRequest("Email already exists");

            var teacher = new Teacher
            {
                FullName = req.FullName,
                Email = req.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password)
            };

            var id = await _repo.RegisterAsync(teacher);

            return Ok(new { message = "User created", id });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                error = ex.Message,
                stack = ex.StackTrace
            });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var teacher = await _repo.GetByEmailAsync(req.Email);

        if (teacher == null)
            return Unauthorized("Invalid email");

        bool isValid = BCrypt.Net.BCrypt.Verify(req.Password, teacher.PasswordHash);

        if (!isValid)
            return Unauthorized("Invalid password");

        var token = GenerateJwtToken(teacher);

        return Ok(new { token });
    }

    // =====================
    // JWT GENERATION
    // =====================
    private string GenerateJwtToken(Teacher teacher)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"])
        );

        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, teacher.FullName),
            new Claim(ClaimTypes.Email, teacher.Email),
            new Claim("TeacherId", teacher.Id.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.Now.AddHours(2),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}