using System.ComponentModel.DataAnnotations;

namespace OdontoKlinika.API.Models
{
    public class Vizitas
    {
        public int Id { get; set; }

        [Required]
        public int PacientasId { get; set; }
        public Pacientas Pacientas { get; set; }

        [Required]
        public int GydytojasId { get; set; }
        public Gydytojas Gydytojas { get; set; }

        [Required]
        public DateTime PradziosLaikas { get; set; }

        [Required]
        public DateTime PabaigosLaikas { get; set; }

        // Būsena: Suplanuotas, Atšauktas, Įvykęs
        public string Busena { get; set; } = "Suplanuotas";

        public string? Pastabos { get; set; } // Pvz. "Skauda protinį dantį"

        public DateTime SukurimoData { get; set; } = DateTime.Now;

        public List<Procedura> Proceduros { get; set; } = new();

        public Boolean Apmoketa { get; set; } = false;
    }
}