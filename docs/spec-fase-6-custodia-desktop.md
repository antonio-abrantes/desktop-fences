# Especificação complementar — Fase 6: custódia transacional de itens do Desktop

**Status:** implementada, validada no Windows 11 e fechada na versão preparada `v0.5.0`.

**Posição no ciclo:** depois do gate da Fase 5 e antes do instalador, que passa a ser a Fase 7.

**Documento operacional:** [plano-fase-6-custodia-desktop.md](plano-fase-6-custodia-desktop.md).

---

## 1. Objetivo

Fortalecer o fluxo central do DesktopFences — levar arquivos, atalhos e pastas do Desktop para uma fence, transferi-los entre fences e devolvê-los ao Desktop — preservando a abordagem aprovada de mover o item real para um store controlado pelo aplicativo.

A fase reúne quatro mudanças que devem ser entregues juntas:

1. store estável por `ItemId`;
2. transação recuperável, JSON atômico, backup e recovery no arranque;
3. transferência entre fences somente por metadados;
4. processamento em lote por gesto do usuário.

O resultado esperado é reduzir movimentações físicas, eliminar janelas conhecidas de inconsistência e manter ou melhorar a performance atual no caso normal.

---

## 2. Escopo fechado

### Incluído

- arquivos, atalhos e pastas provenientes do Desktop do usuário ou do Desktop público;
- entrada de um ou vários itens em uma fence;
- reordenação e transferência de um ou vários itens entre fences;
- saída para o Desktop por ejetar, remover fence, Pausar ou Sair;
- migração recuperável do store atual, organizado por `FenceId`, para o store por `ItemId`;
- persistência e recuperação do estado físico e do `layout.json`;
- ícones de namespace já suportados — Lixeira, Este Computador e Rede — apenas para preservar o comportamento atual. Eles recebem identidade estável, mas continuam sem payload de arquivo no store.

### Fora desta fase — permanecem rastreados para melhorias futuras

- qualquer política ou novo comportamento para itens originados fora do Desktop;
- OneDrive, placeholders, sync roots e Desktop redirecionado;
- progresso, cancelamento ou worker para operações longas;
- ampliação geral do contrato de `IFileOperation` além do resultado mínimo necessário para decidir o commit de uma operação do Desktop;
- mudanças no protocolo OLE, cache de ícones, telemetria, instalador ou desinstalador.

Esta fase não deve criar referências, atalhos ou regras especiais para origens externas. O suporte atual fora do Desktop não é expandido nem usado como critério de aceite da Fase 6.

---

## 3. Invariantes obrigatórios

As regras abaixo têm prioridade sobre detalhes de implementação:

1. Todo payload retirado do Desktop possui um `ItemId` persistente e uma referência recuperável no layout ou em uma transação durável.
2. O diretório físico de um payload não depende da fence proprietária.
3. Transferir um item entre fences não move, copia, renomeia nem reabre o payload.
4. Uma operação física só aparece como concluída na UI e no layout depois de ter resultado confirmado.
5. Um único gesto com vários itens produz no máximo uma captura do Desktop, um commit do layout e uma notificação final da Shell por diretório afetado.
6. `layout.json` nunca é sobrescrito diretamente com conteúdo parcial.
7. Falha ou encerramento entre etapas deixa informação suficiente para recovery determinístico no próximo arranque.
8. Recovery nunca apaga payload desconhecido ou órfão e nunca sobrescreve um arquivo do usuário para resolver conflito.
9. Pausar, Sair e remover fence continuam tendo caminho de restauração para todos os itens sob custódia.
10. Não existe release parcial da Fase 6: schema, migração, transação, transferências por metadados e lote entram no mesmo gate final.
11. O número de gravações duráveis do journal depende das etapas da transação, não da quantidade de itens do lote.

---

## 4. Identidade e store estável

### 4.1 Identidade

Cada item passa a ter `itemId`, um GUID criado uma vez e preservado enquanto o item estiver sob custódia. Nome, posição, ordem e fence proprietária podem mudar sem alterar essa identidade.

Ícones de namespace também recebem `itemId` para que seleção, ownership e lookup não dependam apenas de nome ou CLSID. Eles não possuem payload físico.

### 4.2 Layout físico

Para itens de arquivo:

