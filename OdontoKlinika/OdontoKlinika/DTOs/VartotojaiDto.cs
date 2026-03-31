namespace OdontoKlinika.API.DTOs
{
    // DTO klasė
    public class SlaptazodžioKeitimoDto
    {
        public string SenasSlaptazodis { get; set; } = string.Empty;
        public string NaujasSlaptazodis { get; set; } = string.Empty;
    }

    // DTO klasė
    public class ElPastoKeitimoDto
    {
        public string NaujasEmail { get; set; } = string.Empty;
    }

    public class AdminAccessUpdateDto
    {
        public string? NaujasEmail { get; set; }
        public string? NaujasSlaptazodis { get; set; }
    }
    public class ProfilioAtnaujinimoDto
    {
        public string Vardas { get; set; } = string.Empty;
        public string Pavarde { get; set; } = string.Empty;
        public string Telefonas { get; set; } = string.Empty;
        public int Amzius { get; set; }
        public string? KraujoGrupe { get; set; }
        // Pasirenkami laukai gydytojams
        public string? Specializacija { get; set; }
        public int? DarboPatirtisMetais { get; set; }
    }
}
