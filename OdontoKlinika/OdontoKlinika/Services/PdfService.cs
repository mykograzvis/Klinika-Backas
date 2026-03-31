using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using OdontoKlinika.API.Models; // Pakeisk pagal savo projektą

namespace OdontoKlinika.API.Services
{
    public class PdfService
    {
        public byte[] GeneruotiSaskaitosPdf(Vizitas vizitas)
        {
            // QuestPDF licencijos nustatymas (bendruomenės versija)
            QuestPDF.Settings.License = LicenseType.Community;
            decimal bendraSuma = vizitas.Proceduros?.Sum(p => p.Kaina) ?? 0;

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(50);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily(Fonts.Verdana));

                    // 1. Viršutinė dalis (Antraštė)
                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("UAB GELMIDENTA").FontSize(20).SemiBold().FontColor(Colors.Black);
                            col.Item().Text("Nemuno g. 11, Panevėžys, 36236 Panevėžio m. sav.");
                            col.Item().Text("Įm. kodas: 302898714");
                            col.Item().Text("Bank. Saskaita: LT1002454844156");
                        });

                        row.RelativeItem().AlignRight().Column(col =>
                        {
                            col.Item().Text("SĄSKAITA FAKTŪRA").FontSize(16).SemiBold();
                            col.Item().Text($"Nr. {vizitas.Id}");
                            col.Item().Text($"Išrašymo data: {DateTime.Now:yyyy-MM-dd}");
                            col.Item().Text($"Vizito data: {vizitas.PradziosLaikas:yyyy-MM-dd}");
                        });
                    });

                    // 2. Paciento informacija
                    page.Content().PaddingVertical(20).Column(col =>
                    {
                        col.Item().PaddingBottom(5).Text("GAVĖJAS:").SemiBold();
                        col.Item().Text(vizitas.Pacientas?.Vardas + " " + vizitas.Pacientas?.Pavarde);
                        col.Item().Text(vizitas.Pacientas?.ElPastas);

                        col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                        // 3. Paslaugų lentelė
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(30);
                                columns.RelativeColumn();
                                columns.ConstantColumn(80);
                            });

                            // Lentelės antraštė
                            table.Header(header =>
                            {
                                header.Cell().Element(CellStyle).Text("#");
                                header.Cell().Element(CellStyle).Text("Paslauga");
                                header.Cell().Element(CellStyle).AlignRight().Text("Kaina (€)");

                                static IContainer CellStyle(IContainer container)
                                {
                                    return container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Black);
                                }
                            });

                            // Lentelės turinis
                            int i = 1;
                            foreach (var pro in vizitas.Proceduros)
                            {
                                table.Cell().Element(Padding).Text(i++.ToString());
                                table.Cell().Element(Padding).Text(pro.Pavadinimas);
                                table.Cell().Element(Padding).AlignRight().Text($"{pro.Kaina:F2}");

                                static IContainer Padding(IContainer container) => container.PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten3);
                            }
                        });

                        // 4. Galutinė suma
                        col.Item().AlignRight().PaddingTop(15).Text(t =>
                        {
                            t.Span("IŠ VISO MOKĖTI: ").FontSize(14).SemiBold();
                            t.Span($"{bendraSuma:F2} €").FontSize(14).SemiBold().FontColor(Colors.Black);
                        });
                    });

                    // 5. Poraštė
                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Sąskaita sugeneruota automatiškai. Dėkojame, kad renkatės mūsų kliniką!").FontSize(9).Italic();
                    });
                });
            }).GeneratePdf();
        }
    }
}