import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

@Component({ standalone: true, imports: [CommonModule, RouterLink], template: `
  <main class="page narrow legal-page">
    <a routerLink="/" class="brand"><span class="brand-mark" aria-hidden="true">C</span><span>crônicas</span></a>
    <div class="page-heading"><div><span class="overline">TRANSPARÊNCIA</span><h1>{{ title }}</h1><p>Atualizado em 12/11/2026</p></div></div>
    <p class="notice" role="note"><strong>Documento em validação.</strong> Esta página descreve o funcionamento implementado. A identificação do responsável pelo Crônicas, o canal de atendimento e as condições jurídicas indicadas abaixo ainda precisam ser definidos. Este texto não representa uma certificação de conformidade.</p>
    <ng-container *ngIf="kind === 'privacy'">
      <h2>Dados que o Crônicas utiliza</h2>
      <p>O cadastro utiliza email, nome de exibição e senha. A senha é armazenada como hash, não como texto legível. O perfil pode conter biografia, avatar por URL e cor de destaque. A aplicação guarda campanhas, vínculos de participantes, fichas, versões, inventário, notas, universos, textos, sessões de jogo, mensagens e rolagens conforme o uso.</p>
      <p>Esses dados permitem autenticar sua conta, organizar sua mesa, aplicar permissões, compartilhar conteúdo com os participantes autorizados e recuperar acesso por email quando o serviço de envio estiver configurado.</p>
      <h2>Visibilidade e compartilhamento</h2>
      <p>O acesso varia conforme a campanha, o papel do participante e a visibilidade escolhida. Mestres podem acessar informações que jogadores e espectadores não podem. Confira a visibilidade antes de publicar textos e evite inserir dados pessoais de terceiros sem autorização.</p>
      <p>Links de portrait para OBS dão acesso à projeção visual da ficha a qualquer pessoa que os possua. Gerar outro link revoga o anterior. Não publique esses links. Na sala, câmera, microfone e tela só são ativados por ação do usuário e permissão do navegador. A mídia usa WebRTC entre participantes; informações de conexão, incluindo endereços de rede, podem ser compartilhadas com eles e com o servidor STUN da Cloudflare. A aplicação não implementa gravação da mídia; participantes podem gravar ou transmitir por ferramentas externas.</p>
      <h2>Armazenamento e serviços externos</h2>
      <p>A configuração de produção utiliza Cloudflare para o site e encaminhamento de requisições, Render para a API e Neon/PostgreSQL para persistência. Recuperação de senha usa o provedor SMTP configurado pelo operador. A definição de regiões, contratos, transferências internacionais e períodos de retenção/backups está pendente de validação pelo responsável.</p>
      <p>Fontes são obtidas do Google Fonts, que recebe a requisição do navegador. Imagens por URL HTTPS são carregadas do endereço informado e seus provedores também recebem requisições. Dados técnicos de conexão e logs operacionais podem ser tratados pela infraestrutura. O log de acesso da aplicação usa modelos de rota sem query strings ou tokens; falhas técnicas também são registradas para diagnóstico.</p>
      <h2>Acesso, correção e exclusão</h2>
      <p>Em <a routerLink="/profile">Meu perfil</a>, você pode corrigir nome, biografia e avatar e baixar uma cópia dos dados exportáveis da conta. A exportação inclui conteúdos pessoais e de jogo; guarde o arquivo em local seguro. Em <a routerLink="/settings">Preferências</a>, você pode encerrar outros acessos ou sair da conta.</p>
      <p><strong>Canal de solicitação pendente:</strong> o repositório não informa um contato de privacidade. O responsável precisa publicar um canal para pedidos de acesso complementar, correção e exclusão, definir verificação de identidade, atendimento e tratamento de conteúdo compartilhado e backups. Ainda não existe exclusão automática de conta. Não envie senhas ou códigos de recuperação em pedidos de atendimento.</p>
      <h2>Informações que exigem validação</h2>
      <p>Identificação e contato do controlador, bases legais por finalidade, prazos de retenção e descarte, exercício de direitos, tratamento de dados de crianças/adolescentes e medidas operacionais de resposta a incidentes precisam ser validados antes da abertura ao público.</p>
      <p>Consulte também <a routerLink="/cookies">Cookies e privacidade</a> e os <a routerLink="/terms">Termos de Uso</a>.</p>
    </ng-container>
    <ng-container *ngIf="kind === 'terms'">
      <h2>Proposta do serviço</h2>
      <p>O Crônicas é um espaço para organizar campanhas de RPG de mesa, fichas, universos, registros e sessões com outros participantes. A sala oferece comunicação entre navegadores e fontes para OBS. Não há tabletop/VTT, cobrança ou garantia de disponibilidade implementados neste projeto.</p>
      <h2>Conta e uso responsável</h2>
      <p>Proteja sua senha, códigos e links privados. Utilize as permissões e opções de visibilidade para compartilhar apenas o conteúdo desejado. Respeite os demais participantes e os direitos sobre textos, imagens e materiais que inserir. Não utilize a aplicação para acessar contas ou conteúdo sem autorização.</p>
      <h2>Conteúdo e continuidade</h2>
      <p>As permissões dependem dos papéis na campanha e podem ser alteradas pelos responsáveis pela mesa. Mantenha uma cópia dos materiais importantes usando a exportação disponível no perfil. Falhas de rede, limitações da hospedagem e restrições de WebRTC podem interromper o uso.</p>
      <h2>Condições pendentes</h2>
      <p>A identificação do prestador e seu contato, regras de moderação/suspensão, licença de conteúdo, idade aplicável, encerramento de conta, condições de disponibilidade e demais condições contratuais ainda dependem de validação jurídica e de produto. Nenhuma informação empresarial foi presumida nesta página.</p>
      <p>Leia a <a routerLink="/privacy">Política de Privacidade</a> para conhecer os dados utilizados e os controles existentes.</p>
    </ng-container>
    <ng-container *ngIf="kind === 'cookies'">
      <h2>Cookie de autenticação</h2>
      <p><code>chronicles_session</code> é um cookie próprio, estritamente necessário para manter o acesso à conta. Expira em até 14 dias e é removido ao sair. O servidor verifica se a sessão continua válida; encerrar uma sessão invalida o acesso mesmo antes do prazo do cookie. Em produção utiliza HttpOnly, Secure e SameSite=Lax.</p>
      <h2>Categorias identificadas</h2>
      <dl><dt>Essenciais</dt><dd>Sessão autenticada e proteção de acesso.</dd><dt>Preferências</dt><dd>A cor de destaque é salva no perfil no servidor, sem cookie opcional.</dd><dt>Análise, publicidade e rastreamento</dt><dd>Não há ferramentas dessas categorias integradas ao código auditado. Serviços de hospedagem podem utilizar mecanismos técnicos próprios, cuja configuração precisa ser conferida pelo operador.</dd></dl>
      <h2>Seus controles</h2>
      <p>Você pode <a routerLink="/settings">encerrar sessões e sair da conta</a>. Também pode apagar ou bloquear cookies nas configurações do navegador; bloquear o cookie de sessão impede o uso de áreas autenticadas. Não há preferências de cookies opcionais para aceitar ou revogar nesta versão, por isso não exibimos um banner de consentimento.</p>
      <p>As permissões de câmera, microfone e compartilhamento de tela podem ser encerradas na sala e nas configurações do navegador. Consulte a <a routerLink="/privacy">Política de Privacidade</a> sobre fontes externas e mídia.</p>
    </ng-container>
  </main>` })
export class LegalPage {
  kind = inject(ActivatedRoute).snapshot.data['document'] as 'privacy' | 'terms' | 'cookies';
  title = this.kind === 'privacy' ? 'Política de Privacidade' : this.kind === 'terms' ? 'Termos de Uso' : 'Cookies e privacidade';
}
