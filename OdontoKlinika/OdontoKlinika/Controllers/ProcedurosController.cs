using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using OdontoKlinika.API.Data;
using OdontoKlinika.API.Models;
using OdontoKlinika.API.DTOs;

namespace OdontoKlinika.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Gydytojas,Adminas")]
    public class ProcedurosController : ControllerBase
    {
        private readonly OdontoDbContext _context;

        public ProcedurosController(OdontoDbContext context)
        {
            _context = context;
        }

        // 1. PRIDĖTI NAUJĄ
        [HttpPost]
        public async Task<IActionResult> Create(ProceduraDto dto)
        {
            var vizitas = await _context.Vizitai.FindAsync(dto.VizitasId);
            if (vizitas == null) return NotFound("Vizitas nerastas.");

            var procedura = new Procedura
            {
                VizitasId = dto.VizitasId,
                Pavadinimas = dto.Pavadinimas,
                Kaina = dto.Kaina,
                Aprasymas = dto.Aprasymas
            };

            _context.Proceduros.Add(procedura);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Procedūra pridėta" });
        }

        // 2. REDAGUOTI ESAMĄ
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, ProceduraDto dto)
        {
            var procedura = await _context.Proceduros.FindAsync(id);
            if (procedura == null) return NotFound("Procedūra nerasta.");

            procedura.Pavadinimas = dto.Pavadinimas;
            procedura.Kaina = dto.Kaina;
            procedura.Aprasymas = dto.Aprasymas;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Procedūra atnaujinta" });
        }

        // 3. IŠTRINTI
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var procedura = await _context.Proceduros.FindAsync(id);
            if (procedura == null) return NotFound("Procedūra nerasta.");

            _context.Proceduros.Remove(procedura);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Procedūra pašalinta" });
        }
    }
}