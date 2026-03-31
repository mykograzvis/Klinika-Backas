namespace OdontoKlinika.API.Models
{
    public class Pacientas : Vartotojas
    {
        public List<Vizitas> Vizitai { get; set; } = new();

        public Pacientas()
        {
            Role = "Pacientas";
        }
    }
}