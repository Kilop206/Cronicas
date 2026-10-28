using Chronicles.Api;
using Chronicles.Api.Services;

static void Check(bool condition, string name)
{
    if (!condition) throw new Exception($"Falhou: {name}");
}

foreach (var invalid in new[] { "", "1d3", "0d20", "41d6", "1d20kh1", "2d20kh2", "2d20+10000", "1d20;process.exit()", "2d6++2" })
    Check(!DiceExpression.TryParse(invalid, out _), $"Rejeita {invalid}");

Check(DiceExpression.TryParse("2d20kh1+5", out var high) && high is not null, "Analisa vantagem");
var draws = new Queue<int>([3, 17]);
var result = high!.Roll(_ => draws.Dequeue());
Check(result.Total == 22 && result.Rolls.SequenceEqual([3, 17]) && result.KeptIndexes.SequenceEqual([1]), "Maior dado + modificador");

Check(DiceExpression.TryParse("2d20kl1-2", out var low) && low is not null, "Analisa menor dado");
draws = new Queue<int>([3, 17]);
result = low!.Roll(_ => draws.Dequeue());
Check(result.Total == 1 && result.KeptIndexes.SequenceEqual([0]), "Menor dado - modificador");

Check(DiceExpression.TryParse("4d8", out var pool) && pool is not null, "Analisa pool");
draws = new Queue<int>([1, 2, 3, 4]);
result = pool!.Roll(_ => draws.Dequeue());
Check(result.Total == 10 && result.KeptIndexes.SequenceEqual([0, 1, 2, 3]), "Soma pool completo");

Check(DiceExpression.TryParse("1d100", out var percentile) && percentile is not null, "Analisa d100");
Check(percentile!.Roll(_ => 100).Total == 100, "Limite superior d100");
var percentileDraws = new Queue<int>([0, 0, 4]);
var bonusRoll = PercentileDice.Roll(1, () => percentileDraws.Dequeue());
Check(bonusRoll.Total == 40 && bonusRoll.KeptIndexes.SequenceEqual([0, 2]), "Dado de bonus escolhe menor dezena e zero duplo vale 100");
percentileDraws = new Queue<int>([7, 1, 8]);
var penalty = PercentileDice.Roll(-1, () => percentileDraws.Dequeue());
Check(penalty.Total == 87 && penalty.KeptIndexes.SequenceEqual([0, 2]), "Dado de penalidade escolhe maior dezena");
Check(new PercentileResult(12, [], []).Degree(65) == "extreme" && new PercentileResult(33, [], []).Degree(65) == "regular", "Graus de sucesso percentuais");

var rooms = new RoomRegistry();
var campaign = Guid.NewGuid();
var owner = Guid.NewGuid();
var another = Guid.NewGuid();
var session = Guid.NewGuid();
var ownerPeer = new RoomPeer("owner", campaign, owner, "Owner", "player", "room");
var otherPeer = new RoomPeer("other", campaign, another, "Other", "player", "room");
var obsPeer = new RoomPeer("obs", campaign, another, "OBS", "player", "obs", TargetUserId: owner);
Check(rooms.Join(ownerPeer, session).Count == 0, "Primeiro participante da sala");
Check(rooms.Join(otherPeer, session).Single().ConnectionId == "owner", "Participantes da sala se conectam");
Check(rooms.Join(obsPeer, session).Single().ConnectionId == "owner", "OBS encontra somente o proprietario do personagem");
Check(rooms.SameCampaign("owner", "obs", campaign), "Proprietario pode sinalizar ao OBS");
Check(!rooms.SameCampaign("other", "obs", campaign), "Outro participante nao pode sinalizar ao OBS");
Check(!rooms.SameCampaign("obs", "other", campaign), "OBS nao pode sinalizar a outro participante");
Check(!rooms.SameCampaign("owner", "obs", Guid.NewGuid()), "Sinalizacao bloqueada entre campanhas");

