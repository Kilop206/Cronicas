using System.Text.Json;
using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Api;

public sealed record SystemField(string Key, string Label, string Type, string Section, int? Min, int? Max, string[]? Options);

public static class SystemCatalog
{
    public static readonly Guid GenericId = Guid.Parse("a9e29682-c194-4aae-9c0e-7bbb3f6a72f1");
    public static readonly Guid D20Id = Guid.Parse("b63b9373-858c-49cd-90b6-c9301ed804eb");
    public static readonly Guid PercentileId = Guid.Parse("8771d4d1-2c93-49d7-9ffc-8244ea0be2b9");

    public static async Task SeedAsync(AppDbContext db)
    {
        var known = await db.RpgSystems.Where(x => x.OwnerId == null).Select(x => x.Id).ToListAsync();
        var seeds = new[]
        {
            new RpgSystem { Id = GenericId, Name = "Crônicas Essencial", Description = "Modelo narrativo livre para qualquer cenário.", SchemaJson = JsonSerializer.Serialize(new[]
            {
                new SystemField("origem", "Origem", "text", "Identidade", null, null, null),
                new SystemField("ocupacao", "Ocupação", "text", "Identidade", null, null, null),
                new SystemField("nivel", "Nível", "number", "Progressão", 1, 100, null),
                new SystemField("experiencia", "Experiência", "number", "Progressão", 0, 999999, null),
                new SystemField("historia", "História pessoal", "longtext", "Narrativa", null, null, null)
            }) },
            new RpgSystem { Id = D20Id, Name = "Vinte Lendas", Description = "Exemplo original de aventura com atributos e testes em d20.", SchemaJson = JsonSerializer.Serialize(new[]
            {
                new SystemField("vigor", "Vigor", "number", "Atributos", 1, 30, null),
                new SystemField("agilidade", "Agilidade", "number", "Atributos", 1, 30, null),
                new SystemField("intelecto", "Intelecto", "number", "Atributos", 1, 30, null),
                new SystemField("presenca", "Presença", "number", "Atributos", 1, 30, null),
                new SystemField("nivel", "Nível", "number", "Progressão", 1, 20, null)
            }) },
            new RpgSystem { Id = PercentileId, Name = "Ecos do Centésimo", Description = "Exemplo original de investigação com competências percentuais.", SchemaJson = JsonSerializer.Serialize(new[]
            {
                new SystemField("investigacao", "Investigação", "number", "Competências", 0, 100, null),
                new SystemField("observacao", "Observação", "number", "Competências", 0, 100, null),
                new SystemField("vontade", "Vontade", "number", "Competências", 0, 100, null),
                new SystemField("especialidade", "Especialidade", "text", "Identidade", null, null, null)
            }) }
        };
        foreach (var seed in seeds.Where(x => !known.Contains(x.Id))) db.RpgSystems.Add(seed);
        await db.SaveChangesAsync();
    }
}
