using Microsoft.EntityFrameworkCore;
using OdontoKlinika.API.Models;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace OdontoKlinika.API.Data
{
    public class OdontoDbContext : DbContext
    {
        public OdontoDbContext(DbContextOptions<OdontoDbContext> options) : base(options)
        {
        }

        public DbSet<Pacientas> Pacientai { get; set; }
        public DbSet<Vizitas> Vizitai { get; set; }
        public DbSet<Procedura> Proceduros { get; set; }
        public DbSet<Gydytojas> Gydytojai { get; set; }
        public DbSet<Adminas> Adminai { get; set; }
        public DbSet<Vartotojas> Vartotojai { get; set; }
        public DbSet<DarboGrafikas> DarboGrafikai { get; set; }
        public DbSet<DarboIsimtis> DarboIsimtys { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. Paveldėjimo konfigūracija
            modelBuilder.Entity<Vartotojas>()
                .HasDiscriminator<string>("VartotojoTipas")
                .HasValue<Pacientas>("Pacientas")
                .HasValue<Gydytojas>("Gydytojas")
                .HasValue<Adminas>("Adminas");

            // 2. Ryšys: Vizitas -> Pacientas (Konkretizuojame, kad tai Pacientas)
            modelBuilder.Entity<Vizitas>()
                .HasOne(v => v.Pacientas)
                .WithMany(p => p.Vizitai) // Šis p turi būti tipo Pacientas
                .HasForeignKey(v => v.PacientasId)
                .OnDelete(DeleteBehavior.Restrict);

            // 3. Ryšys: Vizitas -> Gydytojas (Konkretizuojame, kad tai Gydytojas)
            modelBuilder.Entity<Vizitas>()
                .HasOne(v => v.Gydytojas)
                .WithMany(g => g.Vizitai) // Šis g turi būti tipo Gydytojas
                .HasForeignKey(v => v.GydytojasId)
                .OnDelete(DeleteBehavior.Restrict);

            // 4. Ryšys: Vizitas -> Procedura (Trijų lygių gylis)
            modelBuilder.Entity<Procedura>()
                .HasOne(p => p.Vizitas)
                .WithMany(v => v.Proceduros)
                .HasForeignKey(p => p.VizitasId)
                .OnDelete(DeleteBehavior.Cascade); // Ištrynus vizitą, išsitrina ir procedūros

            // 5. Kiti nustatymai
            modelBuilder.Entity<Vartotojas>()
                .HasIndex(v => v.AsmensKodas)
                .IsUnique();

            modelBuilder.Entity<Procedura>()
                .Property(p => p.Kaina)
                .HasColumnType("decimal(18,2)");
        }
    }
}