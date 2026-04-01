using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OdontoKlinika.API.Data;
using OdontoKlinika.API.Models;
using OdontoKlinika.API.Services;
using System.Security.Claims;

[Authorize(Roles = "Gydytojas,Adminas")]
[Route("api/[controller]")]
[ApiController]
public class GrafikasController : ControllerBase
{
    private readonly OdontoDbContext _context;
    private readonly EmailService _emailService;

    public GrafikasController(OdontoDbContext context, EmailService emailService)
    {
        _context = context;
        _emailService = emailService;
    }

    // Pagalbinis metodas nustatyti, kurio gydytojo duomenis valdome
    private string GautiTinkamaId(string? nurodytasId)
    {
        var esamasVartotojasId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = User.FindFirstValue(ClaimTypes.Role);

        // Jei vartotojas nėra Adminas, jis gali valdyti TIK savo duomenis
        if (role != "Adminas" || string.IsNullOrEmpty(nurodytasId))
        {
            return esamasVartotojasId;
        }

        return nurodytasId;
    }

    // GAUTI GRAFIKĄ (Adminas gali pridėti ?gydytojoId=... faile)
    [HttpGet("mano")]
    public async Task<IActionResult> GetGrafikas([FromQuery] string? gydytojoId)
    {
        var tikslinisId = GautiTinkamaId(gydytojoId);

        var grafikas = await _context.DarboGrafikai
            .Where(g => g.GydytojoId == tikslinisId)
            .OrderBy(g => g.SavaitesDiena)
            .ToListAsync();
        return Ok(grafikas);
    }

    // ATNAUJINTI BAZINĮ GRAFIKĄ
    [HttpPost("atnaujinti")]
    public async Task<IActionResult> AtnaujintiGrafika(List<DarboGrafikas> naujasGrafikas, [FromQuery] string? gydytojoId)
    {
        var tikslinisId = GautiTinkamaId(gydytojoId);

        var seni = _context.DarboGrafikai.Where(g => g.GydytojoId == tikslinisId);
        _context.DarboGrafikai.RemoveRange(seni);

        foreach (var g in naujasGrafikas)
        {
            g.GydytojoId = tikslinisId;
            _context.DarboGrafikai.Add(g); // Čia EF automatiškai paims PertraukaNuo/Iki
        }

        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("isimtis")]
    [Authorize(Roles = "Adminas,Gydytojas")]
    public async Task<IActionResult> PridetiIsimti([FromBody] DarboIsimtis isimtis)
    {
        if (isimtis == null) return BadRequest("Duomenys negauti.");

        var esamasVartotojasId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = User.FindFirstValue(ClaimTypes.Role);

        if (role != "Adminas")
        {
            isimtis.GydytojoId = esamasVartotojasId;
        }

        // 1. Pridedame pačią išimtį į DB
        var esama = await _context.DarboIsimtys
            .FirstOrDefaultAsync(x => x.GydytojoId == isimtis.GydytojoId && x.Data.Date == isimtis.Data.Date);

        if (esama != null) _context.DarboIsimtys.Remove(esama);
        _context.DarboIsimtys.Add(isimtis);

        // 2. SURANDAME IR ATSAUKIAME VIZITUS
        // Ieškome vizitų, kurie:
        // - Priklauso šiam gydytojui
        // - Yra tą pačią dieną kaip išimtis
        // - Dar nėra atšaukti arba atlikti
        int gydytojoId = int.Parse(isimtis.GydytojoId);

        var vizitaiAtsaukimui = await _context.Vizitai
            .Include(v => v.Pacientas)
            .Include(v => v.Gydytojas)
            .Where(v => v.GydytojasId == gydytojoId &&
                        v.PradziosLaikas.Date == isimtis.Data.Date &&
                        v.Busena != "Atšauktas" &&
                        v.Busena != "Atliktas")
            .ToListAsync();

        int atsauktuKiekis = 0;

        foreach (var vizitas in vizitaiAtsaukimui)
        {
            vizitas.Busena = "Atšauktas";
            atsauktuKiekis++;

            // Siunčiame laišką (naudojame tavo turimą logiką)
            if (vizitas.Pacientas != null && !string.IsNullOrEmpty(vizitas.Pacientas.ElPastas))
            {
                try
                {
                    string laikas = vizitas.PradziosLaikas.ToString("yyyy-MM-dd HH:mm");
                    string gydytojas = $"{vizitas.Gydytojas?.Vardas} {vizitas.Gydytojas?.Pavarde}";

                    await _emailService.SiustiPranesima(
                        vizitas.Pacientas.ElPastas,
                        "Atšauktas vizitas dėl klinikos darbo grafiko pokyčių",
                        $@"<h3>Gerb. {vizitas.Pacientas.Vardas} {vizitas.Pacientas.Pavarde},</h3>
                       <p>Informuojame, kad dėl gydytojo darbo grafiko pasikeitimų jūsų vizitas, 
                       suplanuotas <strong>{laikas}</strong> pas gydytoją <strong>{gydytojas}</strong>, buvo atšauktas.</p>
                       <p>Prašome užsiregistruoti kitam laikui. Atsiprašome už nepatogumus.</p>"
                    );
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Nepavyko išsiųsti laiško: {ex.Message}");
                }
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = $"Išimtis pridėta. Atšaukta vizitų: {atsauktuKiekis}",
            atsauktuKiekis = atsauktuKiekis
        });
    }

    // GAUTI IŠIMTIS
    [HttpGet("isimtys")]
    public async Task<IActionResult> GetIsimtys([FromQuery] string? gydytojoId)
    {
        var tikslinisId = GautiTinkamaId(gydytojoId);
        var dabar = DateTime.Now.Date;

        var isimtys = await _context.DarboIsimtys
            .Where(x => x.GydytojoId == tikslinisId && x.Data >= dabar)
            .OrderBy(x => x.Data)
            .ToListAsync();
        return Ok(isimtys);
    }

    // IŠTRINTI IŠIMTĮ (Labai svarbu adminui/gydytojui)
    [HttpDelete("isimtis/{id}")]
    public async Task<IActionResult> TrintiIsimti(int id)
    {
        var isimtis = await _context.DarboIsimtys.FindAsync(id);
        if (isimtis == null) return NotFound("Išimtis nerasta.");

        // Saugumo patikra (pasirinktinai): 
        // Patikrinkite, ar trina tas pats gydytojas arba adminas
        var role = User.FindFirstValue(ClaimTypes.Role);
        var esamasVartotojasId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (role != "Adminas" && isimtis.GydytojoId != esamasVartotojasId)
        {
            return Forbid("Neturite teisės trinti šio įrašo.");
        }

        _context.DarboIsimtys.Remove(isimtis);
        await _context.SaveChangesAsync();
        return Ok();
    }


}