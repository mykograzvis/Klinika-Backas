using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using OdontoKlinika.API.Data;
using OdontoKlinika.API.Models;
using System.Security.Claims;
using OdontoKlinika.API.DTOs;

namespace OdontoKlinika.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Visi metodai reikalauja prisijungimo
    public class VartotojaiController : ControllerBase
    {
        private readonly OdontoDbContext _context;

        public VartotojaiController(OdontoDbContext context)
        {
            _context = context;
        }

        // 1. GAUTI MANO PROFILĮ
        [HttpGet("profilis")]
        public async Task<ActionResult<Vartotojas>> GetManoProfilis()
        {
            // PAKEISTA: Naudojame ClaimTypes.NameIdentifier
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Unauthorized();

            var vartotojas = await _context.Vartotojai.FindAsync(int.Parse(userId));
            if (vartotojas == null) return NotFound();

            return Ok(vartotojas);
        }

        // 2. ATNAUJINTI PROFILĮ
        [HttpPut("atnaujinti")]
        public async Task<IActionResult> UpdateProfilis(ProfilioAtnaujinimoDto dto)
        {
            // PAKEISTA: Naudojame ClaimTypes.NameIdentifier
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdStr == null) return Unauthorized();

            int currentUserId = int.Parse(userIdStr);
            var vartotojas = await _context.Vartotojai.FindAsync(currentUserId);
            if (vartotojas == null) return NotFound();

            vartotojas.Vardas = dto.Vardas;
            vartotojas.Pavarde = dto.Pavarde;
            vartotojas.Telefonas = dto.Telefonas;
            vartotojas.Amzius = dto.Amzius;
            vartotojas.KraujoGrupe = dto.KraujoGrupe;

            if (vartotojas is Gydytojas gydytojas)
            {
                gydytojas.Specializacija = dto.Specializacija ?? gydytojas.Specializacija;
                gydytojas.DarboPatirtisMetais = dto.DarboPatirtisMetais ?? gydytojas.DarboPatirtisMetais;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Profilis sėkmingai atnaujintas" });
        }

        [HttpPost("keisti-slaptazodi")]
        public async Task<IActionResult> KeistiSlaptazodi(SlaptazodžioKeitimoDto dto)
        {
            // PAKEISTA: Naudojame ClaimTypes.NameIdentifier
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdStr == null) return Unauthorized();

            var vartotojas = await _context.Vartotojai.FindAsync(int.Parse(userIdStr));
            if (vartotojas == null) return NotFound();

            if (!BCrypt.Net.BCrypt.Verify(dto.SenasSlaptazodis, vartotojas.SlaptazodisHash))
            {
                return BadRequest("Senas slaptažodis neteisingas.");
            }

            vartotojas.SlaptazodisHash = BCrypt.Net.BCrypt.HashPassword(dto.NaujasSlaptazodis);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Slaptažodis sėkmingai pakeistas." });
        }

        [HttpPost("keisti-el-pasta")]
        public async Task<IActionResult> KeistiElPasta(ElPastoKeitimoDto dto)
        {
            // PAKEISTA: Naudojame ClaimTypes.NameIdentifier
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdStr == null) return Unauthorized();

            if (await _context.Vartotojai.AnyAsync(u => u.ElPastas == dto.NaujasEmail))
                return BadRequest("Šis el. pašto adresas jau naudojamas.");

            var vartotojas = await _context.Vartotojai.FindAsync(int.Parse(userIdStr));
            if (vartotojas == null) return NotFound();

            vartotojas.ElPastas = dto.NaujasEmail;
            await _context.SaveChangesAsync();

            return Ok(new { message = "El. paštas pakeistas. Prisijunkite iš naujo." });
        }

        // --- ADMIN METODAI ---

        [Authorize(Roles = "Adminas")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteVartotojas(int id)
        {
            var vartotojas = await _context.Vartotojai.FindAsync(id);
            if (vartotojas == null) return NotFound("Vartotojas nerastas");

            // PAKEISTA: Naudojame ClaimTypes.NameIdentifier, kad adminas neištrintų savęs
            var adminIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (adminIdStr != null && int.Parse(adminIdStr) == id)
                return BadRequest("Negalite ištrinti savo paskyros.");

            _context.Vartotojai.Remove(vartotojas);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Vartotojas sėkmingai pašalintas iš sistemos." });
        }

        // 1. GAUTI BET KURĮ VARTOTOJĄ (Tik Adminas)
        [Authorize(Roles = "Adminas")]
        [HttpGet("{id}")]
        public async Task<ActionResult<Vartotojas>> GetVartotojas(int id)
        {
            var vartotojas = await _context.Vartotojai.FindAsync(id);
            if (vartotojas == null) return NotFound("Vartotojas nerastas");

            return Ok(vartotojas);
        }

        // 2. ATNAUJINTI BET KURĮ VARTOTOJĄ (Tik Adminas)
        [Authorize(Roles = "Adminas")]
        [HttpPut("admin-atnaujinti/{id}")]
        public async Task<IActionResult> AdminUpdateVartotojas(int id, ProfilioAtnaujinimoDto dto)
        {
            var vartotojas = await _context.Vartotojai.FindAsync(id);
            if (vartotojas == null) return NotFound();

            // Atnaujiname laukus
            vartotojas.Vardas = dto.Vardas;
            vartotojas.Pavarde = dto.Pavarde;
            vartotojas.Telefonas = dto.Telefonas;
            vartotojas.Amzius = dto.Amzius;
            vartotojas.KraujoGrupe = dto.KraujoGrupe;

            // Jei tai gydytojas, atnaujiname specifinius laukus
            if (vartotojas is Gydytojas gydytojas)
            {
                gydytojas.Specializacija = dto.Specializacija ?? gydytojas.Specializacija;
                gydytojas.DarboPatirtisMetais = dto.DarboPatirtisMetais ?? gydytojas.DarboPatirtisMetais;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Vartotojo duomenys sėkmingai atnaujinti admino." });
        }

        [Authorize(Roles = "Adminas, Gydytojas")]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetVartotojai()
        {
            // Gražiname sąrašą vartotojų. 
            // .Select naudojame tam, kad nesiųstume slaptažodžių hash'ų į frontą (saugumas!)
            var vartotojai = await _context.Vartotojai
                .Select(u => new {
                    u.Id,
                    u.Vardas,
                    u.Pavarde,
                    u.ElPastas,
                    u.Role,
                    u.Telefonas
                })
                .ToListAsync();

            return Ok(vartotojai);
        }

        [Authorize(Roles = "Adminas")]
        [HttpPut("admin-atnaujinti-prieiga/{id}")]
        public async Task<IActionResult> AdminUpdateAccess(int id, AdminAccessUpdateDto dto)
        {
            var vartotojas = await _context.Vartotojai.FindAsync(id);
            if (vartotojas == null) return NotFound("Vartotojas nerastas");

            // 1. El. pašto keitimas
            if (!string.IsNullOrEmpty(dto.NaujasEmail))
            {
                // Patikriname, ar toks el. paštas jau neužimtas kito vartotojo
                var egzistuoja = await _context.Vartotojai.AnyAsync(u => u.ElPastas == dto.NaujasEmail && u.Id != id);
                if (egzistuoja) return BadRequest("Šis el. pašto adresas jau naudojamas.");

                vartotojas.ElPastas = dto.NaujasEmail;
            }

            // 2. Slaptažodžio keitimas (priverstinis perrašymas)
            if (!string.IsNullOrEmpty(dto.NaujasSlaptazodis))
            {
                vartotojas.SlaptazodisHash = BCrypt.Net.BCrypt.HashPassword(dto.NaujasSlaptazodis);
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Paskyros prieigos duomenys atnaujinti sėkmingai." });
        }

        [HttpPut("admin-reset-2fa/{id}")]
        [Authorize(Roles = "Adminas")]
        public IActionResult Reset2FA(int id)
        {
            var vartotojas = _context.Vartotojai.Find(id);
            if (vartotojas == null) return NotFound("Vartotojas nerastas.");

            // Išvalome 2FA duomenis
            vartotojas.TwoFactorSecret = null;
            vartotojas.IsTwoFactorEnabled = false;

            _context.SaveChanges();

            return Ok("2FA apsauga vartotojui sėkmingai išjungta.");
        }
    }
}