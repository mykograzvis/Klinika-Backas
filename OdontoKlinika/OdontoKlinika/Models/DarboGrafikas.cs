namespace OdontoKlinika.API.Models
{
    public class DarboGrafikas
    {
        public int Id { get; set; }

        public string GydytojoId { get; set; }

        public DayOfWeek SavaitesDiena { get; set; } // 0 = Sekmadienis, 1 = Pirmadienis...

        public bool Dirba { get; set; } // Ar gydytojas apskritai tą dieną dirba?

        // Naudojame string arba TimeSpan saugoti valandas (pvz., "08:00")
        public string? Pradzia { get; set; }
        public string? Pabaiga { get; set; }
        public string? PertraukaNuo { get; set; }
        public string? PertraukaIki { get; set; }
    }
}
