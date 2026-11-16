# Links de convite da campanha — 16/11/2026

O mesmo convite válido pode ser compartilhado com vários participantes. Cada pessoa entra com sua conta e recebe o papel escolhido pelo proprietário ao gerar o link. A validade continua sendo de sete dias.

Aceitar novamente um convite disponível abre a campanha para quem já participa. Isso não altera o papel atual, não duplica a participação e não emite outro evento de entrada. Os bloqueios transacionais continuam serializando adesões, inclusive com tokens distintos para a mesma conta.

Convites expirados, emitidos por quem perdeu a autoridade e links legados já consumidos continuam indisponíveis. Gere um novo link para substituir um convite consumido antes desta atualização. Não há migration, reativação de tokens antigos nem alteração de participações existentes.

Os testes PostgreSQL cobrem contas distintas no mesmo link, repetição concorrente com tokens iguais/diferentes, uma participação/evento por conta, papéis preservados, expiração, links legados, rollback e emissor revogado. Os smokes HTTP verificam o mesmo link entre duas contas e retornos idempotentes. A auditoria de navegador aceita e reabre o convite duas vezes, terminando na tela de convite para preservar a cobertura visual.

Resultados finais e publicação serão registrados após a validação.
