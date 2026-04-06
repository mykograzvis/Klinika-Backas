using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OdontoKlinika.API.Data;
using OdontoKlinika.API.Models;
using OpenAI.Chat;

namespace OdontoKlinika.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly LlmService _llmService;
    private readonly OdontoDbContext _context;

    public ChatController(LlmService llmService, OdontoDbContext context)
    {
        _llmService = llmService;
        _context = context;
    }

    [HttpPost]
    [Produces("application/json")]
    public async Task<IActionResult> Post([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Message))
            return BadRequest("Žinutė negali būti tuščia");

        try
        {
            var history = BuildHistory(request.History);
            var reply = await _llmService.ChatWithToolsAsync(history, request.Message, ExecuteToolAsync);
            return Ok(new ChatResponse { Response = reply });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ChatResponse { Response = $"Klaida: {ex.Message}" });
        }
    }

    private static List<ChatMessage> BuildHistory(List<HistoryMessage>? history)
    {
        if (history == null) return new();
        return history.Select<HistoryMessage, ChatMessage>(m => m.Role == "assistant"
            ? new AssistantChatMessage(m.Content)
            : new UserChatMessage(m.Content)
        ).ToList();
    }

    private async Task<string> ExecuteToolAsync(string toolName, string argsJson)
    {
        var args = JObject.Parse(argsJson);
        return toolName switch
        {
            "find_patient" => await HandleFindPatientAsync(args),
            "get_patient_visits" => await HandleGetPatientVisitsAsync(args),
            "get_patient_history" => await HandleGetPatientHistoryAsync(args),
            "get_free_slots" => await HandleGetFreeSlotsAsync(args),
            "get_doctors" => await HandleGetDoctorsAsync(args),
            "create_reservation" => await HandleCreateReservationAsync(args),
            _ => JsonConvert.SerializeObject(new { error = $"Nežinoma funkcija: {toolName}" })
        };
    }

    private async Task<string> HandleFindPatientAsync(JObject args)
    {
        var name = args.Value<string>("name") ?? "";
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var query = _context.Pacientai.AsQueryable();
        foreach (var part in parts)
            query = query.Where(p => p.Vardas.Contains(part) || p.Pavarde.Contains(part));

        var patients = await query.Take(5)
            .Select(p => new { id = p.Id, fullName = p.Vardas + " " + p.Pavarde })
            .ToListAsync();

        return patients.Any()
            ? JsonConvert.SerializeObject(patients)
            : JsonConvert.SerializeObject(new { error = $"Pacientas nerastas: {name}" });
    }

    private async Task<string> HandleGetPatientVisitsAsync(JObject args)
    {
        var patientId = args.Value<int?>("patientId");
        if (patientId == null)
            return JsonConvert.SerializeObject(new { error = "Trūksta patientId." });

        var visits = await _context.Vizitai
            .Include(v => v.Gydytojas)
            .Include(v => v.Proceduros)
            .Where(v => v.PacientasId == patientId.Value
                     && v.PradziosLaikas >= DateTime.Today
                     && v.Busena != "Atšauktas")
            .OrderBy(v => v.PradziosLaikas)
            .Take(10)
            .ToListAsync();

        if (!visits.Any())
            return JsonConvert.SerializeObject(new { message = "Pacientas neturi artėjančių vizitų." });

        return JsonConvert.SerializeObject(visits.Select(v => new
        {
            id = v.Id,
            time = v.PradziosLaikas.ToString("yyyy-MM-dd HH:mm"),
            doctor = v.Gydytojas != null ? $"{v.Gydytojas.Vardas} {v.Gydytojas.Pavarde}" : "",
            status = v.Busena,
            procedures = v.Proceduros.Select(p => p.Pavadinimas)
        }));
    }

    private async Task<string> HandleGetPatientHistoryAsync(JObject args)
    {
        var patientId = args.Value<int?>("patientId");
        var procedureType = args.Value<string>("procedure_type");

        if (patientId == null)
            return JsonConvert.SerializeObject(new { error = "Trūksta patientId." });

        var query = _context.Vizitai
            .Include(v => v.Gydytojas)
            .Include(v => v.Proceduros)
            .Where(v => v.PacientasId == patientId.Value
                     && v.PradziosLaikas < DateTime.Now
                     && v.Busena != "Atšauktas");

        if (!string.IsNullOrWhiteSpace(procedureType))
            query = query.Where(v => v.Proceduros.Any(p => p.Pavadinimas.Contains(procedureType)));

        var visits = await query
            .OrderByDescending(v => v.PradziosLaikas)
            .Take(10)
            .ToListAsync();

        if (!visits.Any())
            return JsonConvert.SerializeObject(new { message = "Nerasta vizitų pagal nurodytus kriterijus." });

        return JsonConvert.SerializeObject(visits.Select(v => new
        {
            id = v.Id,
            time = v.PradziosLaikas.ToString("yyyy-MM-dd HH:mm"),
            doctor = v.Gydytojas != null ? $"{v.Gydytojas.Vardas} {v.Gydytojas.Pavarde}" : "",
            status = v.Busena,
            procedures = v.Proceduros.Select(p => p.Pavadinimas)
        }));
    }

    private async Task<string> HandleGetDoctorsAsync(JObject args)
    {
        var specializacija = args.Value<string>("specializacija");

        var query = _context.Gydytojai.AsQueryable();

        if (!string.IsNullOrWhiteSpace(specializacija))
            query = query.Where(g => g.Specializacija != null &&
                                     g.Specializacija.Contains(specializacija));

        var doctors = await query
            .Select(g => new
            {
                id = g.Id,
                fullName = g.Vardas + " " + g.Pavarde,
                specializacija = g.Specializacija
            })
            .ToListAsync();

        return doctors.Any()
            ? JsonConvert.SerializeObject(doctors)
            : JsonConvert.SerializeObject(new { message = "Gydytojų nerasta." });
    }

    private async Task<string> HandleGetFreeSlotsAsync(JObject args)
    {
        var doctorName = args.Value<string>("doctorName") ?? "";
        var serviceName = args.Value<string>("serviceName") ?? "";
        var fromDate = args.Value<string>("fromDate");
        var toDate = args.Value<string>("toDate");

        DateTime from = string.IsNullOrWhiteSpace(fromDate) ? DateTime.Today : DateTime.Parse(fromDate);
        DateTime to = string.IsNullOrWhiteSpace(toDate) ? DateTime.Today.AddDays(14) : DateTime.Parse(toDate);

        var gydytojas = await _context.Gydytojai.FirstOrDefaultAsync(g =>
            (g.Vardas + " " + g.Pavarde).Contains(doctorName) ||
            g.Vardas.Contains(doctorName) || g.Pavarde.Contains(doctorName));

        if (gydytojas == null)
            return JsonConvert.SerializeObject(new { error = $"Neradau gydytojo: {doctorName}" });

        var procedura = await _context.Proceduros
            .FirstOrDefaultAsync(p => p.Pavadinimas.Contains(serviceName));

        if (procedura == null)
            return JsonConvert.SerializeObject(new { error = $"Neradau procedūros: {serviceName}" });

        var uzimti = await _context.Vizitai
            .Where(v => v.GydytojasId == gydytojas.Id
                     && v.PradziosLaikas >= from
                     && v.PradziosLaikas <= to
                     && v.Busena != "Atšauktas")
            .Select(v => v.PradziosLaikas)
            .ToListAsync();

        var laisvi = new List<DateTime>();
        var cur = from.Date.AddHours(8);
        var end = to.Date.AddHours(17);

        while (cur <= end && laisvi.Count < 10)
        {
            if (cur.DayOfWeek != DayOfWeek.Saturday &&
                cur.DayOfWeek != DayOfWeek.Sunday &&
                !uzimti.Contains(cur))
                laisvi.Add(cur);
            cur = cur.AddMinutes(30);
        }

        if (!laisvi.Any())
            return JsonConvert.SerializeObject(new { message = "Laisvų laikų nerasta." });

        return JsonConvert.SerializeObject(new
        {
            doctorId = gydytojas.Id,
            doctor = $"{gydytojas.Vardas} {gydytojas.Pavarde}",
            serviceId = procedura.Id,
            service = procedura.Pavadinimas,
            slots = laisvi.Select((d, i) => new { slotId = i + 1, time = d.ToString("yyyy-MM-dd HH:mm") })
        });
    }

    private async Task<string> HandleCreateReservationAsync(JObject args)
    {
        var patientId = args.Value<int?>("patientId");
        var doctorId = args.Value<int?>("doctorId");
        var serviceId = args.Value<int?>("serviceId");
        var datetimeStr = args.Value<string>("datetime");

        if (patientId == null || doctorId == null || serviceId == null || string.IsNullOrWhiteSpace(datetimeStr))
            return JsonConvert.SerializeObject(new { error = "Trūksta argumentų." });

        var datetime = DateTime.Parse(datetimeStr);

        var exists = await _context.Vizitai.AnyAsync(v =>
            v.GydytojasId == doctorId.Value &&
            v.PradziosLaikas == datetime &&
            v.Busena != "Atšauktas");

        if (exists)
            return JsonConvert.SerializeObject(new { error = "Šis laikas jau užimtas." });

        var vizitas = new Vizitas
        {
            PacientasId = patientId.Value,
            GydytojasId = doctorId.Value,
            PradziosLaikas = datetime,
            PabaigosLaikas = datetime.AddMinutes(30),
            Busena = "Suplanuotas",
            Apmoketa = false
        };

        var procedura = await _context.Proceduros.FindAsync(serviceId.Value);
        if (procedura != null)
            vizitas.Proceduros.Add(procedura);

        _context.Vizitai.Add(vizitas);
        await _context.SaveChangesAsync();

        return JsonConvert.SerializeObject(new
        {
            message = "Vizitas sėkmingai užregistruotas.",
            visitId = vizitas.Id,
            time = vizitas.PradziosLaikas.ToString("yyyy-MM-dd HH:mm"),
            doctorId = vizitas.GydytojasId,
            patientId = vizitas.PacientasId
        });
    }
}

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public List<HistoryMessage>? History { get; set; }
}

public class HistoryMessage
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
}

public class ChatResponse
{
    public string Response { get; set; } = string.Empty;
}