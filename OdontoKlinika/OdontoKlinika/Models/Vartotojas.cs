using System.Text.Json.Serialization;

namespace OdontoKlinika.API.Models
{
    public abstract class Vartotojas
    {
        public int Id { get; set; }
        public string Vardas { get; set; } = string.Empty;
        public string Pavarde { get; set; } = string.Empty;
        public string ElPastas { get; set; } = string.Empty;
        public string SlaptazodisHash { get; set; } = string.Empty; // Saugumui
        public string Telefonas { get; set; } = string.Empty;
        public int Amzius { get; set; }
        public string AsmensKodas { get; set; } = string.Empty;
        public string? KraujoGrupe { get; set; } // Pvz.: "A+", "B-"
        public string Role { get; set; } = string.Empty; // "Adminas", "Gydytojas", "Pacientas"
        public string? TwoFactorSecret { get; set; } // Čia saugosime užšifruotą raktą
        public bool IsTwoFactorEnabled { get; set; } = false;
        public string? GoogleId { get; set; }
    }
}