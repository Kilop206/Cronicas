using System.Text.Json;
using Chronicles.Api;

public static class DndClassResourceTests
{
    public static void Run()
    {
        var system = new SystemDefinition([new("estado_regras", "Estado", "table", "Regras", null, null, null), new("res_sabedoria", "Sabedoria", "number", "Resistências", null, null, null, RollExpression: "1d20+{res_sabedoria}"), new("iniciativa", "Iniciativa", "number", "Combate", null, null, null, RollExpression: "1d20+{iniciativa}")], null, "dnd_5e", 4, "2014");
        CharacterInput Character(params (string Key, int Level)[] levels)
        {
            var character = new CharacterInput("Teste", null, null, 5, 30, 0, 0, 0, 0, new() { ["mod_carisma"] = JsonSerializer.SerializeToElement(3), ["res_sabedoria"] = JsonSerializer.SerializeToElement(2), ["iniciativa"] = JsonSerializer.SerializeToElement(1) });
            var state = new RuleState(character); foreach (var item in levels) state.Set("nivel_" + item.Key, item.Level); Dnd2014Rules.SyncSlots(state); return state.Export();
        }
        RuleResult Apply(CharacterInput c, string action, int amount = 0, string? option = null) => SheetRuleActions.Apply(system, c, new(action, amount, option), _ => 5);
        RuleResource Pool(CharacterInput c, string key) => SheetRuleActions.Describe(system, c).Resources!.Single(r => r.Id == key);
        void Check(bool test, string label) { if (!test) throw new InvalidOperationException("D&D class resources: " + label); }
        void Reject(Action action, string label) { try { action(); } catch (ArgumentException) { return; } throw new InvalidOperationException("Expected rejection: " + label); }
        foreach (var (level, uses) in new[] {(1,2),(3,3),(6,4),(12,5),(17,6)}) Check(Pool(Character(("barbaro",level)),"furia").Maximum == uses, "rage class-level tiers");
        Check(Pool(Character(("barbaro",20)),"furia").Maximum is null, "barbarian 20 has unlimited rage");
        var bard = Apply(Character(("bardo",4)), "class-spend", 2, "inspiracao").Character;
        Check(Pool(Apply(bard,"short-rest").Character,"inspiracao").Current == 1, "bard below five does not regain inspiration on short rest");
        bard = Apply(Character(("bardo",5)), "class-spend", 3, "inspiracao").Character;
        Check(Pool(Apply(bard,"short-rest").Character,"inspiracao").Current == 3, "Font of Inspiration begins at class five");
        var multiclass = Character(("clerigo",6),("paladino",4));
        Check(Pool(multiclass,"canalizar").Maximum == 2, "multiclass channel pool is shared, not added");
        multiclass = Apply(multiclass,"class-spend",2,"canalizar").Character;
        Reject(() => Apply(multiclass,"class-spend",1,"canalizar"), "shared channel exhausted");
        Check(Pool(Apply(multiclass,"short-rest").Character,"canalizar").Current == 2, "channel short-rest recharge");
        var fighter = Character(("guerreiro",17));
        var wind = Apply(fighter,"second-wind"); Check(wind.Character.Health == 27 && wind.Rolls[0].Expression == "1d10+17" && wind.Rolls[0].Total == 22, "Second Wind heals d10 plus fighter level and persists complete roll composition");
        Reject(() => Apply(wind.Character,"second-wind"), "Second Wind requires rest");
        fighter = Apply(fighter,"class-spend",1,"surto").Character;
        Reject(() => Apply(fighter,"class-spend",1,"surto"), "surge limited to once per turn");
        fighter = Apply(fighter,"class-turn").Character; fighter = Apply(fighter,"class-spend",1,"surto").Character;
        fighter = Apply(fighter,"class-turn").Character;
        Reject(() => Apply(fighter,"class-spend",1,"surto"), "fighter seventeen has only two surge uses");
        Check(Pool(Apply(fighter,"short-rest").Character,"surto").Current == 2, "surge restores after short rest");
        var reroll = Apply(Character(("guerreiro",9)),"indomitable",15,"res_sabedoria");
        Check(reroll.Rolls.Length == 1 && Pool(reroll.Character,"indomavel").Current == 0, "Indomitable rerolls actual save and spends a use");
        var monk = Apply(Character(("monge",5)),"class-spend",5,"ki").Character;
        Check(Pool(Apply(monk,"short-rest").Character,"ki").Current == 0, "unconfirmed meditation does not restore Ki");
        Check(Pool(Apply(monk,"short-rest-meditation").Character,"ki").Current == 5, "meditated short rest restores Ki");
        Check(Pool(Apply(monk,"long-rest",option:"confirmed").Character,"ki").Current == 0, "long rest also requires meditation for Ki");
        Check(Pool(Apply(monk,"long-rest",option:"meditated").Character,"ki").Current == 5, "meditated long rest restores Ki");
        var paladin = Apply(Character(("paladino",3)),"lay-hands-self",7).Character;
        Check(paladin.Health == 12 && Pool(paladin,"cura_maos").Current == 8, "Lay on Hands heals self and decrements reserve");
        var sorcerer = Character(("feiticeiro",7)); var baseSlots = new RuleState(sorcerer).N("espacos_magia_1");
        sorcerer = Apply(sorcerer,"sorcery-create",5).Character;
        Check(new RuleState(sorcerer).N("espacos_magia_5") == 1 && Pool(sorcerer,"feiticaria").Current == 0, "flexible casting creates fifth level at cost seven, even above base casting level");
        sorcerer = Apply(sorcerer,"cast",5).Character;
        Check(new RuleState(sorcerer).N("espacos_magia_5") == 0, "created slot may be spent normally");
        sorcerer = Apply(sorcerer,"sorcery-convert",1,"spell").Character;
        Check(Pool(sorcerer,"feiticaria").Current == 1 && new RuleState(sorcerer).N("espacos_magia_1") == baseSlots - 1, "converting a spell slot consumes it atomically");
        sorcerer = Apply(sorcerer,"long-rest",option:"confirmed").Character;
        Check(Pool(sorcerer,"feiticaria").Current == 7 && new RuleState(sorcerer).N("espacos_magia_5") == 0 && new RuleState(sorcerer).Get("slot_criado_5") == 0, "created slots expire at long rest without changing base progression");
        var editedSlots = new RuleState(Character(("feiticeiro",3))); editedSlots.Field("espacos_magia_1", 1);
        var withCreatedSlot = Apply(editedSlots.Export(),"sorcery-create",1).Character; var resynced = new RuleState(withCreatedSlot); Dnd2014Rules.SyncSlots(resynced);
        Check(resynced.N("espacos_magia_1") == 2, "creating a slot reconciles manually edited balances without refilling spent base slots");
        Reject(() => Apply(sorcerer,"class-spend",0,"feiticaria"), "zero spend does not silently consume a point");
        var mixed = Apply(Character(("feiticeiro",3),("bruxo",3)),"class-spend",3,"feiticaria").Character;
        mixed = Apply(mixed,"sorcery-convert",option:"pact").Character;
        Check(Pool(mixed,"feiticaria").Current == 2 && new RuleState(mixed).Get("pacto_usado") == 1, "pact conversion uses calculated pact level, not caller amount");
        var supreme = Apply(Character(("feiticeiro",20)),"class-spend",10,"feiticaria").Character;
        Check(Pool(Apply(supreme,"short-rest").Character,"feiticaria").Current == 14, "sorcerer twenty recovers four on short rest");
        var wizard = Apply(Character(("mago",5)),"cast",2).Character;
        Reject(() => Apply(wizard,"arcane-start"), "Arcane Recovery requires actual short rest confirmation");
        wizard = Apply(wizard,"short-rest").Character; wizard = Apply(wizard,"arcane-start").Character;
        Check(new RuleState(wizard).Get("arcana_saldo") == 3, "Arcane Recovery rounds wizard half level upward");
        wizard = Apply(wizard,"arcane-slot",2).Character;
        Check(new RuleState(wizard).Get("arcana_saldo") == 1 && new RuleState(wizard).N("espacos_magia_2") == 3, "recovering level two spends two budget and restores one slot");
        Reject(() => Apply(wizard,"arcane-slot",2), "Arcane Recovery cannot exceed remaining budget");
        wizard = Apply(wizard,"arcane-end").Character; wizard = Apply(wizard,"short-rest").Character;
        Reject(() => Apply(wizard,"arcane-start"), "Arcane Recovery only once per game day");
        wizard = Apply(wizard,"class-day").Character; wizard = Apply(wizard,"short-rest").Character; wizard = Apply(wizard,"arcane-start").Character;
        Reject(() => Apply(wizard,"arcane-slot",6), "Arcane Recovery never restores level six or higher");
        wizard = Apply(wizard,"test",option:"iniciativa").Character;
        Reject(() => Apply(wizard,"arcane-slot",1), "unused budget cannot be carried past another activity");
        var highBard = Apply(Character(("bardo",20)),"class-spend",3,"inspiracao").Character;
        Check(Pool(Apply(highBard,"test",option:"iniciativa").Character,"inspiracao").Current == 1, "bard twenty restores one inspiration only when empty at initiative");
        var reducedCharisma = new RuleState(highBard); reducedCharisma.Field("mod_carisma", -2);
        Check(Pool(Apply(reducedCharisma.Export(),"test",option:"iniciativa").Character,"inspiracao").Current == 1, "inspiration recovery respects reduced capacity, without fictitious over-capacity debt");
        var releveledSorcerer = Apply(Character(("feiticeiro",20)),"class-spend",20,"feiticaria").Character;
        releveledSorcerer = Apply(releveledSorcerer,"configure",3,"nivel_feiticeiro").Character;
        Check(Pool(Apply(releveledSorcerer,"sorcery-convert",1,"spell").Character,"feiticaria").Current == 1, "recovery works after an explicit reduction of configured class level");
        var highMonk = Apply(Character(("monge",20)),"class-spend",20,"ki").Character;
        Check(Pool(Apply(highMonk,"test",option:"iniciativa").Character,"ki").Current == 4, "monk twenty restores four Ki only when empty at initiative");
        var warlock = Apply(Character(("bruxo",20)),"class-spend",1,"arcana_6").Character;
        Check(Pool(Apply(warlock,"short-rest").Character,"arcana_6").Current == 0 && Pool(Apply(warlock,"long-rest",option:"confirmed").Character,"arcana_6").Current == 1, "Mystic Arcanum is not a pact slot and only returns on long rest");
        var usedPact = Apply(warlock,"pact-cast").Character; usedPact = Apply(usedPact,"eldritch-master").Character;
        Check(new RuleState(usedPact).Get("pacto_usado") == 0 && Pool(usedPact,"mestre_mistico").Current == 0, "Eldritch Master refreshes pact, then remains spent");
        var invalid = new RuleState(Character(("monge",5))); invalid.Set("classe_usado_ki", -1);
        Check(SheetRuleActions.ValidateStateAccess(system, [], invalid.Export().Fields!, false) is not null, "negative spent counters rejected in import/save paths");
        Console.WriteLine("D&D class resource tiers, rest timing, multiclass, slot conversion, Arcane Recovery and initiative recovery passed.");
    }
}
