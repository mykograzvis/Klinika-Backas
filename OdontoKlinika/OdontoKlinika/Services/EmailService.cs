using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;
using Microsoft.Extensions.Configuration; // Būtina pridėti šį namespace

namespace OdontoKlinika.API.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;

        // Konstruktorius, kuris paima konfigūraciją iš programos
        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SiustiSaskaita(string klientoPastas, string klientoVardas, decimal suma, string procedurosHtml)
        {
            var email = new MimeMessage();
            // Galite paimti siuntėją iš konfigūracijos arba palikti fiksuotą
            email.From.Add(new MailboxAddress("Odonto Klinika", _configuration["EmailSettings:User"] ?? "tavo-klinika@gmail.com"));
            email.To.Add(new MailboxAddress(klientoVardas, klientoPastas));
            email.Subject = "Jūsų vizito sąskaita faktūra";

            var bodyBuilder = new BodyBuilder();
            bodyBuilder.HtmlBody = $@"
                <div style='font-family: Arial, sans-serif; padding: 20px;'>
                    <h1 style='color: #007bff;'>Sveiki, {klientoVardas}</h1>
                    <p>Dėkojame, kad lankėtės mūsų klinikoje. Štai jūsų vizito išrašas:</p>
                    <div style='background: #f8f9fa; padding: 15px; border-radius: 5px;'>
                        {procedurosHtml}
                    </div>
                    <h2 style='text-align: right;'>Iš viso sumokėta: {suma} €</h2>
                    <p style='font-size: 12px; color: #666;'>Šis laiškas sugeneruotas automatiškai.</p>
                </div>";

            email.Body = bodyBuilder.ToMessageBody();

            using var smtp = new SmtpClient();

            // Naudojame konfigūraciją iš appsettings.json
            await smtp.ConnectAsync(
                _configuration["EmailSettings:Host"],
                int.Parse(_configuration["EmailSettings:Port"]),
                SecureSocketOptions.StartTls
            );

            await smtp.AuthenticateAsync(
                _configuration["EmailSettings:User"],
                _configuration["EmailSettings:Password"]
            );

            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);
        }

        public async Task SiustiPranesima(string pirkejoEmail, string tema, string htmlTurinys)
        {
            var email = new MimeMessage();
            email.From.Add(new MailboxAddress("Odonto Klinika", _configuration["EmailSettings:User"]));
            email.To.Add(MailboxAddress.Parse(pirkejoEmail));
            email.Subject = tema;
            email.Body = new TextPart(TextFormat.Html) { Text = htmlTurinys };

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(
                _configuration["EmailSettings:Host"],
                int.Parse(_configuration["EmailSettings:Port"]),
                SecureSocketOptions.StartTls
            );

            await smtp.AuthenticateAsync(
                _configuration["EmailSettings:User"],
                _configuration["EmailSettings:Password"]
            );

            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);
        }

        public async Task SiustiSaskaitaSuPriedu(string klientoPastas, string klientoVardas, byte[] pdfContent, string saskaitosNr)
        {
            var email = new MimeMessage();
            email.From.Add(new MailboxAddress("Odonto Klinika", _configuration["EmailSettings:User"]));
            email.To.Add(new MailboxAddress(klientoVardas, klientoPastas));
            email.Subject = $"Sąskaita faktūra Nr. {saskaitosNr}";

            // 1. Sukuriame laiško tekstinę dalį
            var body = new TextPart("html")
            {
                Text = $@"
            <div style='font-family: sans-serif;'>
                <h2>Sveiki, {klientoVardas},</h2>
                <p>Dėkojame, kad lankėtės mūsų klinikoje.</p>
                <p>Pridedame jūsų vizito sąskaitą faktūrą PDF formatu.</p>
                <br>
                <p>Pagarbiai,<br>Odonto Klinika</p>
            </div>"
            };

            // 2. Sukuriame priedą
            var attachment = new MimePart("application", "pdf")
            {
                Content = new MimeContent(new MemoryStream(pdfContent)),
                ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                ContentTransferEncoding = ContentEncoding.Base64,
                FileName = $"Saskaita_{saskaitosNr}.pdf"
            };

            // 3. Svarbu: Sukuriame 'multipart/mixed' konteinerį, kuris apjungs tekstą ir failą
            var multipart = new Multipart("mixed");
            multipart.Add(body);
            multipart.Add(attachment);

            // 4. Priskiriame visą multipart turinį laiško Body
            email.Body = multipart;

            using var smtp = new SmtpClient();
            try
            {
                // Mailtrap nustatymai dažniausiai naudoja StartTls arba paprastą jungtį
                await smtp.ConnectAsync(_configuration["EmailSettings:Host"],
                                        int.Parse(_configuration["EmailSettings:Port"]),
                                        MailKit.Security.SecureSocketOptions.StartTls);

                await smtp.AuthenticateAsync(_configuration["EmailSettings:User"],
                                             _configuration["EmailSettings:Password"]);

                await smtp.SendAsync(email);
            }
            finally
            {
                await smtp.DisconnectAsync(true);
            }
        }
    }
}