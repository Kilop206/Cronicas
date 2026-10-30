namespace Chronicles.Api;

// Core 7e creation by random characteristics. Existing investigators are never regenerated.
public static class CthulhuCreation
{
    private static readonly string[] ThreeDice = ["forca", "constituicao", "destreza", "aparencia", "poder"];
    private static readonly string[] TwoDice = ["tamanho", "inteligencia", "educacao"];
    private static readonly string[] Characteristics = [.. ThreeDice, .. TwoDice];
    public static bool CanGenerate(RuleState s) => s.Get("criacao_fase") == 0 && s.Get("morto") == 0 && Characteristics.All(key =>
        s.Fields.TryGetValue(key, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.Number && value.TryGetInt32(out var number) && number == 0);
    private static int PhysicalReduction(int age) => age < 20 ? 5 : age < 40 ? 0 : age < 50 ? 5 : age < 60 ? 10 : age < 70 ? 20 : age < 80 ? 40 : 80;
    public static RuleAction[] Actions(RuleState s) => [
        .. (CanGenerate(s) ? new RuleAction[] { new("creation-generate", "Criar novo investigador: gerar características e sorte", "Idade (15–89)") } : []),
        .. (s.Get("criacao_fase") == 1 && s.Get("criacao_reducao") > 0 ? new RuleAction[] {
            new("creation-age-reduce", "Distribuir redução de características pela idade", "Pontos a reduzir", s.N("idade") < 20 ? [new("forca", "Força"), new("tamanho", "Tamanho")] : [new("forca", "Força"), new("constituicao", "Constituição"), new("destreza", "Destreza")]) } : []),
        .. (s.Get("criacao_fase") == 1 ? new RuleAction[] { new("creation-vitals", "Concluir características e aplicar recursos iniciais") } : [])];
    public static RuleStat[] Stats(RuleState s) => s.Get("criacao_fase") == 1 ? [
        new("Criação: redução por idade ainda a distribuir", s.Get("criacao_reducao").ToString()),
        new("Criação: pontos de interesse pessoal", (s.N("inteligencia") * 2).ToString())] : [];
    public static bool Handles(string action) => action is "creation-generate" or "creation-age-reduce" or "creation-vitals";
    public static string Apply(RuleState s, RuleCommand command)
    {
        switch (command.Action)
        {
            case "creation-generate":
                RuleState.Require(CanGenerate(s), "A geração exige uma ficha nova com as oito características zeradas. Investigadores existentes devem usar edição e histórico.");
                RuleState.Require(command.Amount is >= 15 and <= 89, "A tabela de criação consultada cobre 15 a 89 anos; outras idades exigem decisão do Guardião.");
                foreach (var key in ThreeDice) s.Field(key, s.Pool(3, 6) * 5);
                foreach (var key in TwoDice) s.Field(key, (s.Pool(2, 6) + 6) * 5);
                var age = command.Amount; s.Field("idade", age);
                var luck = s.Pool(3, 6) * 5;
                if (age < 20) luck = Math.Max(luck, s.Pool(3, 6) * 5);
                s.Field("sorte", luck);
                s.Set("criacao_reducao", PhysicalReduction(age)); s.Set("criacao_fase", 1);
                if (age < 20) s.Field("educacao", Math.Max(0, s.N("educacao") - 5));
                else
                {
                    var improvements = age < 40 ? 1 : Math.Min(4, age / 10 - 2);
                    for (var i = 0; i < improvements; i++)
                        if (s.Die(100) > s.N("educacao")) s.Field("educacao", Math.Min(99, s.N("educacao") + s.Die(10)));
                }
                if (age >= 40) s.Field("aparencia", Math.Max(0, s.N("aparencia") - (age / 10 - 3) * 5));
                return "Características e sorte geradas pelo método aleatório 7e; melhorias de EDU aplicadas. Distribua a redução por idade antes de concluir os recursos. Ocupação e perícias são uma etapa separada.";
            case "creation-age-reduce":
                RuleState.Require(s.Get("criacao_fase") == 1 && command.Amount > 0 && command.Amount <= s.Get("criacao_reducao"), "Informe uma redução positiva dentro do saldo de idade.");
                var keyToReduce = command.Option!;
                RuleState.Require(s.N(keyToReduce) >= command.Amount, "A redução não pode deixar uma característica negativa.");
                RuleState.Require(keyToReduce is not ("constituicao" or "tamanho") || s.N(keyToReduce) > command.Amount, "Constituição e Tamanho precisam permanecer positivos para um investigador vivo.");
                s.Field(keyToReduce, s.N(keyToReduce) - command.Amount); s.Set("criacao_reducao", s.Get("criacao_reducao") - command.Amount);
                return "Redução de idade aplicada à característica escolhida.";
            case "creation-vitals":
                RuleState.Require(s.Get("criacao_fase") == 1 && s.Get("criacao_reducao") == 0, "Distribua toda a redução de características pela idade antes de concluir.");
                var health = (s.N("constituicao") + s.N("tamanho")) / 10;
                var magic = s.N("poder") / 5; var sanity = Math.Min(s.N("poder"), Math.Max(0, 99 - s.Get("mitos")));
                RuleState.Require(health > 0, "A combinação de características não fornece PV positivos; revise-a com o Guardião.");
                s.Vitals(health, magic, 99 - s.Get("mitos")); s.Sanity = sanity; s.Set("criacao_fase", 2);
                return "Recursos iniciais aplicados: PV, PM e SAN. As características não serão geradas novamente por esse comando. Defina ocupação, perícias e história antes de jogar.";
            default: throw new ArgumentException("Etapa de criação desconhecida.");
        }
    }
}
