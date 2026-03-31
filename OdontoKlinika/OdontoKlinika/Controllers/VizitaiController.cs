using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OdontoKlinika.API.Data;
using OdontoKlinika.API.DTOs;
using OdontoKlinika.API.Models;
using OdontoKlinika.API.Services;
using System.Security.Claims;
using Stripe;
using Stripe.Checkout;

namespace OdontoKlinika.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class VizitaiController : ControllerBase
    {
        private readonly OdontoDbContext _context;
        private readonly EmailService _emailService;
        private readonly PdfService _pdfService;
        private readonly IConfiguration _configuration;

        public VizitaiController(OdontoDbContext context, EmailService emailService, IConfiguration configuration, PdfService pdfService)
        {
            _context = context;
            _emailService = emailService;
            _pdfService = pdfService;
            _configuration = configuration;
            StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];
        }

        // 1. Gauti visus vizitus (Adminui/Gydytojui)
        [HttpGet]
        [Authorize(Roles = "Adminas,Gydytojas")]
        public async Task<ActionResult<IEnumerable<object>>> GetVisiVizitai()
        {
            var vizitai = await _context.Vizitai
                .Include(v => v.Pacientas)
                .Include(v => v.Gydytojas)
                .OrderByDescending(v => v.PradziosLaikas)
                .Select(v => new {
                    v.Id,
                    Pacientas = v.Pacientas != null ? v.Pacientas.Vardas + " " + v.Pacientas.Pavarde : "Nepašalintas pacientas",
                    Gydytojas = v.Gydytojas != null ? v.Gydytojas.Vardas + " " + v.Gydytojas.Pavarde : "Nepašalintas gydytojas",
                    v.PradziosLaikas,
                    v.Busena,
                    v.Pastabos,
                    v.Apmoketa
                })
                .ToListAsync();

            return Ok(vizitai);
        }

       [HttpPost("registruotis")]
