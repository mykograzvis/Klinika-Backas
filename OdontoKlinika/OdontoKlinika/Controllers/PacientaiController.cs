using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OdontoKlinika.API.Data;
using OdontoKlinika.API.Models;

namespace OdontoKlinika.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PacientaiController : ControllerBase
    {
        private readonly OdontoDbContext _context;

        public PacientaiController(OdontoDbContext context)
        {
            _context = context;
        }

        // GAUTI VISUS: api/pacientai
        [HttpGet]
        [Authorize(Roles = "Adminas")]
        public async Task<ActionResult<IEnumerable<Pacientas>>> GetPacientai()
        {
            return await _context.Pacientai.ToListAsync();
        }

        // REGISTRUOTI NAUJĄ: api/pacientai
        [HttpPost]
        public async Task<ActionResult<Pacientas>> PostPacientas(Pacientas pacientas)
        {
            _context.Pacientai.Add(pacientas);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetPacientai), new { id = pacientas.Id }, pacientas);
        }

        [HttpGet("profilis")]
        [Authorize] // Reikia bet kokio galiojančio žetono
        public IActionResult GetProfilis()
        {
            // Galime ištraukti informaciją tiesiai iš žetono
            var vardas = User.Identity?.Name;
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

            return Ok(new { Zinute = $"Sveiki, {vardas}, jūsų rolė yra {role}" });
        }
    }
}