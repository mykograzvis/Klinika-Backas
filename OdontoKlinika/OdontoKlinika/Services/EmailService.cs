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