var field = new SystemField("forca", "Força", "number", "Atributos", 0, 20, null, 4);
var layout = new SheetLayout("#8d6c9e", [new SheetPage("principal", "Principal", [
    new SheetBlock("identidade", "identity", 8),
    new SheetBlock("atributo", "field", 4, Key: "forca"),
    new SheetBlock("vida", "resource", 6, Key: "health")
])]);
Check(SystemDefinition.ValidateLayout(layout, [field]) is null, "Layout de ficha valido");
Check(SystemDefinition.ValidateLayout(layout with { Pages = [layout.Pages[0] with { Blocks = [layout.Pages[0].Blocks[0], layout.Pages[0].Blocks[1] with { Style = "score" }, layout.Pages[0].Blocks[2] with { Style = "compact" }] }] }, [field]) is null, "Estilos compactos de ficha validos");
var stored = System.Text.Json.JsonSerializer.Serialize(new SystemDefinition([field], layout));
Check(SystemDefinition.Read(stored).Layout?.Pages[0].Blocks.Length == 3, "Layout persiste no esquema");
Check(SystemDefinition.Read(System.Text.Json.JsonSerializer.Serialize(new[] { field })).Layout is null, "Esquema antigo permanece legivel");
Check(SystemDefinition.ValidateLayout(layout with { Pages = [new SheetPage("principal", "Principal", [new SheetBlock("invalido", "field", 6, Key: "ausente")])] }, [field]) is not null, "Referencia de campo invalida rejeitada");
var current = field with { Key = "energia", Label = "Energia" };
var maximum = field with { Key = "energia_maxima", Label = "Energia máxima" };
var customResource = new SheetLayout("#8d6c9e", [new SheetPage("principal", "Principal", [new SheetBlock("energia", "resource", 6, Key: "energia", MaxKey: "energia_maxima")])]);
Check(SystemDefinition.ValidateLayout(customResource, [current, maximum]) is null, "Recurso com campos numericos proprios valido");
Check(SystemDefinition.ValidateLayout(customResource with { Pages = [new SheetPage("principal", "Principal", [new SheetBlock("energia", "resource", 6, Key: "energia", MaxKey: "inexistente")])] }, [current, maximum]) is not null, "Recurso com maximo inexistente rejeitado");
var canvas = new SheetLayout("#8d6c9e", [new SheetPage("principal", "Principal", [
    new SheetBlock("identidade", "identity", 12, X: 5, Y: 8, Width: 60, Height: 20)
], "canvas", "https://example.org/fundo.png", 0.75)]);
Check(SystemDefinition.ValidateLayout(canvas, []) is null, "Pagina livre com fundo HTTPS valida");
Check(SystemDefinition.ValidateLayout(canvas with { Pages = [canvas.Pages[0] with { Blocks = [canvas.Pages[0].Blocks[0] with { X = 70 }] }] }, []) is not null, "Bloco fora da pagina livre rejeitado");
Check(SystemDefinition.ValidateLayout(canvas with { Pages = [canvas.Pages[0] with { BackgroundUrl = "javascript:alert(1)" }] }, []) is not null, "URL de fundo insegura rejeitada");
Check(SystemDefinition.ValidateLayout(canvas with { Skin = "investigation" }, []) is null, "Aparencia de investigacao valida");
Check(SystemDefinition.ValidateLayout(canvas with { Skin = "mythic" }, []) is null, "Aparencia de lenda valida");
Check(SystemDefinition.ValidateLayout(canvas with { HeaderLabel = "CRÔNICAS / LENDA" }, []) is null, "Cabecalho proprio valido");
Check(SystemDefinition.ValidateLayout(canvas with { HeaderLabel = "X" }, []) is not null, "Cabecalho curto rejeitado");
Check(SystemDefinition.ValidateLayout(canvas with { Skin = "script" }, []) is not null, "Aparencia desconhecida rejeitada");

