# Hotfix v0.5.1 — proteção contra downgrade e recuperação de emergência

**Status:** implementado, validado e publicado na `v0.5.1`.

**Escopo:** complemento de segurança da Fase 6. Não inicia a Fase 7 nem altera o instalador.

---

## 1. Motivo

Foi confirmado um cenário destrutivo ao executar uma versão antiga do DesktopFences sobre dados criados pelo schema v2. O binário antigo tentou migrar um layout rebaixado para v1 e, para um item chamado `.env`, a busca pelo nome sem extensão produziu um nome vazio. A combinação acabou aceitando a própria pasta `Desktop` como se fosse o payload do item e moveu essa pasta inteira para o store legado.

Os arquivos permaneceram fisicamente no store, mas desapareceram da área de trabalho. A restauração manual conseguiu devolver o conteúdo, porém o Windows reorganizou os ícones porque não existia um snapshot independente e completo das posições anteriores.

Este hotfix trata as duas necessidades separadamente:

1. impedir que o binário atual aceite novamente o estado produzido por esse downgrade;
2. manter uma rota independente e simples de recuperação, mesmo se o aplicativo principal não conseguir iniciar.

---

## 2. Garantias implementadas

### 2.1 Proteção contra downgrade/caminho incorreto

- Quando `layout.json` e `layout.json.bak` são válidos, um backup com schema mais novo tem precedência sobre um principal rebaixado por uma versão antiga.
- A migração v1 rejeita um payload cujo nome físico não corresponda ao nome original persistido.
- Um item cujo stem seja vazio, como `.env`, nunca pode resolver para a raiz do Desktop por fallback de diretório.
- Diante dessa incompatibilidade, o arranque para e oferece abrir a recuperação independente; nenhum payload é movido pela migração rejeitada.

Essas proteções existem no binário atual. Elas não conseguem modificar releases antigas já distribuídas, por isso o executável de recuperação permanece necessário.

### 2.2 Snapshot independente das posições

Antes de assumir nova custódia no arranque, o aplicativo captura uma vez os ícones visíveis do Explorer e combina essa informação com as posições originais dos itens já registrados no layout. O manifesto resultante é gravado atomicamente em:

```text
%LocalAppData%\DesktopFences\Recovery\desktop-snapshot.json
%LocalAppData%\DesktopFences\Recovery\desktop-snapshot.json.bak
```

Itens temporariamente ausentes não são removidos do snapshot só por não aparecerem em uma captura. Pausar, Sair e a liberação de arranque reposicionam os itens restaurados usando esse manifesto.

### 2.3 `DesktopFences.Recovery.exe`

A release passa a conter um segundo executável, independente da janela principal. Com o DesktopFences fechado, o usuário abre `DesktopFences.Recovery.exe`, confirma a operação e recebe um resultado legível.

O processo:

1. carrega o melhor layout válido e o snapshot de posições;
2. cria uma sessão em `%LocalAppData%\DesktopFences\Recovery\Emergency-AAAA...`;
3. arquiva cópias do layout, backup, snapshot e journals para diagnóstico;
4. copia para o Desktop todos os payloads conhecidos e órfãos encontrados no store;
5. mescla com segurança um snapshot acidental da pasta Desktop;
6. preserva arquivos já existentes: conteúdo idêntico é ignorado e conteúdo diferente ganha um nome de recuperação;
7. mantém o store original intacto;
8. somente após uma cópia sem erros, limpa as referências ativas dos itens no layout v2 e arquiva os journals, impedindo que o próximo arranque esconda novamente as cópias recuperadas;
9. preserva por padrão as posições atuais do Desktop e atualiza o Explorer; reaplicar posições antigas exige seleção explícita do usuário.

Se qualquer cópia falhar, a ferramenta não faz o reset final. O store, layout e journals continuam disponíveis para nova tentativa ou recuperação manual.

### 2.4 Handoff automático

Se o aplicativo principal detectar uma falha de segurança no arranque, ele informa o usuário e oferece abrir a ferramenta de recuperação. O processo principal encerra e libera o mutex antes de iniciar o executável auxiliar.

### 2.5 Ejeção previsível e Desktop Público

- Ao arrastar um item para fora da fence, o primeiro ícone volta no ponto em que o cursor foi solto. Uma seleção múltipla recebe posições próximas e distintas, evitando colisões que o Explorer reorganizaria por conta própria.
- Como `SHChangeNotify` é assíncrono, o posicionamento é repetido por uma janela curta após a ejeção, até o Explorer materializar os ícones.
- Pausar e Sair usam a mesma proteção temporal: o app permanece vivo por uma janela limitada, localiza cada item pelo destino físico realmente restaurado e reaplica `OriginalX`/`OriginalY`; o snapshot supre layouts antigos sem coordenadas.
- Se outra coisa já ocupar a posição original, o app não desloca esse item. A coordenada original é entregue ao Explorer, que aplica sua própria política de alinhamento/colisão.
- Atalhos, arquivos ou pastas originalmente vindos do Desktop Público são restaurados no Desktop do usuário. O app continua `asInvoker`, não tenta escrever em uma pasta pública protegida e um único item sem permissão não reverte o restante do lote.
- Um caminho externo às pastas de Desktop nunca é usado como destino de restore.

