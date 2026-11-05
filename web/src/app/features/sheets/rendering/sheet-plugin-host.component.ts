import { Component, ComponentRef, EventEmitter, Input, OnChanges, OnDestroy, Output, Type, ViewChild, ViewContainerRef } from '@angular/core';
import { Subscription } from 'rxjs';
import type { Character, CharacterInput, RpgSystem } from '../../../api';
import { ParanormalSheetComponent } from '../renderers/paranormal/paranormal-sheet.component';
import { DndSheetComponent } from '../renderers/dnd/dnd-sheet.component';
import { CocSheetComponent } from '../renderers/cthulhu/coc-sheet.component';
import { T20SheetComponent } from '../renderers/tormenta20/t20-sheet.component';
import { resolveSheetRenderer } from './sheet-renderer-registry';

interface SheetPresentation {
  changed: EventEmitter<void>;
  restored: EventEmitter<Character>;
  working: EventEmitter<boolean>;
}

// Trusted presentation code, bundled at build time. Adding a renderer does not change the editor page.
const PRESENTATIONS: Readonly<Record<string, Type<SheetPresentation>>> = {
  ordem_paranormal: ParanormalSheetComponent,
  dnd_5e: DndSheetComponent,
  call_of_cthulhu_7e: CocSheetComponent,
  tormenta20: T20SheetComponent
};

@Component({ selector: 'app-sheet-plugin-host', standalone: true, template: '<ng-container #outlet />' })
export class SheetPluginHostComponent implements OnChanges, OnDestroy {
  @ViewChild('outlet', { read: ViewContainerRef, static: true }) private outlet!: ViewContainerRef;
  @Input({ required: true }) system!: RpgSystem;
  @Input({ required: true }) character!: Character;
  @Input({ required: true }) draft!: CharacterInput;
  @Input() mode: 'read' | 'edit' | 'print' = 'read';
  @Input() isMaster = false;
  @Input() rulesReady = false;
  @Output() changed = new EventEmitter<void>();
  @Output() restored = new EventEmitter<Character>();
  @Output() working = new EventEmitter<boolean>();
  private ref?: ComponentRef<SheetPresentation>;
  private subscriptions = new Subscription();

  ngOnChanges() {
    const renderer = resolveSheetRenderer(this.system);
    const type = renderer ? PRESENTATIONS[renderer.id] : undefined;
    if (!type) { this.clear(); return; }
    if (this.ref?.componentType !== type) {
      this.clear();
      this.ref = this.outlet.createComponent(type);
      this.subscriptions.add(this.ref.instance.changed.subscribe(() => this.changed.emit()));
      this.subscriptions.add(this.ref.instance.restored.subscribe(item => this.restored.emit(item)));
      this.subscriptions.add(this.ref.instance.working.subscribe(busy => this.working.emit(busy)));
    }
    for (const [key, value] of Object.entries({ system: this.system, character: this.character, draft: this.draft,
      mode: this.mode, isMaster: this.isMaster, rulesReady: this.rulesReady })) this.ref!.setInput(key, value);
  }

  private clear() { this.subscriptions.unsubscribe(); this.subscriptions = new Subscription(); this.outlet.clear(); this.ref = undefined; }
  ngOnDestroy() { this.subscriptions.unsubscribe(); }
}
