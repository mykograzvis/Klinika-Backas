namespace OdontoKlinika.API.DTOs
{
    public class LoginDto
    {
        public string ElPastas { get; set; } = string.Empty;
        public string Slaptazodis { get; set; } = string.Empty;
    }
    public class RegistracijaDto
    {
        public string Vardas { get; set; } = string.Empty;
        public string Pavarde { get; set; } = string.Empty;
        public string AsmensKodas { get; set; } = string.Empty;
        public string ElPastas { get; set; } = string.Empty;
        public string Slaptazodis { get; set; } = string.Empty;
        public string Telefonas { get; set; } = string.Empty;
        public int Amzius { get; set; }
        public string? KraujoGrupe { get; set; }
        public string Tipas { get; set; } = "Pacientas";
    }
    public class LoginTwoFactorDto
    {
        public int UserId { get; set; }
        public string Code { get; set; } = string.Empty;
    }
    public class GoogleLoginDto
    {
        public string IdToken { get; set; }
    }

}