var visible = new SystemField("mostrar", "Mostrar", "boolean", "Geral", null, null, null);
var trackerLayout = new SheetLayout("#8d6c9e", [new SheetPage("principal", "Principal", [
    new SheetBlock("circulos", "tracker", 6, Key: "health", VisibleWhenKey: "mostrar", VisibleWhenValue: true),
    new SheetBlock("subsecao", "section", 12, Title: "Detalhes")
])], FontStyle: "typewriter", Density: "compact", BorderStyle: "ornate");
Check(SystemDefinition.ValidateLayout(trackerLayout, [visible]) is null, "Marcadores, secoes, tema e visibilidade validos");
Check(SystemDefinition.ValidateLayout(trackerLayout, []) is not null, "Visibilidade com campo ausente rejeitada");
Check(SystemDefinition.ValidateLayout(trackerLayout with { FontStyle = "script" }, [visible]) is not null, "Fonte nao permitida rejeitada");
var versioned = new SystemDefinition([field], layout, "dnd_5e", 1, "2024", "specialized", 2,
    [new SystemRevision(1, DateTime.UtcNow, "Antigo", "", [field], layout, "dnd_5e", 1, "2024", "specialized")]);
var versionedCopy = SystemDefinition.Read(System.Text.Json.JsonSerializer.Serialize(versioned));
Check(versionedCopy.TemplateId == "dnd_5e" && versionedCopy.TemplateVersion == 1 && versionedCopy.Revision == 2
    && versionedCopy.History?.Length == 1, "Identidade estavel e historico do modelo persistem");
var npcModel = new NpcTemplate("informante", "Informante", Motivation: "Conhece uma pista", Visibility: "gm", Health: 5, MaxHealth: 5);
var withNpcs = versioned with { NpcTemplates = [npcModel] };
Check(SystemDefinition.Read(System.Text.Json.JsonSerializer.Serialize(withNpcs)).NpcTemplates?.Single().Id == "informante", "Modelos de NPC persistem no sistema versionado");
var listField = new SystemField("condicoes", "Condicoes", "list", "Combate", null, null, null);
var tableField = new SystemField("ataques", "Ataques", "table", "Combate", null, null, null,
    Columns: [new SystemColumn("nome", "Nome", "text"), new SystemColumn("bonus", "Bonus", "number")]);
Check(SheetCollectionValues.ValidField(listField, System.Text.Json.JsonSerializer.SerializeToElement(new[] { "Atordoado" })), "Lista validada");
Check(!SheetCollectionValues.ValidField(listField, System.Text.Json.JsonSerializer.SerializeToElement(new[] { 42 })), "Lista rejeita tipo errado");
Check(SheetCollectionValues.ValidField(tableField, System.Text.Json.JsonSerializer.SerializeToElement(new[] { new { nome = "Espada", bonus = 4 } })), "Tabela validada");
Check(!SheetCollectionValues.ValidField(tableField, System.Text.Json.JsonSerializer.SerializeToElement(new[] { new { desconhecida = "x" } })), "Tabela rejeita coluna desconhecida");
var attackTable = tableField with { Columns = [new SystemColumn("nome", "Nome", "text"), new SystemColumn("teste", "Teste", "dice")] };
Check(SheetCollectionValues.ValidField(attackTable, System.Text.Json.JsonSerializer.SerializeToElement(new[] { new { nome = "Lâmina", teste = "1d20+5" } })), "Tabela aceita rolagem segura");
Check(!SheetCollectionValues.ValidField(attackTable, System.Text.Json.JsonSerializer.SerializeToElement(new[] { new { nome = "Lâmina", teste = "1d20;process.exit()" } })), "Tabela rejeita expressao executavel");
var oldValues = new Dictionary<string, System.Text.Json.JsonElement> { ["forca"] = System.Text.Json.JsonSerializer.SerializeToElement(17) };
var renamed = field with { Key = "potencia", Label = "Potencia" };
var migration = SheetSchemaMigration.Apply(oldValues, new Dictionary<string, string> { ["forca"] = "potencia" }, [renamed]);
Check(migration.Error is null && migration.Changed && migration.Values["potencia"].GetInt32() == 17 && !migration.Values.ContainsKey("forca") && oldValues.ContainsKey("forca"), "Migracao preserva valor e origem");
Check(SheetSchemaMigration.Apply(oldValues, new Dictionary<string, string> { ["forca"] = "potencia" }, [renamed with { Max = 10 }]).Error is not null, "Migracao invalida rejeitada");
var gmField = renamed with { EditRole = "gm" };
var afterGmEdit = new Dictionary<string, System.Text.Json.JsonElement> { ["potencia"] = System.Text.Json.JsonSerializer.SerializeToElement(18) };
var beforeGmEdit = new Dictionary<string, System.Text.Json.JsonElement> { ["potencia"] = System.Text.Json.JsonSerializer.SerializeToElement(17) };
Check(SheetFieldAccess.RestrictedChange([gmField], beforeGmEdit, afterGmEdit, false) is not null, "Jogador nao edita campo do mestre");
Check(SheetFieldAccess.RestrictedChange([gmField], beforeGmEdit, afterGmEdit, true) is null, "Mestre edita campo reservado");
Check(SheetFieldAccess.RestrictedChange([gmField], beforeGmEdit, new Dictionary<string, System.Text.Json.JsonElement>(), false) is not null, "Jogador nao apaga campo do mestre");
Check(SheetFieldAccess.RestrictedChange([gmField], beforeGmEdit, beforeGmEdit, false) is null, "Jogador preserva campo do mestre em outros salvamentos");

