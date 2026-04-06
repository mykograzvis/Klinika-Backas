using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using OdontoKlinika.API.Data;
using OdontoKlinika.API.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. DB Kontekstas
builder.Services.AddDbContext<OdontoDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Kontroleriai su JSON nustatymais
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddEndpointsApiExplorer();

//Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "OdontoKlinika API", Version = "v1" });

    var securityScheme = new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "JWT Authentication",
        Description = "Įveskite savo JWT žetoną šiuo formatu: Bearer {tokenas}",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new Microsoft.OpenApi.Models.OpenApiReference
        {
            Id = "Bearer",
            Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme
        }
    };

    c.AddSecurityDefinition("Bearer", securityScheme);

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        { securityScheme, new string[] { } }
    });
});

// 4. Autentifikacija
builder.Services.AddAuthentication().AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8
            .GetBytes(builder.Configuration.GetSection("Jwt:Key").Value!)),
        ValidateIssuer = false,
        ValidateAudience = false
    };
});

// 5. CORS (Pridėjau, kad Next.js galėtų jungtis)
builder.Services.AddCors(options => {
    options.AddPolicy("NextJS", policy => {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<PdfService>();
builder.Services.AddScoped<LlmService>();

var app = builder.Build();

// Konvejeris (Middleware pipeline)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("NextJS"); // Įjungiame CORS

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

DbInitializer.Seed(app);

app.Run();