```text
%LocalAppData%\DesktopFences\Items\{ItemId}\{nome-do-payload}
```

Regras:

- um diretório de item contém no máximo um payload principal;
- nomes iguais não colidem porque cada item possui diretório próprio;
- o caminho é derivado do `ItemId`; mudar de fence não muda o caminho;
- não persistir `FenceId` no path físico;
- diretórios desconhecidos não são excluídos automaticamente;
- diretório vazio só pode ser removido depois do commit que encerra a custódia do item.

### 4.3 Modelo persistido alvo

O schema do `layout.json` sobe para a versão `2`. O item continua dentro da lista da fence proprietária, tornando a mudança de fence uma alteração de metadados no documento.

Exemplo conceitual:

```json
{
  "version": 2,
  "fences": [
    {
      "id": "fence-guid",
      "title": "Trabalho",
      "items": [
        {
          "itemId": "item-guid",
          "kind": "stored",
          "name": "Relatorio.docx",
          "storageName": "Relatorio.docx",
          "originalPath": "C:\\Users\\...\\Desktop\\Relatorio.docx",
          "originalX": 12,
          "originalY": 48
        }
      ]
    }
  ]
}
```

`storageName` é relativo ao diretório do `ItemId`; não gravar uma raiz absoluta de `%LocalAppData%` como fonte de verdade. Durante a migração, o leitor aceita a versão 1, mas todo novo commit deve produzir somente a versão 2.

---

## 5. Persistência atômica

### 5.1 Arquivos

```text
%AppData%\DesktopFences\layout.json
%AppData%\DesktopFences\layout.json.bak
%AppData%\DesktopFences\layout.json.tmp
%LocalAppData%\DesktopFences\Transactions\{OperationId}.json
```

O arquivo temporário deve ficar no mesmo diretório do layout para permitir substituição atômica no mesmo volume.

### 5.2 Commit do layout

1. serializar o documento completo para `layout.json.tmp`;
2. fechar a serialização e executar flush durável;
3. validar que o temporário pode ser desserializado e respeita invariantes básicas;
4. se já existir layout válido, substituí-lo atomicamente e manter a versão anterior em `layout.json.bak`;
5. no primeiro save, promover o temporário por rename no mesmo volume;
6. nunca apagar o backup antes de existir um novo layout válido;
7. uma falha de commit é devolvida ao orquestrador e não é capturada silenciosamente como sucesso.

O backup representa o último layout válido anterior, não uma cópia feita depois de uma possível corrupção.

---

## 6. Transações recuperáveis

### 6.1 Natureza da transação

O sistema de arquivos e o JSON não formam uma transação ACID única. A Fase 6 implementa uma transação de aplicação baseada em journal, com etapas duráveis e recovery idempotente.

Cada journal contém:

- `operationId` e tipo (`inbound`, `outbound`, `removeFence`, `pause`, `shutdown`, `migration`);
- estado da operação;
- lista de itens com `itemId`, origem, destino físico, fence de origem/destino e resultado individual;
- versão/revisão esperada do layout antes e depois do commit;
- informação suficiente para repetir ou compensar a etapa sem adivinhação por nome.

Estados mínimos:

```text
Prepared → PayloadChanged → LayoutCommitted → Completed
                     ↘ FailedRecoverable
```

O journal também é gravado por temporário + substituição atômica. Atualizações de estado devem ser duráveis antes de avançar para a etapa seguinte. O plano `Prepared` já contém todos os itens; por isso, uma queda durante o loop físico pode ser reconciliada inspecionando origem e destino, sem fazer um flush de journal para cada item.

### 6.2 Entrada do Desktop para a fence

1. planejar o lote sem alterar arquivo nem UI;
2. criar os `ItemId` e persistir `Prepared`;
3. mover cada payload confirmado para o store estável e coletar o resultado individual em memória;
4. persistir um único checkpoint `PayloadChanged` para o estágio, independentemente do tamanho do lote;
5. se todo o lote exigido não puder concluir, compensar os itens já movidos e manter a UI/layout anterior;
6. se todos concluírem, fazer um único commit atômico do layout;
7. somente depois atualizar a UI como concluída e notificar a Shell;
8. marcar `Completed` e remover o journal quando não for mais necessário.

