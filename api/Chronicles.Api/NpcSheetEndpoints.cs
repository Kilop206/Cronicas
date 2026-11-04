using System.Text.Json;
using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Api;

public static partial class NpcEndpoints
{
    private sealed record NpcSnapshot(int Version,DateTime SavedAt,Guid EditedById,NpcInput Data);
    public sealed record NpcRuleInput(int ExpectedVersion,ThreatCommand Command);
    public sealed record NpcRestoreInput(int ExpectedVersion);
    private static NpcInput Input(Npc n) => new(n.Name,n.PortraitUrl,n.Appearance,n.Personality,n.Motivation,n.Relationships,n.Location,n.Secrets,n.Status,n.Visibility,n.Health,n.MaxHealth,n.Version,ParanormalThreatSchema.Read(n.SheetJson));
    internal static void Capture(Npc npc,Guid actor)
    {
        var history=JsonSerializer.Deserialize<List<NpcSnapshot>>(npc.HistoryJson)??[];
        history.Insert(0,new(npc.Version,DateTime.UtcNow,actor,Input(npc)));
        npc.HistoryJson=JsonSerializer.Serialize(history.Take(20));
    }
    private static IResult SheetError(string message)=>Results.ValidationProblem(new Dictionary<string,string[]>{["sheet"]=[message]});
    private static async Task<string?> SheetContextError(AppDbContext db,Guid campaignId,ParanormalThreatSheet? sheet,string? previous)
    {
        if(sheet is null)return null;
        if(ParanormalThreatSchema.Validate(sheet) is {} error)return error;
        if(ParanormalThreatSchema.Read(previous) is {} old)
            return sheet.SystemId is not null && sheet.SystemId!=old.SystemId?"A ficha não pode trocar silenciosamente o sistema de origem.":null;
        var schema=await db.Campaigns.Where(c=>c.Id==campaignId).Join(db.RpgSystems,c=>c.SystemId,s=>s.Id,(_,s)=>s.SchemaJson).SingleOrDefaultAsync();
        return schema is null || !ParanormalThreatSchema.CampaignSupports(SystemDefinition.Read(schema))?"Associe a campanha a Ordem Paranormal 1.3 antes de criar uma ficha de ameaça dessa edição.":null;
    }
    private static async Task<NpcInput> BindSheetSystem(AppDbContext db,Guid campaignId,NpcInput input,string? previous=null)
    {
        if(input.Sheet is null)return input;
        var id=ParanormalThreatSchema.Read(previous)?.SystemId??await db.Campaigns.Where(c=>c.Id==campaignId).Select(c=>c.SystemId).SingleAsync();
        return input with{Sheet=input.Sheet with{SystemId=id}};
    }
    internal static void AdjustHealingLedger(Npc npc,int previousHealth)
    {
        if(ParanormalThreatSchema.Read(npc.SheetJson) is not {} sheet)return;
        var state=new Dictionary<string,int>(sheet.State);
        var blocked=Math.Clamp(state.GetValueOrDefault("cura_impedida")-Math.Max(0,npc.Health-previousHealth),0,npc.MaxHealth-npc.Health);
        if(blocked>0)state["cura_impedida"]=blocked;else state.Remove("cura_impedida");npc.SheetJson=JsonSerializer.Serialize(sheet with{State=state});
    }
    internal static string? ConditionText(Npc npc)
    {
        var sheet=ParanormalThreatSchema.Read(npc.SheetJson);
        if(sheet is null)return null;var state=ParanormalThreatSchema.ToState(sheet,npc.Health,npc.MaxHealth);var active=D20Conditions.Effective(state);
        return string.Join(", ",D20Conditions.Choices(true).Where(c=>active.Contains(c.Value)).Select(c=>c.Label));
    }
    internal static async Task SyncNpcParticipants(AppDbContext db,Npc npc)
    {
        var conditions=ConditionText(npc);
        foreach(var participant in await db.EncounterParticipants.Where(p=>p.NpcId==npc.Id && p.Encounter.Status!="completed").ToListAsync())
        {
            participant.Health=npc.Health;participant.MaxHealth=npc.MaxHealth;if(conditions is not null)participant.Conditions=conditions;
        }
    }
    private static void MapNpcSheets(RouteGroupBuilder api)
    {
        api.MapPost("/campaigns/{campaignId:guid}/npcs/validate-sheet",async Task<IResult>(Guid campaignId,ParanormalThreatSheet sheet,HttpContext http,AppDbContext db)=>
        {
            if(UserId(http) is not {} userId)return Results.Unauthorized();var role=await Role(db,campaignId,userId);if(role is null)return Results.NotFound();if(!Master(role))return Results.Problem("Somente mestres validam modelos de ameaças.",statusCode:403);
            if(ParanormalThreatSchema.Validate(sheet) is {} error)return SheetError(error);
            return Results.Ok(sheet);
        });
        api.MapGet("/npcs/{id:guid}/revisions",async Task<IResult>(Guid id,HttpContext http,AppDbContext db)=>
        {
            if(UserId(http) is not {} userId)return Results.Unauthorized();
            var npc=await db.Npcs.AsNoTracking().SingleOrDefaultAsync(n=>n.Id==id);if(npc is null)return Results.NotFound();
            var role=await Role(db,npc.CampaignId,userId);if(role is null)return Results.NotFound();if(!Master(role))return Results.Problem("Histórico reservado aos mestres.",statusCode:403);
            return Results.Ok((JsonSerializer.Deserialize<List<NpcSnapshot>>(npc.HistoryJson)??[]).Select(s=>new{s.Version,s.SavedAt,s.EditedById,name=s.Data.Name}));
        });
        api.MapPost("/npcs/{id:guid}/revisions/{version:int}/restore",async Task<IResult>(Guid id,int version,NpcRestoreInput input,HttpContext http,AppDbContext db)=>
        {
            if(UserId(http) is not {} userId)return Results.Unauthorized();var npc=await db.Npcs.SingleOrDefaultAsync(n=>n.Id==id);if(npc is null)return Results.NotFound();
            var role=await Role(db,npc.CampaignId,userId);if(role is null)return Results.NotFound();if(!Master(role))return Results.Problem("Somente mestres restauram NPCs.",statusCode:403);
            if(input.ExpectedVersion!=npc.Version)return Results.Conflict(new{message="O NPC mudou. Atualize antes de restaurar."});
            var snapshot=(JsonSerializer.Deserialize<List<NpcSnapshot>>(npc.HistoryJson)??[]).SingleOrDefault(s=>s.Version==version);if(snapshot is null)return Results.NotFound();
            var errors=Validate(snapshot.Data);if(errors.Count>0)return Results.ValidationProblem(errors);
            Capture(npc,userId);Apply(npc,snapshot.Data);npc.SheetJson=snapshot.Data.Sheet is null?null:JsonSerializer.Serialize(snapshot.Data.Sheet);npc.Version++;npc.UpdatedAt=DateTime.UtcNow;
            await SyncNpcParticipants(db,npc);
            try{await db.SaveChangesAsync();}catch(DbUpdateConcurrencyException){return Results.Conflict(new{message="O NPC mudou; restauração não aplicada."});}
            return Results.Ok(View(npc,true));
        });
        api.MapPost("/npcs/{id:guid}/rules",async Task<IResult>(Guid id,NpcRuleInput input,HttpContext http,AppDbContext db)=>
        {
            if(UserId(http) is not {} userId)return Results.Unauthorized();var npc=await db.Npcs.SingleOrDefaultAsync(n=>n.Id==id);if(npc is null)return Results.NotFound();
            var role=await Role(db,npc.CampaignId,userId);if(role is null)return Results.NotFound();if(!Master(role))return Results.Problem("Somente mestres controlam ameaças.",statusCode:403);
            if(input.ExpectedVersion!=npc.Version)return Results.Conflict(new{message="A ameaça mudou. Atualize antes de executar a ação."});
            if(input.Command is null)return SheetError("Informe uma ação.");
            var sheet=ParanormalThreatSchema.Read(npc.SheetJson);if(sheet is null)return SheetError("Este NPC não tem uma ficha de ameaça.");
            if(input.Command.Action=="presence")return await ApplyPresence(db,npc,sheet,input.Command,userId);
            ThreatResult result;
            try{result=ParanormalThreatRules.Apply(sheet,npc.Health,npc.MaxHealth,input.Command,sides=>System.Security.Cryptography.RandomNumberGenerator.GetInt32(1,sides+1));}
            catch(ArgumentException e){return SheetError(e.Message);}
            if(ParanormalThreatSchema.Validate(result.Sheet) is {} validation)return SheetError(validation);
            var nextJson=JsonSerializer.Serialize(result.Sheet);
            if(npc.Health!=result.Health || !JsonElement.DeepEquals(JsonSerializer.Deserialize<JsonElement>(npc.SheetJson!),JsonSerializer.Deserialize<JsonElement>(nextJson)))
            {
                Capture(npc,userId);npc.Health=result.Health;npc.SheetJson=nextJson;npc.Version++;npc.UpdatedAt=DateTime.UtcNow;await SyncNpcParticipants(db,npc);
            }
            else db.Entry(npc).Property(n=>n.Version).IsModified=true;
            SaveRolls(db,npc.CampaignId,userId,input.Command.Visibility,result.Rolls);
            try{await db.SaveChangesAsync();}catch(DbUpdateConcurrencyException){return Results.Conflict(new{message="A ameaça mudou; ação não aplicada."});}
            return Results.Ok(new{npc=View(npc,true),result.Message,result.Rolls,result.Attacks,result.Damage});
        }).RequireRateLimiting("rolls");
    }
    private static void SaveRolls(AppDbContext db,Guid campaignId,Guid userId,string visibility,IEnumerable<RuleRoll> rolls)
    {
        foreach(var roll in rolls)db.DiceRolls.Add(new DiceRoll{CampaignId=campaignId,UserId=userId,RequestId=Guid.NewGuid(),Expression=roll.Expression,RollsJson=JsonSerializer.Serialize(roll.Rolls),KeptIndexesJson=JsonSerializer.Serialize(roll.KeptIndexes),Modifier=roll.Modifier,Total=roll.Total,Visibility=visibility});
    }
    private static async Task<IResult> ApplyPresence(AppDbContext db,Npc npc,ParanormalThreatSheet sheet,ThreatCommand command,Guid userId)
    {
        if(ParanormalThreatSchema.Validate(sheet) is {} error)return SheetError(error);
        if(!sheet.Presence.Enabled || sheet.Kind!="paranormal" || command.TargetId is null || command.TargetVersion is null || command.AdditionalCreatures is <0 or >20 || command.Visibility is not("gm" or "private" or "campaign"))return SheetError("Configure a presença, o alvo, a versão e até vinte criaturas adicionais.");
        var target=await db.Characters.SingleOrDefaultAsync(c=>c.Id==command.TargetId && c.CampaignId==npc.CampaignId);if(target is null)return Results.NotFound();
        if(target.Version!=command.TargetVersion)return Results.Conflict(new{message="A ficha do alvo mudou. Atualize antes de resolver a presença."});
        db.Entry(target).Property(c=>c.Version).IsModified=true;
        if(sheet.PresenceTargets.Contains(target.Id))return Results.Conflict(new{message="Presença já resolvida para este alvo nesta cena. Inicie uma nova cena para resolver novamente."});
        if(sheet.PresenceTargets.Length>=100)return SheetError("Limite de cem alvos por cena atingido.");
        var schema=await db.Campaigns.Where(c=>c.Id==npc.CampaignId).Join(db.RpgSystems,c=>c.SystemId,s=>s.Id,(_,s)=>s.SchemaJson).SingleOrDefaultAsync();
        if(schema is null || !ParanormalThreatSchema.CampaignSupports(SystemDefinition.Read(schema)))return SheetError("O alvo precisa usar Ordem Paranormal 1.3.");
        var definition=SystemDefinition.Read(schema);
        if(new[]{"nex","presenca","per_vontade"}.Any(key=>!definition.Fields.Any(f=>f.Key==key&&f.Type=="number")) || !definition.Fields.Any(f=>f.Key==SheetRuleActions.StateKey&&f.Type=="table"))return SheetError("O sistema do alvo precisa declarar NEX, Presença, Vontade e estado de regras para resolver a presença.");
        var fields=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(target.FieldsJson)??[];
        var s=new RuleState(new CharacterEndpoints.CharacterInput(target.Name,target.PortraitUrl,target.BackgroundUrl,target.Health,target.MaxHealth,target.Sanity,target.MaxSanity,target.Mana,target.MaxMana,fields),sides=>System.Security.Cryptography.RandomNumberGenerator.GetInt32(1,sides+1));
        void MarkPresence(){Capture(npc,userId);npc.SheetJson=JsonSerializer.Serialize(sheet with{PresenceTargets=[..sheet.PresenceTargets,target.Id]});npc.Version++;npc.UpdatedAt=DateTime.UtcNow;}
        if(sheet.Presence.ImmuneNex is {} cutoff && s.N("nex")>=cutoff)
        {
            MarkPresence();try{await db.SaveChangesAsync();}catch(DbUpdateConcurrencyException){return Results.Conflict(new{message="A ameaça mudou; presença não registrada."});}
            return Results.Ok(new{npc=View(npc,true),message="Alvo imune pelo NEX configurado; nenhum dado rolado.",rolls=Array.Empty<RuleRoll>(),targetId=target.Id,targetVersion=target.Version,sanity=target.Sanity});
        }
        try
        {
            var check=s.Roll(D20Checks.ParanormalExpression(s,"presenca",s.ParanormalSkill("per_vontade"),key:"per_vontade"));var passed=D20Checks.Passed(s,check,sheet.Presence.Difficulty,true);
            var damageState=ParanormalThreatSchema.ToState(sheet,npc.Health,npc.MaxHealth,sides=>System.Security.Cryptography.RandomNumberGenerator.GetInt32(1,sides+1));
            var raw=Math.Max(0,DamageFormula.Evaluate(sheet.Presence.Damage,damageState))+(command.AdditionalCreatures>0?damageState.Pool(command.AdditionalCreatures,6):0);
            var applied=passed?raw/2:raw;var before=s.Sanity;s.Sanity-=applied;if(s.Sanity==0 && applied>0)s.Set("enlouquecendo",1);
            var nextFields=JsonSerializer.Serialize(s.Export().Fields);
            RuleState.Require(nextFields.Length<=20000&&s.Export().Fields!.Count<=100,"O resultado excede o limite de campos da ficha do alvo.");
            if(s.Sanity!=before || !JsonElement.DeepEquals(JsonSerializer.Deserialize<JsonElement>(target.FieldsJson),JsonSerializer.Deserialize<JsonElement>(nextFields))){CharacterHistory.Capture(db,target,userId);target.Sanity=s.Sanity;target.FieldsJson=nextFields;target.UpdatedAt=DateTime.UtcNow;}
            var rolls=s.Rolls.Concat(damageState.Rolls).ToArray();SaveRolls(db,npc.CampaignId,userId,command.Visibility,rolls);
            MarkPresence();
            await db.SaveChangesAsync();return Results.Ok(new{npc=View(npc,true),message=$"Presença: Vontade {check}/DT {sheet.Presence.Difficulty}; dano mental {raw} → {applied}. {s.Sanity} SAN restante(s).",rolls,targetId=target.Id,targetVersion=target.Version,sanity=target.Sanity});
        }
        catch(ArgumentException e){return SheetError(e.Message);}
        catch(DbUpdateConcurrencyException){return Results.Conflict(new{message="A ameaça ou o alvo mudou; presença não aplicada."});}
    }
}
