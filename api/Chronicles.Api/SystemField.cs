namespace Chronicles.Api;

public sealed record SystemField(string Key, string Label, string Type, string Section, int? Min, int? Max, string[]? Options, int? Span = null, string? Formula = null, string? RollExpression = null);