A semântica adotada para o gesto é **tudo ou nada**: falha em um item impede que apenas parte da seleção pareça ter entrado na fence. Se a compensação não puder terminar, o journal permanece para recovery e nenhum payload é apagado.

### 6.3 Saída da fence para o Desktop

1. persistir o plano e os destinos finais antes do primeiro move;
2. reservar nomes não destrutivos para conflitos no Desktop;
3. restaurar os payloads e coletar resultados em memória;
4. persistir um checkpoint do estágio físico, não um save por item;
5. fazer um único commit removendo os itens restaurados do layout;
6. em falha parcial, usar o journal para concluir ou compensar sem sobrescrever arquivos;
7. não remover a fence nem encerrar a custódia enquanto houver item sem estado recuperável.

Pausar, Sair e remover fence usam o mesmo coordenador de transação e lote; não criam caminhos paralelos de restore.

### 6.4 Transferência entre fences

Transferir um ou vários itens entre fences exige somente:

1. calcular nova fence e nova ordem;
2. produzir um novo documento em memória;
3. fazer um único commit atômico do layout;
4. atualizar as coleções visuais depois do commit.

Não há operação de payload, `SHChangeNotify`, restore, re-hide ou mudança de tracker físico. Se o save falhar, origem e UI permanecem como antes.

---

## 7. Recovery no arranque

O recovery ocorre antes de mostrar as fences e antes de aceitar novos drops.

Ordem:

1. tentar carregar e validar `layout.json`;
2. se inválido, tentar `layout.json.bak` sem sobrescrever o arquivo defeituoso;
3. enumerar journals pendentes;
4. reconciliar cada operação pelo seu tipo, estágio e presença real de origem/destino;
5. validar que cada item `stored` do layout possui o payload esperado;
6. identificar diretórios de `ItemId` sem referência, preservá-los e registrar estado de recuperação;
7. só então abrir as fences.

Decisões:

- `Prepared` sem alteração física pode ser cancelado com segurança;
- inbound com payload movido e layout não commitado deve ser compensado para o Desktop; se isso não for seguro, permanece pendente e visível no diagnóstico de recovery;
- layout commitado tem precedência para completar a limpeza do journal;
- outbound com payload já restaurado e layout antigo deve concluir a remoção de metadados quando o journal provar a operação;
- dois layouts inválidos ou uma situação ambígua não autorizam criar layout vazio nem limpar o store;
- recovery deve ser idempotente: executá-lo novamente produz o mesmo estado final.

O aplicativo deve informar de forma simples quando uma recuperação automática não puder terminar e oferecer acesso à pasta do item. Uma UI completa de gerenciamento de recovery não faz parte desta fase; preservar dados e tornar o problema observável, sim.

---

## 8. Migração da versão 1

A migração do store `{FenceId}` para `{ItemId}` faz parte da Fase 6 e usa o mesmo journal.

1. carregar o layout v1 válido sem alterá-lo;
2. gerar `ItemId` para cada item;
3. construir e persistir o plano completo de migração;
4. mover cada payload da pasta da fence para seu diretório estável;
5. persistir o layout v2 por commit atômico, mantendo backup do v1;
6. remover somente diretórios antigos comprovadamente vazios;
7. se houver falha antes do commit v2, compensar ou concluir no próximo arranque pelo journal.

A migração nunca busca payload apenas por stem quando houver ambiguidade. Ausência, duplicidade ou conflito preserva os arquivos e interrompe a migração com estado recuperável.

Enquanto a migração não concluir, a aplicação não aceita novas operações de custódia. Não há suporte para voltar a executar uma versão anterior do aplicativo sobre um layout v2.

---

## 9. Processamento em lote

Todo gesto de entrada, saída, transferência, Pausar, Sair ou remover fence passa por um pipeline único:

```text
Capture → Resolve → Plan → Journal → Execute → Commit → Apply UI → Notify
```

Requisitos:

