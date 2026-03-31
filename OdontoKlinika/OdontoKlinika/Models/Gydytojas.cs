namespace OdontoKlinika.API.Models
{
    public class Gydytojas : Vartotojas
    {
        public string Specializacija { get; set; } = string.Empty;
        public int DarboPatirtisMetais { get; set; }

        // Ryšys: Gydytojo kalendorius yra jo vizitų sąrašas
        public List<Vizitas> Vizitai { get; set; } = new();

        public Gydytojas()
        {
            Role = "Gydytojas";
        }
    }
}