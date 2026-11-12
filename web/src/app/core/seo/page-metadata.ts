export const PUBLIC_ORIGIN = 'https://cronicas-bsy.pages.dev';
export const HOME_DESCRIPTION = 'Organize campanhas de RPG de mesa, crie fichas e mundos personalizados, registre sessões e jogue com seus amigos em um só lugar.';

const pages: Record<string, string> = {
  '/login': 'Entrar', '/register': 'Criar conta', '/forgot-password': 'Recuperar acesso',
  '/reset-password': 'Redefinir senha', '/dashboard': 'Visão geral', '/search': 'Pesquisa',
  '/campaigns': 'Campanhas', '/campaigns/new': 'Nova campanha', '/worlds': 'Universos',
  '/systems': 'Sistemas', '/systems/new': 'Novo sistema', '/profile': 'Meu perfil', '/settings': 'Preferências',
  '/privacy': 'Política de Privacidade', '/terms': 'Termos de Uso', '/cookies': 'Cookies e privacidade',
  '/error': 'Não foi possível abrir a página'
};
const sections: Record<string, string> = {
  characters: 'Personagens', npcs: 'NPCs', sessions: 'Sessões', documents: 'Documentos',
  wiki: 'Mundo da campanha', encounters: 'Encontros', room: 'Transmissão'
};

// Only product labels belong in metadata: never include names, search terms or tokens.
export function pageMetadata(path: string) {
  if (path === '/') return { title: 'Crônicas — Campanhas, fichas e mundos para RPG de mesa', description: HOME_DESCRIPTION, known: true, public: true };
  let label = pages[path];
  const campaign = /^\/campaigns\/[^/]+(?:\/([^/]+))?$/.exec(path);
  if (campaign && !label) label = campaign[1] ? sections[campaign[1]] : 'Campanha';
  if (/^\/characters\/[^/]+$/.test(path)) label = 'Ficha de personagem';
  if (/^\/worlds\/[^/]+$/.test(path)) label = 'Universo';
  if (!label && /^\/systems\/[^/]+$/.test(path)) label = 'Editar sistema';
  if (/^\/invites\/[^/]+$/.test(path)) label = 'Convite de campanha';
  if (/^\/obs\/[^/]+$/.test(path)) label = 'Portrait para OBS';
  if (/^\/obs-camera\/[^/]+\/[^/]+$/.test(path)) label = 'Câmera para OBS';
  return { title: `${label || 'Página não encontrada'} · Crônicas`, description: label ? `${label} no Crônicas, seu espaço para RPG de mesa.` : 'O endereço solicitado não foi encontrado no Crônicas.', known: !!label, public: false };
}
