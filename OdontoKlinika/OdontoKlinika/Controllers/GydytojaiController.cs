using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using OdontoKlinika.API.Data;
using OdontoKlinika.API.Models;
using OdontoKlinika.API.DTOs; // Įsitikink, kad čia tavo DTO namespace

namespace OdontoKlinika.API.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    [ApiController]
    public class GydytojaiController : ControllerBase
    {
        private readonly OdontoDbContext _context;

        public GydytojaiController(OdontoDbContext context)
        {
            _context = context;
        }

        // GAUTI VISUS (reikės pacientui rezervacijos puslapyje)

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetGydytojai()
        {
            return await _context.Gydytojai
                .Select(g => new { g.Id, g.Vardas, g.Pavarde, g.Specializacija })
                .ToListAsync();
        }

        // SUKURTI NAUJĄ (Tik Adminas)
        [Authorize(Roles = "Adminas")]
        [HttpPost]
        public async Task<ActionResult<Gydytojas>> PostGydytojas(GydytojasDto dto)
        {
            if (await _context.Vartotojai.AnyAsync(u => u.ElPastas == dto.ElPastas))
                return BadRequest("Toks el. paštas jau užimtas.");

            var gydytojas = new Gydytojas
            {
                Vardas = dto.Vardas,
                Pavarde = dto.Pavarde,
                AsmensKodas = dto.AsmensKodas,
                ElPastas = dto.ElPastas,
                Telefonas = dto.Telefonas,
                Amzius = dto.Amzius,
                KraujoGrupe = dto.KraujoGrupe,
                Specializacija = dto.Specializacija,
                DarboPatirtisMetais = dto.DarboPatirtisMetais,
                SlaptazodisHash = BCrypt.Net.BCrypt.HashPassword(dto.Slaptazodis),
                Role = "Gydytojas"
            };

            _context.Gydytojai.Add(gydytojas);

            // Pirmiausia išsaugojam, kad gautume gydytojo ID (jei naudojate Identity arba auto-increment)
            await _context.SaveChangesAsync();

            // SUKURIAME NUMATYTĄJĮ GRAFIKĄ (Pirmadienis-Penktadienis 8:00-17:00)
            var numatytasisGrafikas = new List<DarboGrafikas>();
            for (int i = 0; i < 7; i++)
            {
                bool arDirba = i >= 1 && i <= 5; // Dirba nuo pirmadienio (1) iki penktadienio (5)

                numatytasisGrafikas.Add(new DarboGrafikas
                {
                    GydytojoId = gydytojas.Id.ToString(), // Įsitikinkite, kad Gydytojas turi Id savybę
                    SavaitesDiena = (DayOfWeek)i,
                    Dirba = arDirba,
                    Pradzia = arDirba ? "08:00" : null,
                    Pabaiga = arDirba ? "17:00" : null
                });
            }

            _context.DarboGrafikai.AddRange(numatytasisGrafikas);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Gydytojas ir bazinis darbo grafikas sėkmingai sukurti" });
        }

        // IŠTRINTI (Tik Adminas)
        [Authorize(Roles = "Adminas")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteGydytojas(int id)
        {
            var gydytojas = await _context.Gydytojai.FindAsync(id);
            if (gydytojas == null) return NotFound();

            _context.Gydytojai.Remove(gydytojas);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}