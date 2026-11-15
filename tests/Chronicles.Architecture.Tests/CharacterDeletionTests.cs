using Chronicles.Application.CharacterSheets;
using Chronicles.Domain;

public static class CharacterDeletionTests
{
    public static async Task Run()
    {
        static void Check(bool value,string label){if(!value)throw new Exception("Character deletion: "+label);}
        var actor=Guid.NewGuid();var id=Guid.NewGuid();
        var sheet=new CharacterSheetState("Test",null,null,10,10,0,0,0,0,[],5);
        var snapshot=new CharacterSheetSnapshot(id,Guid.NewGuid(),actor,"player",sheet,null,DateTime.UtcNow,null);
        var store=new MemoryStore(snapshot,actor);var command=new DeleteCharacter(store);
        Check((await command.Execute(Guid.NewGuid(),5,actor)).Failure==CharacterWriteFailure.NotFound,"unknown character hidden");
        Check((await command.Execute(id,5,Guid.NewGuid())).Failure==CharacterWriteFailure.NotFound,"nonmember hidden");
        store.Current=snapshot with{Role="spectator"};
        Check((await command.Execute(id,5,actor)).Failure==CharacterWriteFailure.NotFound,"spectator cannot delete even owned character");
        store.Current=snapshot with{OwnerId=Guid.NewGuid()};
        Check((await command.Execute(id,5,actor)).Failure==CharacterWriteFailure.Forbidden,"player cannot delete another owner's character");
        store.Current=snapshot;
        Check((await command.Execute(id,null,actor)).Failure==CharacterWriteFailure.Invalid,"version required");
        Check((await command.Execute(id,0,actor)).Failure==CharacterWriteFailure.Invalid,"positive version required");
        Check((await command.Execute(id,4,actor)).Failure==CharacterWriteFailure.Conflict&&store.Deletes==0,"stale confirmation cannot reach deletion port");
        Check((await command.Execute(id,5,actor)).Failure is null&&store.Deletes==1,"owner can delete reviewed revision");
        foreach(var role in new[]{"owner","gm","co-gm"}){store.Current=snapshot with{OwnerId=Guid.NewGuid(),Role=role};Check((await command.Execute(id,5,actor)).Failure is null,"authorized master role "+role);}
        store.Failure=CharacterWriteFailure.Conflict;Check((await command.Execute(id,5,actor)).Failure==CharacterWriteFailure.Conflict,"commit-time conflict propagated");
        store.Failure=CharacterWriteFailure.Forbidden;Check((await command.Execute(id,5,actor)).Failure==CharacterWriteFailure.Forbidden,"commit-time permission loss propagated");
        Console.WriteLine("Character deletion: membership, ownership/master roles, reviewed versions and commit-time conflicts passed.");
    }
    private sealed class MemoryStore(CharacterSheetSnapshot initial,Guid actor):ICharacterDeletionStore
    {
        public CharacterSheetSnapshot Current=initial;public int Deletes;public CharacterWriteFailure? Failure;
        public Task<CharacterSheetSnapshot?> Load(Guid id,Guid user,CancellationToken ct)=>Task.FromResult<CharacterSheetSnapshot?>(id==Current.Id&&user==actor?Current:null);
        public Task<CharacterWriteResponse> Delete(CharacterSheetSnapshot before,Guid user,CancellationToken ct){Deletes++;return Task.FromResult(new CharacterWriteResponse(Failure:Failure));}
    }
}
