using Chronicles.Api;

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

Console.WriteLine("Regras de dados: expressões válidas/inválidas, kh1, kl1, modificadores e d100 passaram.");
