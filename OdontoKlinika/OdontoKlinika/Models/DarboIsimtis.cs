namespace OdontoKlinika.API.Models
{
    public class DarboIsimtis
    {
        public int Id { get; set; }
        public string GydytojoId { get; set; }
        public DateTime Data { get; set; } // Konkreti diena, pvz. 2024-05-20
        public bool ArDirba { get; set; } // Jei false - visą dieną nedirba
        public string? Pradzia { get; set; } // Galima nurodyti kitokį laiką tą dieną
        public string? Pabaiga { get; set; }
        public string? Priezastis { get; set; } // Pvz. "Atostogos", "Konferencija"
    }
}
