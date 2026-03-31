using System.ComponentModel.DataAnnotations;

namespace OdontoKlinika.API.Models
{
    public class Procedura
    {
        public int Id { get; set; }

        [Required]
        public string Pavadinimas { get; set; } // Pvz: "Danties kanalo valymas"

        public string? Aprasymas { get; set; }

        [Required]
        public decimal Kaina { get; set; }

        // Ryšys su Vizitu (Trijų lygių gylis: Vartotojas -> Vizitas -> Procedura)
        [Required]
        public int VizitasId { get; set; }
        public Vizitas? Vizitas { get; set; }
    }
}