---

## 3. Política de não destruição

A recuperação de emergência segue regras mais conservadoras que o fluxo normal:

- copia; não move nem exclui o payload de origem;
- nunca sobrescreve arquivo ou pasta do usuário;
- compara arquivos existentes antes de decidir conflito;
- preserva payloads órfãos;
- não esvazia o layout nem arquiva transações antes de todas as cópias concluírem;
- mantém recibo textual da sessão;
- pode ser repetida sem transformar uma falha parcial em perda de dados.

O objetivo é priorizar custódia e reversibilidade. O custo temporário é poder existir uma cópia adicional no Desktop e no store até o usuário confirmar que está tudo correto.

---

## 4. Impacto de performance

O caminho normal não recebe polling, watchdog de arquivos nem gravação por operação:

- uma captura do `SysListView32` no arranque;
- uma gravação atômica pequena do snapshot no arranque;
- uma captura em lote ao reposicionar itens durante Pausar/Sair ou quando o usuário solicitar posições antigas no recovery, já fora do uso contínuo;
- tentativas curtas de captura/posicionamento após ejeção, Pausar ou Sair, sem timer permanente;
- zero trabalho adicional por frame e zero I/O adicional ao transferir itens entre fences.

O executável de recuperação pode levar tempo proporcional ao volume do store porque copia e compara arquivos. Esse custo existe somente quando o usuário aciona a recuperação e é deliberado para manter a fonte intacta.

---

## 5. Limites honestos

- A garantia sobre posições começa depois do primeiro arranque da v0.5.1, quando o snapshot é criado. Não é possível reconstruir integralmente posições históricas que já foram perdidas antes desse snapshot.
- Uma release antiga ainda pode não conhecer essas proteções. O usuário deve conservar o `DesktopFences.Recovery.exe` junto do aplicativo e preferir sempre a versão mais recente.
- Falhas físicas do disco, corrupção externa simultânea do Desktop e do store ou exclusão manual de ambos não podem ser recuperadas somente pelo aplicativo.
- A ferramenta recupera custódia e posições conhecidas; ela não substitui backup do sistema.

---

## 6. Validação

### Automatizada

- [x] backup v2 vence principal v1 rebaixado;
- [x] `.env` não resolve para a pasta Desktop;
- [x] migração v1 rejeita payload com nome incompatível sem alterar a fonte;
- [x] snapshot combina Desktop, layout e versão anterior e possui fallback atômico;
- [x] recuperação copia payload estável e árvore Desktop órfã sem apagar a fonte;
- [x] conflito nunca sobrescreve destino;
- [x] layout só é desativado depois da cópia completa;
- [x] journals são arquivados apenas no sucesso.
- [x] snapshot do Desktop é persistido antes de recovery, migração ou retomada de custódia;
- [x] recuperação preserva posições atuais por padrão; posições antigas são opção explícita.
- [x] ejeção individual usa o ponto do cursor e seleção múltipla recebe posições próximas distintas;
- [x] origem no Desktop Público é redirecionada ao Desktop do usuário;
- [x] caminho externo ao Desktop não é aceito como destino de restore.
- [x] Pausar/Sair localizam pelo destino restaurado, repetem o posicionamento com limite e usam snapshot quando a coordenada não existir no layout.

### Gate manual encerrado pelo desenvolvedor

- [x] Executar a ferramenta com uma cópia controlada do incidente e confirmar todos os arquivos.
- [x] Confirmar restauração de posições após entrada, fechamento forçado, recuperação e novo arranque.
- [x] Confirmar Pausar e Sair com itens em mais de uma fence.
- [x] Confirmar que Pausar/Sair devolvem os ícones às posições originais e que uma posição ocupada é resolvida pelo Explorer.
- [x] Ejetar um e vários itens e confirmar posicionamento no cursor e ao redor dele.
- [x] Adicionar um atalho do Desktop Público e confirmar saída no Desktop do usuário sem elevação.
- [x] Confirmar que falha de cópia não limpa o layout nem o store.
- [x] Confirmar que os dois executáveis seguem juntos no zip `win-x64` e `win-arm64`.

Gate encerrado pelo desenvolvedor; a `v0.5.1` foi publicada antes da Fase 7.
