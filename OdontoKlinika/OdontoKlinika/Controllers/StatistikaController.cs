using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OdontoKlinika.API.Data;
using OdontoKlinika.API.DTOs;
using OdontoKlinika.API.Models;
using OdontoKlinika.API.Services;
using System.Security.Claims;

namespace OdontoKlinika.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Adminas")]
    public class StatistikaController : ControllerBase
    {
        private readonly OdontoDbContext _context;

        public StatistikaController(OdontoDbContext context)
        {
            _context = context;
        }

        [HttpGet("statistika")]
        public async Task<IActionResult> GetStatistika()
        {
            var visiVizitai = await _context.Vizitai
                .Include(v => v.Proceduros)
                .ToListAsync();

            var statistika = new
            {
                BendraPajamuSuma = visiVizitai.Sum(v => v.Proceduros.Sum(p => p.Kaina)),
                VisoVizitu = visiVizitai.Count,
                AtliktiVizitai = visiVizitai.Count(v => v.Busena == "Atliktas"),
                LaukiaVizitu = visiVizitai.Count(v => v.Busena == "Suplanuotas"),

                // Sugrupuojame pajamas pagal mėnesius (paskutinių 6 mėn.)
                PajamosPerMenesi = visiVizitai
                    .Where(v => v.Busena == "Atliktas")
                    .GroupBy(v => v.PradziosLaikas.Month)
                    .Select(g => new {
                        Menesis = g.Key,
                        Suma = g.Sum(v => v.Proceduros.Sum(p => p.Kaina))
                    })
                    .OrderBy(x => x.Menesis)
            };

            return Ok(statistika);
        }

        [HttpGet("analize")]
        [Authorize(Roles = "Adminas")]
        public async Task<IActionResult> GetAnalize(int? metai, int? menesis)
        {
            var dabar = DateTime.Now;
            int pasirinktiMetai = metai ?? dabar.Year;
            int pasirinktasMenesis = menesis ?? dabar.Month;

            // Į analizę traukiame visus vizitus, kurie nėra atšaukti,
            // pasirinktų metų ir mėnesio ribose
            var menesioVizitai = await _context.Vizitai
                .Include(v => v.Gydytojas)
                .Include(v => v.Proceduros)
                .Where(v =>
                    v.Busena != "Atšauktas" &&            // pritaikyk tikslų tekstą
                    v.PradziosLaikas.Year == pasirinktiMetai &&
                    v.PradziosLaikas.Month == pasirinktasMenesis)
                .ToListAsync();

            // Debug: jeigu nori, kurį laiką palik šitą eilutę
            Console.WriteLine($"Analize: rasta vizitų = {menesioVizitai.Count}");

            var bendraSuma = menesioVizitai.Sum(v => v.Proceduros.Sum(p => p.Kaina));
            var pacientuSkaicius = menesioVizitai
                .Select(v => v.PacientasId)
                .Distinct()
                .Count();

            var gydytojuEfektyvumas = menesioVizitai
                .Where(v => v.Gydytojas != null)
                .GroupBy(v => v.Gydytojas.Vardas + " " + v.Gydytojas.Pavarde)
                .Select(g => new
                {
                    Vardas = g.Key,
                    Pajamos = g.Sum(v => v.Proceduros.Sum(p => p.Kaina)),
                    Vizitai = g.Count()
                })
                .OrderByDescending(x => x.Pajamos)
                .ToList();

            var topProceduros = menesioVizitai
                .SelectMany(v => v.Proceduros)
                .GroupBy(p => p.Pavadinimas)
                .Select(g => new
                {
                    Pavadinimas = g.Key,
                    Kiekis = g.Count(),
                    Suma = g.Sum(p => p.Kaina)
                })
                .OrderByDescending(x => x.Kiekis)
                .Take(5)
                .ToList();

            var statistika = new
            {
                Menuo = pasirinktasMenesis,
                Metai = pasirinktiMetai,
                BendraSuma = bendraSuma,
                PacientuSkaicius = pacientuSkaicius,
                GydytojuEfektyvumas = gydytojuEfektyvumas,
                TopProceduros = topProceduros
            };

            return Ok(statistika);
        }

    }
}