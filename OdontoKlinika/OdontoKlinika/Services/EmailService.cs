using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;
using Microsoft.Extensions.Configuration;

namespace OdontoKlinika.API.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // Pagalbinis metodas – jungiamasi vieną kartą, naudojamas visuose 3 metoduose
        private async Task<SmtpClient> PrisijungtiPrieSMTP()
        {
            var smtp = new SmtpClient();
            await smtp.ConnectAsync(
                "smtp.gmail.com",
                587,
                SecureSocketOptions.StartTls
            );
            await smtp.AuthenticateAsync(
                _configuration["EmailSettings:User"],
                _configuration["EmailSettings:Password"]
            );
            return smtp;
        }

        public async Task SiustiSaskaita(string klientoPastas, string klientoVardas, decimal suma, string procedurosHtml)
        {
            var email = new MimeMessage();
            email.From.Add(new MailboxAddress("Gelmidenta", _configuration["EmailSettings:User"]));
            email.To.Add(new MailboxAddress(klientoVardas, klientoPastas));
            email.Subject = "Jūsų vizito sąskaita faktūra";

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = $@"
                <div style='font-family: Arial, sans-serif; padding: 20px;'>
                    <h1 style='color: #007bff;'>Sveiki, {klientoVardas}</h1>
                    <p>Dėkojame, kad lankėtės mūsų klinikoje. Štai jūsų vizito išrašas:</p>
                    <div style='background: #f8f9fa; padding: 15px; border-radius: 5px;'>
                        {procedurosHtml}
                    </div>
                    <h2 style='text-align: right;'>Iš viso sumokėta: {suma} €</h2>
                    <p style='font-size: 12px; color: #666;'>Šis laiškas sugeneruotas automatiškai.</p>
                </div>"
            };
            email.Body = bodyBuilder.ToMessageBody();

            using var smtp = await PrisijungtiPrieSMTP();
            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);
        }

        public async Task SiustiPranesima(string pirkejoEmail, string tema, string htmlTurinys)
        {
            var email = new MimeMessage();
            email.From.Add(new MailboxAddress("Gelmidenta", _configuration["EmailSettings:User"]));
            email.To.Add(MailboxAddress.Parse(pirkejoEmail));
            email.Subject = tema;
            email.Body = new TextPart(TextFormat.Html) { Text = htmlTurinys };

            using var smtp = await PrisijungtiPrieSMTP();
            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);
        }

        public async Task SiustiSaskaitaSuPriedu(string klientoPastas, string klientoVardas, string kleintoPavarde, byte[] pdfContent, string saskaitosNr)
        {
            var email = new MimeMessage();
            email.From.Add(new MailboxAddress("Gelmidenta", _configuration["EmailSettings:User"]));
            email.To.Add(new MailboxAddress(klientoVardas, klientoPastas));
            email.Subject = $"Sąskaita faktūra Nr. {saskaitosNr}";

            var body = new TextPart("html")
            {
                Text = $@"
                <div style='font-family: sans-serif;'>
                    <h2>Sveiki, {klientoVardas} {kleintoPavarde},</h2>
                    <p>Dėkojame, kad lankėtės mūsų klinikoje.</p>
                    <p>Pridedame jūsų vizito sąskaitą faktūrą PDF formatu.</p>
                    <br>
                    <p>Pagarbiai,<br>Odonto Klinika</p>
                </div>"
            };

            var attachment = new MimePart("application", "pdf")
            {
                Content = new MimeContent(new MemoryStream(pdfContent)),
                ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                ContentTransferEncoding = ContentEncoding.Base64,
                FileName = $"Saskaita_{saskaitosNr}.pdf"
            };

            var multipart = new Multipart("mixed");
            multipart.Add(body);
            multipart.Add(attachment);
            email.Body = multipart;

            using var smtp = await PrisijungtiPrieSMTP();
            try
            {
                await smtp.SendAsync(email);
            }
            finally
            {
                await smtp.DisconnectAsync(true);
            }
        }
    }
}