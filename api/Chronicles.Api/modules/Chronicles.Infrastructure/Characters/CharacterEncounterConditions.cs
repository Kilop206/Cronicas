using System.Text.Json;
using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Api;

public static class CharacterEncounterConditions
{
    public static async Task<SystemDefinition?> Definition(AppDbContext db,Guid campaignId)
    {
        var schema=await db.Campaigns.Where(c=>c.Id==campaignId).Join(db.RpgSystems,c=>c.SystemId,s=>s.Id,(_,s)=>s.SchemaJson).SingleOrDefaultAsync();
        return schema is null?null:SystemDefinition.Read(schema);
    }
    public static string Project(Character character,SystemDefinition definition,string previous)
    {
        if(!ParanormalThreatSchema.CampaignSupports(definition)||!SheetRuleActions.Supports(definition))return previous;
        var fields=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(character.FieldsJson)??[];
        var state=new RuleState(new CharacterSheetState(character.Name,null,null,character.Health,character.MaxHealth,character.Sanity,character.MaxSanity,character.Mana,character.MaxMana,fields));
        var active=D20Conditions.Effective(state);
        var labels=D20Conditions.Choices(true).Where(c=>active.Contains(c.Value)).Select(c=>"Ficha: "+c.Label);
        var notes=previous.Split(',').Select(t=>t.Trim()).Where(t=>t.Length>0&&!t.StartsWith("Ficha: ",StringComparison.Ordinal));
        var result=string.Join(", ",notes.Concat(labels));
        RuleState.Require(result.Length<=1000,"As anotações de condições do encontro estão cheias. Reduza o texto antes de sincronizar a ficha.");
        return result;
    }
    // Prefix separates engine-owned tags from the encounter's existing free text.
    public static async Task Sync(AppDbContext db,Character character,SystemDefinition definition)
    {
        if(!ParanormalThreatSchema.CampaignSupports(definition)||!SheetRuleActions.Supports(definition))return;
        foreach(var p in await db.EncounterParticipants.Where(p=>p.CharacterId==character.Id&&p.Encounter.Status!="completed").ToListAsync())
        {
            p.Conditions=Project(character,definition,p.Conditions);
        }
    }
}
