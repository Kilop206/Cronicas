using System.Text.RegularExpressions;

namespace Chronicles.Api;

public sealed record DiceExpression(int Count, int Sides, string? Keep, int Modifier)
{
    private static readonly Regex Pattern = new(@"^(?<count>[1-9][0-9]?)d(?<sides>4|6|8|10|12|20|100)(?<keep>kh1|kl1)?(?<modifier>[+-][0-9]{1,4})?$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static bool TryParse(string? source, out DiceExpression? expression)
    {
        expression = null;
        if (source is null || source.Length > 40) return false;
        var match = Pattern.Match(source.Trim());
        if (!match.Success) return false;
        var count = int.Parse(match.Groups["count"].Value);
        if (count > 40) return false;
        var sides = int.Parse(match.Groups["sides"].Value);
        var keep = match.Groups["keep"].Success ? match.Groups["keep"].Value.ToLowerInvariant() : null;
        if (keep is not null && count < 2) return false;
        var modifier = match.Groups["modifier"].Success ? int.Parse(match.Groups["modifier"].Value) : 0;
        expression = new DiceExpression(count, sides, keep, modifier);
        return true;
    }

    public DiceResult Roll(Func<int, int> draw)
    {
        var rolls = Enumerable.Range(0, Count).Select(_ => draw(Sides)).ToArray();
        if (rolls.Any(value => value < 1 || value > Sides)) throw new ArgumentOutOfRangeException(nameof(draw));
        int[] kept = Keep switch
        {
            "kh1" => [Array.IndexOf(rolls, rolls.Max())],
            "kl1" => [Array.IndexOf(rolls, rolls.Min())],
            _ => Enumerable.Range(0, Count).ToArray()
        };
        return new DiceResult(rolls, kept, rolls.Where((_, index) => kept.Contains(index)).Sum() + Modifier);
    }
}

public sealed record DiceResult(int[] Rolls, int[] KeptIndexes, int Total);