var level = new SystemField("nivel", "Nível", "number", "Progressão", 0, 100, null);
var experience = new SystemField("experiencia", "Experiência", "number", "Progressão", 0, null, null);
var milestone = new SystemField("marco", "Próximo marco", "number", "Progressão", 0, null, null, Formula: "(nivel + 1) * 100");
var bonus = new SystemField("bonus", "Bônus", "number", "Aptidões", null, null, null, Formula: "nivel + experiencia / 10", RollExpression: "1d20+{bonus}");
var formulaFields = new[] { level, experience, milestone, bonus };
Check(SheetFormula.Validate(formulaFields) is null, "Fórmulas e rolagem dinâmica válidas");
var values = new Dictionary<string, System.Text.Json.JsonElement>
{
    ["nivel"] = System.Text.Json.JsonSerializer.SerializeToElement(3),
    ["experiencia"] = System.Text.Json.JsonSerializer.SerializeToElement(25),
    ["bonus"] = System.Text.Json.JsonSerializer.SerializeToElement(999)
};
Check(SheetFormula.Apply(formulaFields, values) is null && values["marco"].GetInt32() == 400 && values["bonus"].GetInt32() == 5, "Cálculo substitui valor enviado pelo cliente");
Check(SheetFormula.Validate([level with { Formula = "marco" }, milestone with { Formula = "nivel" }]) is not null, "Ciclo entre campos rejeitado");
Check(SheetFormula.Validate([level, bonus with { Formula = "nivel + ausente" }]) is not null, "Referência desconhecida rejeitada");
Check(SheetFormula.Validate([level, bonus with { Formula = "10 / (nivel - 1)" }]) is null, "Divisão dependente de valores aceita no template");
Check(SheetFormula.Apply([level, bonus with { Formula = "10 / (nivel - 1)" }], new Dictionary<string, System.Text.Json.JsonElement> { ["nivel"] = System.Text.Json.JsonSerializer.SerializeToElement(1) }) is not null, "Divisão por zero rejeitada ao calcular");
Check(SheetFormula.Validate([level, bonus with { RollExpression = "1d20+{ausente}" }]) is not null, "Modificador desconhecido rejeitado");
Check(SheetFormula.Evaluate("8 / 3 - 5", _ => 0) == -3, "Divisão inteira e precedência");
Check(SheetFormula.Evaluate("floordiv(9 - 10, 2)", _ => 0) == -1, "Modificador arredondado para baixo");
Check(SheetFormula.Validate([level, bonus with { Formula = "floordiv(nivel - 10, 2)", RollExpression = "{nivel}d20kh1+{bonus}" }]) is null, "Fórmula de modificador e quantidade dinâmica de dados");
var defaultLevel = level with { DefaultValue = System.Text.Json.JsonSerializer.SerializeToElement(1) };
var defaults = new Dictionary<string, System.Text.Json.JsonElement>();
Check(SheetFormula.Apply([defaultLevel, milestone], defaults) is null && defaults["nivel"].GetInt32() == 1 && defaults["marco"].GetInt32() == 200, "Valor inicial aplicado antes do cálculo");

Console.WriteLine("Regras de dados: expressões válidas/inválidas, kh1, kl1, modificadores e d100 passaram.");
