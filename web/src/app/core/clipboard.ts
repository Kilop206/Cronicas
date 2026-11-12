export async function copyLink(document: Document, text: string): Promise<string> {
  try {
    const clipboard = document.defaultView?.navigator.clipboard;
    if (!clipboard || !text) throw new Error('Clipboard unavailable');
    await clipboard.writeText(text);
    return 'Link copiado.';
  } catch {
    return 'Não foi possível copiar. Selecione o link e copie manualmente.';
  }
}
