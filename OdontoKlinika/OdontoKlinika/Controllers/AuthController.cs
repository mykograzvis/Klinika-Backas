using Google.Authenticator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using OdontoKlinika.API.Data;
using OdontoKlinika.API.DTOs;
using OdontoKlinika.API.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace OdontoKlinika.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly OdontoDbContext _context;
        private readonly IConfiguration _configuration;
        public AuthController(OdontoDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("registracija")]
        public async Task<ActionResult<Vartotojas>> Registruotis(RegistracijaDto request)
        {
            // 1. Patikriname, ar toks el. paštas jau egzistuoja
            if (await _context.Vartotojai.AnyAsync(u => u.ElPastas == request.ElPastas))
            {
                return BadRequest("Vartotojas su tokiu el. paštu jau yra.");
            }

            if (await _context.Vartotojai.AnyAsync(u => u.AsmensKodas == request.AsmensKodas))
                return BadRequest("Vartotojas su tokiu asmens kodu jau užregistruotas.");

            // 2. Užšifruojame slaptažodį
            string slaptazodisHash = BCrypt.Net.BCrypt.HashPassword(request.Slaptazodis);

            // 3. Sukuriame vartotoją pagal tipą
            Vartotojas naujasVartotojas;

            if (request.Tipas == "Gydytojas")
            {
                naujasVartotojas = new Gydytojas
                {
                    Vardas = request.Vardas,
                    Pavarde = request.Pavarde,
                    AsmensKodas = request.AsmensKodas,
                    ElPastas = request.ElPastas,
                    SlaptazodisHash = slaptazodisHash,
                    Telefonas = request.Telefonas,
                    Amzius = request.Amzius,
                    KraujoGrupe = request.KraujoGrupe,
                    Specializacija = "Bendrai" // Galima papildyti DTO lauku
                };
            }
            else
            {
                naujasVartotojas = new Pacientas
                {
                    Vardas = request.Vardas,
                    Pavarde = request.Pavarde,
                    AsmensKodas = request.AsmensKodas,
                    ElPastas = request.ElPastas,
                    SlaptazodisHash = slaptazodisHash,
                    Telefonas = request.Telefonas,
                    Amzius = request.Amzius,
                    KraujoGrupe = request.KraujoGrupe
                };
            }

            _context.Vartotojai.Add(naujasVartotojas);
            await _context.SaveChangesAsync();

            return Ok("Registracija sėkminga!");
        }

        [HttpPost("login")]
        public async Task<ActionResult<object>> Login(LoginDto request)
        {
            var vartotojas = await _context.Vartotojai.FirstOrDefaultAsync(u => u.ElPastas == request.ElPastas);
            if (vartotojas == null) return BadRequest("Vartotojas nerastas.");

            if (!BCrypt.Net.BCrypt.Verify(request.Slaptazodis, vartotojas.SlaptazodisHash))
            {
                return BadRequest("Neteisingas slaptažodis.");
            }

            bool isPersonalas = vartotojas.Role == "Gydytojas" || vartotojas.Role == "Adminas";

            // 1. Jei personalas JAU susitvarkė 2FA -> prašome KODO
            if (isPersonalas && vartotojas.IsTwoFactorEnabled)
            {
                return Ok(new
                {
                    requiresTwoFactor = true,
                    userId = vartotojas.Id // Būtina 2-am žingsniui
                });
            }

            // 2. Jei personalas dar NESUSITVARKĖ -> siunčiame nustatyti (QR kodui)
            if (isPersonalas && !vartotojas.IsTwoFactorEnabled)
            {
                return Ok(new
                {
                    mustSetup2FA = true, // Pakeista iš needs2FASetup
                    userId = vartotojas.Id
                });
            }

            // 3. Pacientas arba sėkmingas login be 2FA apribojimų
            var token = CreateToken(vartotojas);

            return Ok(new
            {
                token = token,
                role = vartotojas.Role,
                vardas = vartotojas.Vardas,
                userId = vartotojas.Id,
                requiresTwoFactor = false,
                mustSetup2FA = false
            });
        }

        [HttpPost("login-2fa")]
        public async Task<ActionResult<object>> Login2FA([FromBody] LoginTwoFactorDto request)
        {
            var vartotojas = await _context.Vartotojai.FindAsync(request.UserId);
            if (vartotojas == null) return BadRequest("Vartotojas nerastas.");

            var tfa = new TwoFactorAuthenticator();
            bool isValid = tfa.ValidateTwoFactorPIN(vartotojas.TwoFactorSecret, request.Code);

            if (!isValid) return BadRequest("Neteisingas 2FA kodas.");

            // Jei kodas tinka, dabar jau generuojame JWT tokeną
            var token = CreateToken(vartotojas);

            return Ok(new
            {
                token = token,
                role = vartotojas.Role,
                vardas = vartotojas.Vardas,
                userId = vartotojas.Id
            });
        }

        private string CreateToken(Vartotojas vartotojas)
        {
            List<Claim> claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, vartotojas.Vardas + " " + vartotojas.Pavarde),
                new Claim(ClaimTypes.Email, vartotojas.ElPastas),
                new Claim(ClaimTypes.Role, vartotojas.Role),
                new Claim(ClaimTypes.NameIdentifier, vartotojas.Id.ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                _configuration.GetSection("Jwt:Key").Value!));

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

            var token = new JwtSecurityToken(
                    issuer: _configuration.GetSection("Jwt:Issuer").Value,
                    audience: _configuration.GetSection("Jwt:Audience").Value,
                    claims: claims,
                    expires: DateTime.Now.AddDays(1),
                    signingCredentials: creds
                );

            var jwt = new JwtSecurityTokenHandler().WriteToken(token);
            return jwt;
        }

        [HttpGet("setup-2fa")]
        public IActionResult Setup2FA([FromQuery] int userId)
        {
            var user = _context.Vartotojai.Find(userId);
            if (user == null) return BadRequest("Vartotojas nerastas.");

            // Jei dar neturi rakto – sugeneruojam
            if (string.IsNullOrEmpty(user.TwoFactorSecret))
            {
                user.TwoFactorSecret = Guid.NewGuid().ToString().Replace("-", "").Substring(0, 10);
                _context.SaveChanges();
            }

            var tfa = new TwoFactorAuthenticator();

            // ČIA visa magija
            var setupInfo = tfa.GenerateSetupCode(
                "Gelmidenta",          // Issuer (be tarpų, be LT raidžių)
                user.ElPastas,        // Account
                user.TwoFactorSecret, // Secret
                false,                // QR kaip image URL
                300                   // QR size
            );

            return Ok(new
            {
                qrCodeUrl = setupInfo.QrCodeSetupImageUrl,
                manualEntryKey = setupInfo.ManualEntryKey
            });
        }


        [HttpPost("verify-2fa")]
        public IActionResult Verify2FA([FromQuery] int userId, [FromBody] string code)
        {
            var user = _context.Vartotojai.Find(userId);
            if (user == null) return BadRequest("Vartotojas nerastas.");

            var tfa = new TwoFactorAuthenticator();

            bool isValid = tfa.ValidateTwoFactorPIN(
                user.TwoFactorSecret,
                code
            );

            if (isValid)
            {
                user.IsTwoFactorEnabled = true;
                _context.SaveChanges();
                return Ok(true);
            }

            return BadRequest("Neteisingas kodas");
        }

        [HttpPost("disable-self-2fa")]
        [Authorize] // Tik prisijungę vartotojai gali pasiekti
        public async Task<IActionResult> DisableSelf2FA()
        {
            try
            {
                // 1. Pasiimame vartotojo ID iš Token'o (Claims)
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return Unauthorized("Nepavyko nustatyti vartotojo tapatybės.");
                }

                // 2. Surandame vartotoją duomenų bazėje
                // Pakeisk 'Vartotojas' į savo modelio pavadinimą, o '_context' į savo DB kontekstą
                var vartotojas = await _context.Vartotojai
                    .FirstOrDefaultAsync(u => u.Id.ToString() == userIdClaim);

                if (vartotojas == null)
                {
                    return NotFound("Vartotojas nerastas.");
                }

                // 3. Išjungiame 2FA
                vartotojas.TwoFactorSecret = null;
                vartotojas.IsTwoFactorEnabled = false;

                // Jei naudoji ASP.NET Identity ir saugai SecretKey, protinga jį išvalyti:
                // vartotojas.TwoFactorSecretKey = null; 

                await _context.SaveChangesAsync();

                return Ok(new { message = "2FA sėkmingai išjungtas." });
            }
            catch (Exception ex)
            {
                // Log klaida čia
                return StatusCode(500, "Serverio klaida išjungiant 2FA.");
            }
        }

        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginDto request)
        {
            try
            {
                var payload = await ValidateGoogleToken(request.IdToken);

                if (payload == null)
                    return BadRequest("Neteisingas Google token.");

                var vartotojas = await _context.Vartotojai
                    .FirstOrDefaultAsync(u => u.ElPastas == payload.Email);

                if (vartotojas == null)
                {
                    vartotojas = new Pacientas
                    {
                        Vardas = payload.GivenName,
                        Pavarde = payload.FamilyName,
                        ElPastas = payload.Email,
                        SlaptazodisHash = "",
                        GoogleId = payload.Subject,
                        Telefonas = "",
                        AsmensKodas = "",
                        Amzius = 0
                    };

                    _context.Vartotojai.Add(vartotojas);
                    await _context.SaveChangesAsync();
                }
                else if (string.IsNullOrEmpty(vartotojas.GoogleId))
                {
                    vartotojas.GoogleId = payload.Subject;
                    await _context.SaveChangesAsync();
                }

                if (vartotojas.IsTwoFactorEnabled)
                {
                    return Ok(new
                    {
                        requiresTwoFactor = true,
                        userId = vartotojas.Id
                    });
                }

                var token = CreateToken(vartotojas);

                return Ok(new
                {
                    token = token,
                    role = vartotojas.Role,
                    vardas = vartotojas.Vardas,
                    userId = vartotojas.Id,
                    isNewUser = string.IsNullOrEmpty(vartotojas.AsmensKodas),
                    requiresTwoFactor = false,
                    mustSetup2FA = false
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Klaida prisijungiant su Google.");
            }
        }


        private async Task<Google.Apis.Auth.GoogleJsonWebSignature.Payload> ValidateGoogleToken(string idToken)
        {
            try
            {
                var settings = new Google.Apis.Auth.GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { _configuration["Authentication:Google:ClientId"] }
                };

                var payload = await Google.Apis.Auth.GoogleJsonWebSignature.ValidateAsync(idToken, settings);
                return payload;
            }
            catch
            {
                return null;
            }
        }

    }
}