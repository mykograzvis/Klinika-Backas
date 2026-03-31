namespace OdontoKlinika.API.DTOs
{
    public class ProceduraDto
    {
        public int? Id { get; set; } // Reikalingas redagavimui/trynimui
        public int VizitasId { get; set; }
        public string Pavadinimas { get; set; } = string.Empty;
        public decimal Kaina { get; set; }
        public string? Aprasymas { get; set; }
    }
}