- capturar o `SysListView32` no máximo uma vez quando a operação precisar das posições atuais;
- normalizar, deduplicar e validar todos os itens antes do primeiro move;
- usar índices por `ItemId` e path normalizado para evitar buscas repetidas por nome;
- extrair/atualizar a apresentação dos itens depois do planejamento, sem salvar por item;
- coletar resultado por item, mas decidir o commit no nível do lote;
- persistir o journal por estágio, com quantidade de checkpoints `O(1)` em relação ao número de itens;
- persistir layout uma vez por gesto;
- emitir `SHChangeNotify` somente ao final e no máximo uma vez por diretório afetado;
- impedir reentrada concorrente em itens que já participam de uma transação.

A fase não introduz processamento assíncrono geral. O objetivo de performance é remover trabalho repetido e I/O desnecessário sem ampliar o escopo para progresso/cancelamento.

---

## 10. Responsabilidades por camada

```text
DesktopFences.Core
  ItemId e schema v2
  planos e estados de transação
  regras de migração e recovery
  índices e invariantes de lote
  serialização sem API Windows

DesktopFences.Native
  move/restore físico do Desktop com resultado estruturado por item
  captura única do Desktop
  notificação final da Shell
  nenhum conhecimento de FenceWindow

DesktopFences.App
  coordenador da operação
  commit único via serviços Core/Native
  aplicar UI somente após sucesso
  mensagem de recovery não resolvido
```

`IntPtr`, COM, structs Win32 e P/Invoke permanecem exclusivamente em `DesktopFences.Native`.

---

## 11. Compatibilidade e falhas

- Schema v1: leitura e migração suportadas.
- Schema v2: formato gravado após a Fase 6.
- Layout ausente no primeiro uso: criação normal, já em v2.
- Layout primário corrompido: carregar backup válido e preservar o corrompido para diagnóstico.
- Store inacessível: não remover item do layout nem afirmar restore/hide concluído.
- Save indisponível: não mudar ownership visual e não remover journal.
- Conflito de nome no restore: escolher destino não destrutivo e registrar o path final.
- Falha no segundo item de um lote: compensar o primeiro; não apresentar lote parcial como sucesso.

---

## 12. Critérios de aceite da fase

### Automatizados

- round-trip do schema v2 e leitura do v1;
- migração v1 → v2 com arquivos, pastas, atalhos e namespace;
- store derivado de `ItemId`, independente de `FenceId`;
- transferência entre fences altera somente o layout;
- commit atômico preserva backup e não deixa JSON parcial como principal;
- recovery em cada transição de estado do journal;
- recovery repetido é idempotente;
- falha parcial de lote executa compensação e mantém estado anterior;
- conflitos de nome nunca sobrescrevem destino;
- diretório órfão é preservado e reportado;
- um gesto produz uma chamada de captura/save/notificação nos testes de orquestração.

### Windows 11 real

- migrar uma instalação existente com itens em duas fences;
- adicionar 1, 10, 50 e 100 itens do Desktop por seleção múltipla;
- transferir 1, 50 e 100 itens entre fences e comprovar zero movimentação de payload;
- ejetar itens, Pausar, Retomar, remover fence e Sair;
- matar o processo em cada checkpoint injetável e validar o próximo arranque;
- corromper o layout principal e confirmar uso do backup sem perda do store;
- bloquear um item no meio de um lote e confirmar que o lote não fica parcialmente aplicado;
- validar itens com nomes iguais em fences diferentes;
- repetir o gate da Fase 5: restart do Explorer, DPI e Win+D.

### Performance

- transferência entre fences: `O(n)` somente em metadados/memória e zero I/O de payload;
- entrada de lote: uma captura, um commit de layout e uma notificação por diretório;
- saída de lote: um commit de layout e uma notificação por diretório;
- checkpoints duráveis do journal: quantidade fixa por estágio, sem save por item;
- o caso de um único item no mesmo volume não pode apresentar regressão perceptível frente à versão anterior;
- medir antes/depois com os mesmos conjuntos; registrar tempo total, número de capturas, commits, notificações e moves físicos.

---

## 13. Gate e release

A Fase 6 começou após o cumprimento dos três pré-requisitos:

- [x] gate humano da Fase 5 fechado;
- [x] pedido explícito de implementação;
- [x] confirmação de que esta especificação continua aceita.

O gate Windows 11 e os critérios de aceite da Fase 6 foram aprovados. A entrega `v0.5.0` recebeu parecer `GO` dentro do escopo desta especificação. O instalador permanece como Fase 7 e exige novo pedido explícito.
