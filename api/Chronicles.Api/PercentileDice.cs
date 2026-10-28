namespace Chronicles.Api;

public sealed record PercentileResult(int Total, int[] Rolls, int[] KeptIndexes)
{
    public string Degree(int target) => Total <= Math.Floor(target / 5d) ? "extreme" :
        Total <= Math.Floor(target / 2d) ? "hard" : Total <= target ? "regular" : "failure";
}

public static class PercentileDice
{
    // The units die is shared by all tens dice. Zero and zero represent 100.
    public static PercentileResult Roll(int bonusDice, Func<int> drawDigit)
    {
        if (bonusDice is < -2 or > 2) throw new ArgumentOutOfRangeException(nameof(bonusDice));
        var rolls = new int[Math.Abs(bonusDice) + 2];
        for (var i = 0; i < rolls.Length; i++)
        {
            rolls[i] = drawDigit();
            if (rolls[i] is < 0 or > 9) throw new ArgumentOutOfRangeException(nameof(drawDigit));
        }
        static int Value(int units, int tens) => units == 0 && tens == 0 ? 100 : units + tens * 10;
        var chosen = 1;
        for (var i = 2; i < rolls.Length; i++)
        {
            var next = Value(rolls[0], rolls[i]);
            var current = Value(rolls[0], rolls[chosen]);
            if (bonusDice > 0 && next < current || bonusDice < 0 && next > current) chosen = i;
        }
        return new PercentileResult(Value(rolls[0], rolls[chosen]), rolls, [0, chosen]);
    }
}
