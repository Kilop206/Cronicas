using System.Security.Cryptography;
using System.Text.Json;
using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Chronicles.Api;

public static partial class NpcEndpoints
{
    private static void MapNpcManeuvers(RouteGroupBuilder api)
    {
        api.MapPost("/npcs/{id:guid}/maneuvers",async Task<IResult>(Guid id,ThreatManeuverInput input,HttpContext http,AppDbContext db)=>
        {
            if(UserId(http) is not {} userId)return Results.Unauthorized();
            var npc=await db.Npcs.SingleOrDefaultAsync(n=>n.Id==id);if(npc is null)return Results.NotFound();
            var role=await Role(db,npc.CampaignId,userId);if(role is null)return Results.NotFound();if(!Master(role))return Results.Problem("Somente mestres resolvem manobras das ameaças.",statusCode:403);
            if(input.ExpectedVersion!=npc.Version)return Results.Conflict(new{message="A ameaça mudou. Atualize antes de executar a manobra."});
            if(input.TargetId==Guid.Empty || input.TargetVersion<1 || input.TargetKind is not("npc" or "character") || input.TargetKind=="npc"&&input.TargetId==id)return SheetError("Selecione outro personagem ou ameaça da mesma campanha e informe sua versão.");
            var sheet=ParanormalThreatSchema.Read(npc.SheetJson);if(sheet is null)return SheetError("Este NPC não tem ficha de ameaça.");
            var schema=await db.Campaigns.Where(c=>c.Id==npc.CampaignId).Join(db.RpgSystems,c=>c.SystemId,s=>s.Id,(_,s)=>s.SchemaJson).SingleOrDefaultAsync();
            var definition=schema is null?null:SystemDefinition.Read(schema);
            if(!ParanormalThreatSchema.CampaignSupports(definition))return SheetError("A campanha precisa usar Ordem Paranormal 1.3 para manobras entre fichas.");
            try
            {
                int Draw(int sides)=>RandomNumberGenerator.GetInt32(1,sides+1);
                var attacker=ParanormalThreatManeuvers.Threat(sheet,npc.Health,npc.MaxHealth,Draw,input.UsePerceptionContext);
                Npc? targetNpc=null;Character? targetCharacter=null;ParanormalThreatSheet? targetSheet=null;ManeuverCombatant defender;
                string targetName;int targetVersion;
                bool sourceBlocked=false;
                if(input.Source=="incorporeal")RuleState.Require(sheet.Incorporeal,"A origem Outra ameaça incorpórea exige que o atacante seja incorpóreo.");
                if(input.TargetKind=="npc")
                {
                    targetNpc=await db.Npcs.SingleOrDefaultAsync(n=>n.Id==input.TargetId && n.CampaignId==npc.CampaignId);if(targetNpc is null)return Results.NotFound();
                    if(targetNpc.Version!=input.TargetVersion)return Results.Conflict(new{message="O alvo mudou. Atualize antes de resolver a manobra."});
                    targetSheet=ParanormalThreatSchema.Read(targetNpc.SheetJson);if(targetSheet is null)return SheetError("O alvo precisa de uma ficha de ameaça, com Luta em manobras configurada.");
                    defender=ParanormalThreatManeuvers.Threat(targetSheet,targetNpc.Health,targetNpc.MaxHealth,Draw);
                    sourceBlocked=!ParanormalThreatDefenses.CanAffect(targetSheet,input.Source)||input.Source=="fear-ritual"&&targetSheet.Kind=="paranormal";
                    targetName=targetNpc.Name;targetVersion=targetNpc.Version;
                }
                else
                {
                    targetCharacter=await db.Characters.SingleOrDefaultAsync(c=>c.Id==input.TargetId && c.CampaignId==npc.CampaignId);if(targetCharacter is null)return Results.NotFound();
                    if(targetCharacter.Version!=input.TargetVersion)return Results.Conflict(new{message="A ficha do alvo mudou. Atualize antes de resolver a manobra."});
                    definition=await CharacterEncounterConditions.Definition(db,npc.CampaignId,targetCharacter.Id);
                    RuleState.Require(definition is not null && ParanormalThreatSchema.CampaignSupports(definition),"O alvo precisa usar Ordem Paranormal 1.3.");
                    RuleState.Require(SheetRuleActions.Supports(definition!) && new[]{"forca","agilidade","per_luta"}.All(key=>definition!.Fields.Any(f=>f.Key==key&&f.Type=="number")),"O modelo do personagem precisa declarar Força, Agilidade, Luta e o estado de regras desta edição.");
                    RuleState.Require(input.TargetSizeConfirmed && input.TargetAttribute is "forca" or "agilidade","Confirme o tamanho e selecione Força ou Agilidade para a defesa de Luta do personagem.");
                    RuleState.Require(input.TargetAttribute!="agilidade"||input.AgileWeaponConfirmed,"Agilidade exige confirmação de uma arma ágil aplicável à oposição.");
                    var fields=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(targetCharacter.FieldsJson)??[];
                    var s=new RuleState(new CharacterSheetState(targetCharacter.Name,targetCharacter.PortraitUrl,targetCharacter.BackgroundUrl,targetCharacter.Health,targetCharacter.MaxHealth,targetCharacter.Sanity,targetCharacter.MaxSanity,targetCharacter.Mana,targetCharacter.MaxMana,fields,targetCharacter.Version),Draw);
                    defender=new(s,input.TargetAttribute,s.ParanormalSkill("per_luta"),input.TargetSize,input.TargetBonusIncludesSize);
                    targetName=targetCharacter.Name;targetVersion=targetCharacter.Version;
                }
                defender=defender with{Immune=defender.Immune||input.TargetImmune};
                var sourceView=input.UsePerceptionContext?sheet:sheet with{SensoryContext=new(),State=sheet.State.Where(x=>x.Key is not("camuflagem_alvo" or "ignora_camuflagem")).ToDictionary()};
                var chance=ParanormalThreatPerception.Resolve(sourceView,attacker.State).MissChance;
                var outcome=ParanormalThreatManeuvers.Resolve(attacker,defender,input,chance,sourceBlocked);
                if(targetNpc is not null)
                {
                    if(outcome.ProneApplied)
                    {
                        var next=targetSheet! with{State=new(defender.State.State)};var json=JsonSerializer.Serialize(next);
                        if(ParanormalThreatSchema.Validate(next) is {} invalidTarget)return SheetError(invalidTarget);
                        if(!JsonElement.DeepEquals(JsonSerializer.Deserialize<JsonElement>(targetNpc.SheetJson!),JsonSerializer.Deserialize<JsonElement>(json)))
                        {Capture(targetNpc,userId);targetNpc.SheetJson=json;targetNpc.Version++;targetNpc.UpdatedAt=DateTime.UtcNow;await SyncNpcParticipants(db,targetNpc);}
                        else db.Entry(targetNpc).Property(n=>n.Version).IsModified=true;
                    }
                    else db.Entry(targetNpc).Property(n=>n.Version).IsModified=true;
                    targetVersion=targetNpc.Version;
                }
                else
                {
                    if(outcome.ProneApplied)
                    {
                        var exported=defender.State.Export();var next=JsonSerializer.Serialize(exported.Fields);
                        RuleState.Require(exported.Fields!.Count<=100&&next.Length<=20000,"A condição excederia o limite de campos do personagem.");
                        var previous=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(targetCharacter!.FieldsJson)??[];
                        if(SheetRuleActions.ValidateStateAccess(definition!,previous,exported.Fields!,master:true,ruleExecution:true) is {} invalidFields)return SheetError(invalidFields);
                        if(!JsonElement.DeepEquals(JsonSerializer.Deserialize<JsonElement>(targetCharacter.FieldsJson),JsonSerializer.Deserialize<JsonElement>(next)))
                        {CharacterHistory.Capture(db,targetCharacter,userId);targetCharacter.FieldsJson=next;targetCharacter.UpdatedAt=DateTime.UtcNow;await CharacterEncounterConditions.Sync(db,targetCharacter,definition!);}
                        else db.Entry(targetCharacter).Property(c=>c.Version).IsModified=true;
                    }
                    else db.Entry(targetCharacter!).Property(c=>c.Version).IsModified=true;
                    targetVersion=targetCharacter!.Version;
                }
                var entry=new ThreatManeuverRecord(Guid.NewGuid(),input.TargetId,input.TargetKind,targetName,input.Kind,outcome.Outcome,outcome.AttackerTotal,outcome.DefenderTotal,outcome.Ties,outcome.Displacement,outcome.ProneApplied,DateTime.UtcNow);
                var final=sheet with{ManeuverLog=[entry,..sheet.ManeuverLog.Take(9)]};
                if(ParanormalThreatSchema.Validate(final) is {} invalidSheet)return SheetError(invalidSheet);
                Capture(npc,userId);npc.SheetJson=JsonSerializer.Serialize(final);npc.Version++;npc.UpdatedAt=DateTime.UtcNow;
                var rolls=attacker.State.Rolls.Concat(defender.State.Rolls).ToArray();SaveRolls(db,npc.CampaignId,userId,input.Visibility,rolls);
                await db.SaveChangesAsync();
                return Results.Ok(new{npc=View(npc,true),targetId=input.TargetId,targetKind=input.TargetKind,targetName,targetVersion,outcome,rolls,attackerRolls=attacker.State.Rolls,defenderRolls=defender.State.Rolls});
            }
            catch(ArgumentException error){return SheetError(error.Message);}
            catch(DbUpdateConcurrencyException){return Results.Conflict(new{message="A ameaça ou o alvo mudou durante a resolução. Nenhum efeito ou dado foi salvo; atualize as fichas."});}
            catch(DbUpdateException error) when(error.InnerException is PostgresException{SqlState:PostgresErrorCodes.UniqueViolation}){return Results.Conflict(new{message="O histórico do alvo mudou durante a resolução. Nenhuma alteração foi salva; atualize as fichas."});}
        }).RequireRateLimiting("rolls");
    }
}
