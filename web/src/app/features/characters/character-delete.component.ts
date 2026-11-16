import { CommonModule } from '@angular/common';
import { Component, ElementRef, EventEmitter, Input, Output, ViewChild, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { ApiService, Character, apiError } from '../../api';

@Component({selector:'app-character-delete',standalone:true,imports:[CommonModule,FormsModule],template:`
  <button *ngIf="character.canEdit" #trigger type="button" class="text-link danger" [disabled]="!available||busy()" (click)="open()">Excluir ficha</button>
  <dialog #confirmation class="character-delete-dialog" aria-labelledby="character-delete-title" (cancel)="cancel($event)">
    <form (ngSubmit)="remove()">
      <div #content class="character-delete-body" role="region" aria-label="Detalhes da exclusão" tabindex="0">
      <h2 id="character-delete-title">Excluir {{reviewed?.name}}?</h2>
      <p>A exclusão é permanente e remove esta ficha, histórico, inventário, habilidades, notas e links de OBS. Fichas vinculadas a encontros precisam ser removidas deles antes.</p>
      <label>Digite o nome da ficha para confirmar<input name="deleteName" [(ngModel)]="typedName" [disabled]="busy()" autocomplete="off" maxlength="120" [placeholder]="reviewed?.name||''"></label>
      <p *ngIf="error()" class="form-error" role="alert">{{error()}}</p>
      </div>
      <div class="form-actions"><button type="button" class="button secondary" autofocus [disabled]="busy()" (click)="cancel()">Cancelar</button><button type="submit" class="button danger-button" [disabled]="busy()||!available||typedName!==reviewed?.name">{{busy()?'Excluindo...':'Excluir permanentemente'}}</button></div>
    </form>
  </dialog>`})
export class CharacterDeleteComponent {
  @Input({required:true}) character!:Character;
  @Input() available=false;
  @Output() working=new EventEmitter<boolean>();
  @Output() deleted=new EventEmitter<{campaignId:string}>();
  @ViewChild('confirmation') confirmation?:ElementRef<HTMLDialogElement>;
  @ViewChild('content') content?:ElementRef<HTMLElement>;
  @ViewChild('trigger') trigger?:ElementRef<HTMLButtonElement>;
  private api=inject(ApiService);
  busy=signal(false);error=signal('');typedName='';reviewed:Pick<Character,'id'|'name'|'version'|'campaignId'>|null=null;
  open(){if(!this.available||this.busy()||!this.character.canEdit)return;const {id,name,version,campaignId}=this.character;this.reviewed={id,name,version,campaignId};this.typedName='';this.error.set('');this.confirmation?.nativeElement.showModal();if(this.content)this.content.nativeElement.scrollTop=0;}
  cancel(event?:Event){event?.preventDefault();if(this.busy())return;this.confirmation?.nativeElement.close();this.reviewed=null;this.typedName='';this.trigger?.nativeElement.focus();}
  remove(){
    const reviewed=this.reviewed;if(!reviewed||!this.available||this.busy()||!this.character.canEdit||this.character.id!==reviewed.id||this.typedName!==reviewed.name)return;
    this.busy.set(true);this.working.emit(true);this.error.set('');
    this.api.deleteCharacter(reviewed.id,reviewed.version).pipe(finalize(()=>{this.busy.set(false);this.working.emit(false);})).subscribe({
      next:()=>{this.confirmation?.nativeElement.close();this.reviewed=null;this.deleted.emit({campaignId:reviewed.campaignId});},
      error:e=>this.error.set(apiError(e))
    });
  }
}