public async Task<IActionResult> Registruotis(VizitasDto dto)
{
            DateTime pabaiga = dto.PradziosLaikas.AddMinutes(dto.TrukmeMin);

        var uzimta = await _context.Vizitai.AnyAsync(v =>
        v.GydytojasId == dto.GydytojasId &&
        v.Busena != "Atšauktas" &&
        ((dto.PradziosLaikas < v.PabaigosLaikas) && (pabaiga > v.PradziosLaikas)));

    if (uzimta) return BadRequest("Šis laikas arba dalis jo jau užimta.");
    // 2. Sukuriame vizitą
    var vizitas = new Vizitas
    {
        PacientasId = dto.PacientasId,
        GydytojasId = dto.GydytojasId,
        PradziosLaikas = dto.PradziosLaikas,
        PabaigosLaikas = pabaiga,
        Pastabos = dto.Pastabos,
        Busena = "Suplanuotas"
    };

    _context.Vizitai.Add(vizitas);
    
    // SVARBU: Išsaugome, kad gautume vizito ID procedūrai
    await _context.SaveChangesAsync();

    // 3. Iškart sukuriame pirminę procedūrą
    var pirmineProcedura = new Procedura
    {
        VizitasId = vizitas.Id,
        Pavadinimas = dto.ProcedurosPavadinimas,
        Kaina = dto.ProcedurosKaina,
        Aprasymas = "Pirminė registracijos paslauga"
    };

    _context.Proceduros.Add(pirmineProcedura);
    await _context.SaveChangesAsync();

    return Ok(new { message = "Registracija ir paslauga sėkmingai sukurta!" });
}

        // 3. Išsamus sąrašas su procedūromis (3 lygių gylis bakalaurui)
        [HttpGet("issamus-sarasas")]
        [Authorize(Roles = "Adminas,Gydytojas")]
        public async Task<ActionResult<IEnumerable<object>>> GetIssamusVizitai()
        {
            var vizitai = await _context.Vizitai
                .Include(v => v.Pacientas)
                .Include(v => v.Gydytojas)
                .Include(v => v.Proceduros)
                .Select(v => new {
                    v.Id,
                    PacientoVardas = v.Pacientas.Vardas + " " + v.Pacientas.Pavarde,
                    GydytojoVardas = v.Gydytojas.Vardas + " " + v.Gydytojas.Pavarde,
                    v.PradziosLaikas,
                    v.Busena,
                    // 3 lygis: Procedūros
                    AtliktosProceduros = v.Proceduros.Select(p => new {
                        p.Id,
                        p.Pavadinimas,
                        p.Kaina
                    }).ToList(),
                    BendraSuma = v.Proceduros.Any() ? v.Proceduros.Sum(p => p.Kaina) : 0
                })
                .ToListAsync();

            return Ok(vizitai);
        }

        [HttpGet("mano-vizitai")]
        public async Task<ActionResult<IEnumerable<object>>> GetManoVizitai()
        {
            // 1. Gauname prisijungusio vartotojo ID ir Rolę iš Token'o
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized("Nepavyko atpažinti vartotojo.");

            int userId = int.Parse(userIdClaim);
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            IQueryable<Vizitas> query = _context.Vizitai
                .Include(v => v.Pacientas)
                .Include(v => v.Gydytojas)
                .Include(v => v.Proceduros);

            // 2. Filtravimo logika pagal rolę
            if (role == "Pacientas")
            {
                query = query.Where(v => v.PacientasId == userId);
            }
            else if (role == "Gydytojas")
            {
                query = query.Where(v => v.GydytojasId == userId);
            }

            // 3. Formuojame rezultatą su visais reikalingais laukais
            var rezultatas = await query
                .OrderByDescending(v => v.PradziosLaikas)
                .Select(v => new {
                    v.Id,
                    PacientoVardas = v.Pacientas.Vardas + " " + v.Pacientas.Pavarde,
                    GydytojoVardas = v.Gydytojas.Vardas + " " + v.Gydytojas.Pavarde,
                    v.PradziosLaikas,
                    v.Busena,
                    v.Pastabos,
                    // SVARBU: Čia pridedame p.Id
                    AtliktosProceduros = v.Proceduros.Select(p => new {
                        p.Id,             // <--- Dabar Frontend gaus ID trynimui ir raktams (key)
                        p.Pavadinimas,
                        p.Kaina,
                        p.Aprasymas
                    }).ToList(),
                    BendraSuma = v.Proceduros.Sum(p => p.Kaina)
                })
                .ToListAsync();

            return Ok(rezultatas);
        }

        [Authorize(Roles = "Adminas,Gydytojas")]
        [HttpPatch("{id}/uzbaigti")]
        public async Task<IActionResult> Uzbaigti(int id)
        {
            // 1. Gauname vizitą su visais reikiamais duomenimis
            var vizitas = await _context.Vizitai
                .Include(v => v.Pacientas)
                .Include(v => v.Proceduros)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (vizitas == null) return NotFound();

            // 2. Atnaujiname būseną
            vizitas.Busena = "Atliktas";
            _context.Vizitai.Update(vizitas);
            await _context.SaveChangesAsync();

            try
            {
                // 3. Generuojame PDF naudojant PdfService (kurį sukūrėme anksčiau)
                byte[] pdfFailas = _pdfService.GeneruotiSaskaitosPdf(vizitas);

                // 4. Siunčiame laišką su priedu
                await _emailService.SiustiSaskaitaSuPriedu(
                    vizitas.Pacientas.ElPastas,
                    vizitas.Pacientas.Vardas,
                    pdfFailas,
                    vizitas.Id.ToString()
                );
            }
            catch (Exception ex)
            {
                // Log klaida, bet vizitas jau užbaigtas DB
                // Galite grąžinti informaciją, kad laiškas neišsiųstas
                return Ok(new { message = "Vizitas užbaigtas, bet nepavyko išsiųsti el. laiško." });
            }

            return Ok();
        }

        [HttpPut("{id}/atshaukti")]
        [Authorize] // Tik prisijungusiems vartotojams
        public async Task<IActionResult> AtshauktiVizita(int id)
        {
            // 1. Surandame vizitą
            var vizitas = await _context.Vizitai.FindAsync(id);

            if (vizitas == null)
            {
                return NotFound("Vizitas nerastas.");
            }

            // 2. Saugumo patikra: Gauti vartotojo ID iš JWT tokeno
            // Įsitikiname, kad pacientas atšaukia TIK SAVO vizitą
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || vizitas.PacientasId != int.Parse(userIdClaim.Value))
            {
                return Forbid("Galite atšaukti tik savo registracijas.");
            }

            // 3. Verslo logikos patikra: Negalima atšaukti jau įvykusio ar jau atšaukto vizito
            if (vizitas.Busena == "Atliktas")
            {
                return BadRequest("Negalima atšaukti jau įvykusio vizito.");
            }

            if (vizitas.Busena == "Atšauktas")
            {
                return BadRequest("Šis vizitas jau yra atšauktas.");
            }

            // 4. Atšaukimas
            vizitas.Busena = "Atšauktas";

            // Jei vizitas turi susietas procedūras, kurios buvo sukurtos registracijos metu:
            // Galite jas palikti (su vizito būsena "Atšauktas") arba ištrinti.
            // Dažniausiai geriausia palikti istorijai, bet pakeisti vizito būseną.

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { message = "Vizitas sėkmingai atšauktas." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Klaida išsaugant duomenis.");
            }
        }

        [HttpPost("{id}/sukurti-apmokejima")]
        [Authorize]
        public async Task<IActionResult> SukurtiApmokejima(int id)
        {
            // 1. Surandame vizitą ir įtraukiame jo procedūras
            var vizitas = await _context.Vizitai
                .Include(v => v.Proceduros)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (vizitas == null) return NotFound("Vizitas nerastas.");

            // Saugumo patikra: ar vartotojas bando apmokėti savo vizitą
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || vizitas.PacientasId != int.Parse(userIdClaim.Value))
            {
                return Forbid("Galite apmokėti tik savo vizitus.");
            }

            // 2. Stripe konfigūracija (paimama iš appsettings.json)
            StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>(),
                Mode = "payment",
                SuccessUrl = $"http://localhost:3000/istorija?success=true&vizitasId={id}",
                CancelUrl = "http://localhost:3000/istorija?canceled=true",
            };

            // 3. Pridedame procedūras į Stripe krepšelį
            if (vizitas.Proceduros != null && vizitas.Proceduros.Any())
            {
                foreach (var proc in vizitas.Proceduros)
                {
                    options.LineItems.Add(new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            // Stripe reikalauja sumos centais (pvz., 50.00 EUR -> 5000)
                            UnitAmount = (long)(proc.Kaina * 100),
                            Currency = "eur",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = proc.Pavadinimas,
                            },
                        },
                        Quantity = 1,
                    });
                }
            }
            else
            {
                // Jei dėl kokių nors priežasčių procedūrų sąrašas tuščias, bet yra bendra suma
                return BadRequest("Vizitas neturi priskirtų procedūrų, už kurias būtų galima sumokėti.");
            }

            try
            {
                var service = new SessionService();
                Session session = await service.CreateAsync(options);
                return Ok(new { url = session.Url });
            }
            catch (StripeException e)
            {
                // Tai padės pamatyti klaidą tavo serverio konsolėje
                Console.WriteLine($"Stripe klaida: {e.Message}");
                return StatusCode(500, new { message = "Klaida generuojant Stripe sesiją." });
            }
        }

        [HttpGet("patvirtinti-apmokejima/{id}")]
        [Authorize]
        public async Task<IActionResult> PatvirtintiApmokejima(int id)
        {
            var vizitas = await _context.Vizitai.FindAsync(id);
            if (vizitas == null) return NotFound();

            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
            if (vizitas.PacientasId != userId) return Forbid();

            // PAGRINDINIS PAKEITIMAS:
            vizitas.Busena = "Apmokėta";
            // vizitas.Apmoketa = true; // Galite palikti abu, jei DB turi abu laukus

            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPatch("{id}/atsaukti-gydytojas")] // Pakeičiau į Patch, nes keičiame tik būseną
        [Authorize(Roles = "Adminas,Gydytojas")] // Leidžiame gydytojui atšaukti
        public async Task<IActionResult> AtsauktiVizitaGydytojas(int id)
        {
            Console.WriteLine("atejo i metoda");
            // 1. Surandame vizitą kartu su paciento duomenimis
            var vizitas = await _context.Vizitai
                .Include(v => v.Pacientas)
                .Include(v => v.Gydytojas)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (vizitas == null) return NotFound("Vizitas nerastas.");

            // 2. Patikra: ar vizitas jau nėra užbaigtas arba jau atšauktas
            if (vizitas.Busena == "Atliktas")
            {
                return BadRequest("Negalima atšaukti jau įvykusio vizito.");
            }
            if (vizitas.Busena == "Atšauktas")
            {
                return BadRequest("Šis vizitas jau yra atšauktas.");
            }

            Console.WriteLine("iki cia veikia");

            // 3. Pakeičiame būseną
            vizitas.Busena = "Atšauktas";
            Console.WriteLine("busenai keicia");

            try
            {
                await _context.SaveChangesAsync();

                // 4. Siunčiame informacinį laišką pacientui
                if (vizitas.Pacientas != null)
                {
                    string laikas = vizitas.PradziosLaikas.ToString("yyyy-MM-dd HH:mm");
                    string gydytojas = $"{vizitas.Gydytojas?.Vardas} {vizitas.Gydytojas?.Pavarde}";

                    // Sukuriame paprastą pranešimo tekstą (arba galite pridėti naują metodą į EmailService)
                    await _emailService.SiustiPranesima(
                        vizitas.Pacientas.ElPastas,
                        "Atšauktas vizitas - OdontoKlinika",
                        $@"<h3>Gerb. {vizitas.Pacientas.Vardas},</h3>
                   <p>Informuojame, kad jūsų vizitas, suplanuotas <strong>{laikas}</strong> pas gydytoją <strong>{gydytojas}</strong>, buvo atšauktas.</p>
                   <p>Jei turite klausimų, susisiekite su klinikos administracija.</p>"
                    );
                }

                return Ok(new { message = "Vizitas atšauktas, pacientas informuotas el. paštu." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Klaida atšaukiant vizitą: " + ex.Message);
            }
        }

        [HttpGet("{id}/generuoti-pdf")]
        public async Task<IActionResult> DownloadPdf(int id)
        {
            var vizitas = await _context.Vizitai
                .Include(v => v.Pacientas)
                .Include(v => v.Proceduros) // BŪTINA: be šito Sum() visada bus 0
                .FirstOrDefaultAsync(v => v.Id == id);

            if (vizitas == null) return NotFound();

            var pdfBytes = _pdfService.GeneruotiSaskaitosPdf(vizitas);

            return File(pdfBytes, "application/pdf", $"Saskaita_{vizitas.Id}.pdf");
        }

        [HttpPatch("{id}/apmoketi")]
        public async Task<IActionResult> Apmoketi(int id)
        {
            var vizitas = await _context.Vizitai.FindAsync(id);

            if (vizitas == null) return NotFound();

            // Tikriname, ar vizitas yra tokios būsenos, kurią galima apmokėti
            if (vizitas.Busena != "Atliktas")
            {
                return BadRequest("Tik užbaigtas (atliktas) vizitas gali būti pažymėtas kaip apmokėtas.");
            }

            vizitas.Busena = "Apmokėta";
            _context.Vizitai.Update(vizitas);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Vizitas sėkmingai pažymėtas kaip apmokėtas." });
        }

        [HttpPut("{id}/redaguoti")]
        [Authorize]
        public async Task<IActionResult> RedaguotiVizita(int id, [FromBody] VizitasUpdateDto dto)
        {
            // 1. Surandame vizitą kartu su jo procedūromis
            var vizitas = await _context.Vizitai
                .Include(v => v.Proceduros)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (vizitas == null) return NotFound("Vizitas nerastas.");

            // 2. 24 VALANDŲ TAISYKLĖ (Tikriname esamą laiką DB)
            if ((vizitas.PradziosLaikas - DateTime.Now).TotalHours < 24)
            {
                return BadRequest("Vizitą keisti galima likus ne mažiau kaip 24 valandoms iki jo pradžios.");
            }

            // 3. Statuso patikra
            if (vizitas.Apmoketa || vizitas.Busena == "Atliktas")
            {
                return BadRequest("Negalima redaguoti apmokėtų arba jau įvykusių vizitų.");
            }

            // 4. LAIKO ATNAUJINIMAS
            vizitas.PradziosLaikas = dto.PradziosLaikas;
            vizitas.PabaigosLaikas = dto.PradziosLaikas.AddMinutes(dto.TrukmeMin);
            vizitas.GydytojasId = dto.GydytojasId;

            // 5. PROCEDŪRŲ ATNAUJINIMAS
            // Kadangi tavo modelis naudoja List<Procedura>, paprasčiausia yra išvalyti senas 
            // ir pridėti naują (jei keičiasi paslauga)
            _context.Proceduros.RemoveRange(vizitas.Proceduros);

            vizitas.Proceduros = new List<Procedura>
    {
        new Procedura
        {
            Pavadinimas = dto.ProcedurosPavadinimas,
            Kaina = dto.ProcedurosKaina,
            VizitasId = vizitas.Id
        }
    };

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { message = "Vizito laikas ir duomenys sėkmingai atnaujinti." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Klaida atnaujinant vizitą: " + ex.Message);
            }
        }

        [HttpGet("uzimti-laikai")]
        public async Task<IActionResult> GetUzimtiLaikai(int gydytojasId, string data)
        {
            // 1. Sutvarkome datą
            if (!DateTime.TryParse(data, out DateTime pasirinktaData))
                return BadRequest("Neteisingas datos formatas.");

            var dienosPradzia = pasirinktaData.Date;
            var dienosPabaiga = dienosPradzia.AddDays(1);

            // 2. Gydytojas (kad gautume jo ID grafiko lentelėms)
            var gydytojas = await _context.Gydytojai.FindAsync(gydytojasId);
            if (gydytojas == null)
                return NotFound("Gydytojas nerastas.");

            // DarboGrafikas ir DarboIsimtis naudoja string GydytojoId
            var gydytojoVartotojoId = gydytojas.Id.ToString();

            // 3. IŠIMTYS (atostogos, visą dieną nedirba ir pan.)
            var isimtis = await _context.DarboIsimtys
                .FirstOrDefaultAsync(i =>
                    i.GydytojoId == gydytojoVartotojoId &&
                    i.Data.Date == dienosPradzia);

            // Jei nustatyta išimtis ir ArDirba == false – visa diena uždaryta
            if (isimtis != null && !isimtis.ArDirba)
            {
                return Ok(new
                {
                    uzimta = true,
                    zinute = "Gydytojas šią dieną nedirba (išimtis).",
                    laikai = new List<string>()
                });
            }

            // 4. BAZINIS GRAFIKAS (savaitės dienos)
            var savaitesDiena = dienosPradzia.DayOfWeek;

            var grafikas = await _context.DarboGrafikai
                .FirstOrDefaultAsync(g =>
                    g.GydytojoId == gydytojoVartotojoId &&
                    g.SavaitesDiena == savaitesDiena);

            if (grafikas == null || !grafikas.Dirba)
            {
                return Ok(new
                {
                    uzimta = true,
                    zinute = "Gydytojas šią savaitės dieną nedirba.",
                    laikai = new List<string>()
                });
            }

            // 5. Nustatome realias darbo valandas šiai dienai
            // Bazinės valandos
            TimeSpan darboPradzia = !string.IsNullOrEmpty(grafikas.Pradzia)
                ? TimeSpan.Parse(grafikas.Pradzia)
                : TimeSpan.FromHours(0);

            TimeSpan darboPabaiga = !string.IsNullOrEmpty(grafikas.Pabaiga)
                ? TimeSpan.Parse(grafikas.Pabaiga)
                : TimeSpan.FromHours(24);

            // Jei yra išimtis su ArDirba == true ir nurodytomis valandomis – perrašom
            if (isimtis != null &&
                isimtis.ArDirba &&
                !string.IsNullOrEmpty(isimtis.Pradzia) &&
                !string.IsNullOrEmpty(isimtis.Pabaiga))
            {
                darboPradzia = TimeSpan.Parse(isimtis.Pradzia);
                darboPabaiga = TimeSpan.Parse(isimtis.Pabaiga);
            }

            // 6. UŽIMTI SEGMENTAI (30 min žingsniu)
            var uzimtiSegmentai = new HashSet<string>();

            // 6.1. Visi laikai UŽ darbo valandų ribų laikomi užimti
            for (var t = TimeSpan.FromHours(0);
                 t < TimeSpan.FromHours(24);
                 t = t.Add(TimeSpan.FromMinutes(30)))
            {
                if (t < darboPradzia || t >= darboPabaiga)
                {
                    var dt = dienosPradzia.Add(t);
                    uzimtiSegmentai.Add(dt.ToString("HH:mm")); // formatas kaip fronte
                }
            }

            // 6.2. Esami vizitai tą dieną
            var vizitai = await _context.Vizitai
                .Where(v =>
                    v.GydytojasId == gydytojasId &&
                    v.PradziosLaikas >= dienosPradzia &&
                    v.PradziosLaikas < dienosPabaiga &&
                    v.Busena != "Atšauktas")   // naudok tą patį tekstą kaip kituose metoduose
                .ToListAsync();

            foreach (var v in vizitai)
            {
                var temp = v.PradziosLaikas;
                while (temp < v.PabaigosLaikas)
                {
                    uzimtiSegmentai.Add(temp.ToString("HH:mm"));
                    temp = temp.AddMinutes(30);
                }
            }

            // 6.3. PERTRAUKA iš DarboGrafikas
            if (!string.IsNullOrEmpty(grafikas.PertraukaNuo) &&
                !string.IsNullOrEmpty(grafikas.PertraukaIki))
            {
                var pNuo = TimeSpan.Parse(grafikas.PertraukaNuo);
                var pIki = TimeSpan.Parse(grafikas.PertraukaIki);

                var tempPertrauka = dienosPradzia.Add(pNuo);
                var pabaigaPertraukos = dienosPradzia.Add(pIki);

                while (tempPertrauka < pabaigaPertraukos)
                {
                    uzimtiSegmentai.Add(tempPertrauka.ToString("HH:mm"));
                    tempPertrauka = tempPertrauka.AddMinutes(30);
                }
            }

            // 7. Grąžinam tik string sąrašą (pvz. ["08:00","08:30",...])
            return Ok(uzimtiSegmentai.OrderBy(x => x).ToList());
        }

    }
}