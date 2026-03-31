namespace OdontoKlinika.API.DTOs
{
    public class GydytojasDto
    {
        public string Vardas { get; set; } = string.Empty;
        public string Pavarde { get; set; } = string.Empty;
        public string AsmensKodas { get; set; } = string.Empty;
        public string ElPastas { get; set; } = string.Empty;
        public string Slaptazodis { get; set; } = string.Empty;
        public string Telefonas { get; set; } = string.Empty;
        public int Amzius { get; set; }
        public string? KraujoGrupe { get; set; }
        public string Specializacija { get; set; } = string.Empty;
        public int DarboPatirtisMetais { get; set; }
    }
}