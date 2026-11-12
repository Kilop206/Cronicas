export function confirmDiscard(pending: boolean): boolean {
  return !pending || globalThis.confirm('Há alterações não salvas ou uma operação em andamento. Sair desta página?');
}

export function preventUnsavedUnload(event: BeforeUnloadEvent, pending: boolean): void {
  if (pending) { event.preventDefault(); event.returnValue = ''; }
}
