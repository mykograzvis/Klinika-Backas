using OdontoKlinika.API.Data;
using OdontoKlinika.API.Models;
using BCrypt.Net;

namespace OdontoKlinika.API.Data
{
    public static class DbInitializer
    {
        public static void Seed(IApplicationBuilder applicationBuilder)
        {
            using (var serviceScope = applicationBuilder.ApplicationServices.CreateScope())
            {
                var context = serviceScope.ServiceProvider.GetService<OdontoDbContext>();

                if (context == null) return;

                // Užtikriname, kad DB egzistuoja
                context.Database.EnsureCreated();

                // Tikriname, ar DB jau yra bet koks vartotojas su role "Adminas"
                // Naudojame tavo modelyje nurodytą tekstą "Adminas"
                if (!context.Vartotojai.Any(u => u.Role == "Adminas"))
                {
                    // Sukuriame Adminas objektą (kuris paveldi Vartotojas)
                    var pradinisAdminas = new Adminas
                    {
                        Vardas = "Sistemos",
                        Pavarde = "Administratorius",
                        ElPastas = "admin@odontoklinika.lt",
                        Telefonas = "+37060000000",
                        Amzius = 30,
                        AsmensKodas = "39001010000",
                        KraujoGrupe = "A+",
                        SlaptazodisHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
                        TwoFactorSecret = Guid.NewGuid().ToString().Replace("-", "").Substring(0, 10),
                        IsTwoFactorEnabled = false
                    };

                    context.Vartotojai.Add(pradinisAdminas);
                    context.SaveChanges();

                    Console.WriteLine("------------------------------------------");
                    Console.WriteLine("--> DB SEED: Sukurtas administratorius");
                    Console.WriteLine("--> El. paštas: admin@odontoklinika.lt");
                    Console.WriteLine("--> Slaptažodis: Admin123!");
                    Console.WriteLine("------------------------------------------");
                }
            }
        }
    }
}