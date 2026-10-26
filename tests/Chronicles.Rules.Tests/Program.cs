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
var stored = System.Text.Json.JsonSerializer.Serialize(new SystemDefinition([field], layout));
Check(SystemDefinition.Read(stored).Layout?.Pages[0].Blocks.Length == 3, "Layout persiste no esquema");
Check(SystemDefinition.Read(System.Text.Json.JsonSerializer.Serialize(new[] { field })).Layout is null, "Esquema antigo permanece legivel");
Check(SystemDefinition.ValidateLayout(layout with { Pages = [new SheetPage("principal", "Principal", [new SheetBlock("invalido", "field", 6, Key: "ausente")])] }, [field]) is not null, "Referencia de campo invalida rejeitada");

Console.WriteLine("Regras de dados: expressões válidas/inválidas, kh1, kl1, modificadores e d100 passaram.");
