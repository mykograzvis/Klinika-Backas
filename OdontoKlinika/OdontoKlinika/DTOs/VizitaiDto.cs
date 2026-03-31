namespace OdontoKlinika.API.DTOs
{
    public class VizitasDto
    {
        public int PacientasId { get; set; }
        public int GydytojasId { get; set; }
        public DateTime PradziosLaikas { get; set; }
        public string? Pastabos { get; set; }

        // Nauji laukai pirminei procedūrai
        public string ProcedurosPavadinimas { get; set; }
        public decimal ProcedurosKaina { get; set; }

        public int TrukmeMin { get; set; }
    }

    public class VizitasUpdateDto
    {
        public int Id { get; set; }
        public DateTime PradziosLaikas { get; set; }
        public int GydytojasId { get; set; }
        public int TrukmeMin { get; set; } // Gaunama iš frontendo 'paslaugos' masyvo
        public string ProcedurosPavadinimas { get; set; }
        public decimal ProcedurosKaina { get; set; }
    }
}
