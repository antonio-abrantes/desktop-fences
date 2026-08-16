# Plano complementar — Fase 6: custódia transacional de itens do Desktop

**Status:** fechada; marcos 6.1–6.4 implementados e validações automatizada/Windows 11 aprovadas.

**Spec:** [spec-fase-6-custodia-desktop.md](spec-fase-6-custodia-desktop.md).

**Regra de release:** os quatro marcos abaixo pertencem a uma única fase e a um único gate final.

---

## 1. Resultado da fase

Ao fim desta fase:

- cada item do Desktop sob custódia terá store estável por `ItemId`;
- mover entre fences será somente uma mudança atômica de ownership/ordem no layout;
- movimento físico, layout e recovery serão coordenados por journal;
- `layout.json` terá gravação atômica e backup válido;
- operações com vários itens usarão captura, planejamento, commit e notificação em lote;
- instalações existentes serão migradas do schema/store v1 para v2 sem perda silenciosa.

Não serão implementadas nesta fase políticas para origem externa, OneDrive, operações longas, progresso/cancelamento ou instalador.

---

## 2. Estratégia de entrega

A implementação pode ocorrer em quatro marcos internos, sempre na mesma branch e sem release entre eles:

| Marco | Bloco | Complexidade | Dependência |
|---|---|---:|---|
| 6.1 | Modelo v2, store por `ItemId` e migração recuperável | Alta | Base da fase |
| 6.2 | JSON atômico, backup, journal e recovery | Alta | 6.1 |
| 6.3 | Transferência entre fences somente por metadados | Média | 6.1–6.2 |
| 6.4 | Orquestração em lote e validação final | Média–alta | 6.1–6.3 |

O gate humano ocorre somente depois de 6.4. Uma falha que impeça concluir qualquer marco mantém toda a Fase 6 pendente.

Estimativa inicial para uma pessoa familiarizada com o projeto: **3 a 5 semanas**, incluindo testes automatizados, injeção de falhas e correções encontradas no gate local. A validação humana no Windows 11 é adicional. É uma estimativa de planejamento, não prazo fechado.

---

## 3. Preparação e linha de base

- [x] Confirmar gate da Fase 5 no Windows 11 e registrar o resultado.
- [x] Criar uma cópia de teste com layout v1 e payloads reais representativos.
- [x] Registrar baseline estrutural da `v0.4.0` e medições físicas da Fase 6 com 1, 10, 50 e 100 itens.
- [x] Mapear todos os caminhos atuais de entrada, saída, Pausar, Sair, remover fence e transferência.
- [x] Identificar qualquer save/hide/restore paralelo fora do `FenceHost` que precise convergir para o coordenador único.
- [x] Definir checkpoints de falha habilitados somente em build de teste.

**Saída:** baseline registrado e nenhum fluxo de custódia esquecido.

---

## 4. Marco 6.1 — modelo v2, `ItemId` e store estável

### Core

- [x] Adicionar `ItemId` obrigatório ao modelo de item.
- [x] Diferenciar item `stored` de namespace sem introduzir API Windows no Core.
- [x] Definir schema v2 e leitor compatível com schema v1.
- [x] Derivar o caminho relativo do store pelo `ItemId`, não por `FenceId`.
- [x] Criar índices por `ItemId`, original path e storage path normalizados.
- [x] Validar unicidade de `ItemId` e impedir duas referências ao mesmo payload.

### Migração

- [x] Planejar v1 → v2 antes de qualquer move.
- [x] Gerar `ItemId` estável para todos os itens existentes.
- [x] Mover payloads `{FenceId}` → `{ItemId}` sob journal de migração.
- [x] Preservar layout v1 como backup até o commit v2.
- [x] Não remover pasta antiga que ainda contenha qualquer arquivo.
- [x] Interromper com recovery observável diante de item ausente ou ambíguo.

### Testes

- [x] Round-trip v2.
- [x] Leitura de todos os campos opcionais do v1.
- [x] Migração de arquivo, pasta, `.lnk`, `.url` e namespace.
- [x] Nomes iguais em fences distintas geram stores distintos.
- [x] Reinício em cada checkpoint da migração mantém recovery possível.

**Saída:** modelo e store deixam de depender da fence; instalações v1 migram de forma recuperável.

---

## 5. Marco 6.2 — transação, JSON atômico, backup e recovery

### Persistência

- [x] Substituir gravação direta por temporário + flush + validação + substituição atômica.
- [x] Manter `layout.json.bak` como último layout válido anterior.
- [x] Devolver falha de save ao chamador; remover catches silenciosos do caminho crítico.
- [x] Impedir dois commits concorrentes do layout.
- [x] Identificar revisão anterior/posterior no journal para evitar aplicar operação sobre layout inesperado.

### Journal

- [x] Modelar tipos e estados definidos na spec.
- [x] Persistir journal atomicamente antes do primeiro efeito físico.
- [x] Coletar resultado individual e persistir checkpoints por estágio, sem save de journal por item.
- [x] Manter journal até que payload, layout e UI tenham um estado coerente.
- [x] Tornar conclusão e compensação idempotentes.

### Recovery

- [x] Executar recovery antes de abrir as fences.
- [x] Carregar backup se o layout principal estiver inválido.
- [x] Reconciliar inbound, outbound, Pausar/Sair, remoção de fence e migração.
- [x] Detectar payload esperado ausente e store órfão sem excluir dados.
- [x] Exibir aviso simples e acesso à pasta quando a recuperação automática não concluir.
- [x] Nunca criar layout vazio por cima de primário e backup inválidos.

### Resultado físico mínimo

- [x] O serviço de move/restore deve retornar sucesso/falha e path final por item.
- [x] O coordenador só avança ao commit quando a pós-condição física do item do Desktop estiver confirmada.
- [x] Este contrato é restrito ao necessário para a transação da Fase 6; a ampliação geral de `IFileOperation` permanece futura.

### Testes

- [x] Falha antes/depois de cada mudança de estado do journal.
- [x] JSON truncado antes da promoção não substitui o principal válido.
- [x] Principal corrompido carrega backup sem limpar store.
- [x] Falha no segundo item compensa o primeiro.
- [x] Recovery executado duas vezes produz o mesmo resultado.
- [x] Conflito no restore nunca sobrescreve destino.
- [x] Falha de save preserva ownership e UI anteriores.

**Saída:** nenhuma operação concluída pode deixar payload sem referência recuperável.

---

## 6. Marco 6.3 — transferência entre fences por metadados

- [x] Remover o move físico entre diretórios de fences.
- [x] Alterar ownership e ordem em uma cópia do documento em memória.
- [x] Fazer um único commit atômico por transferência, inclusive multi-seleção.
- [x] Aplicar as coleções visuais somente depois do commit.
- [x] Em falha de save, manter itens na fence de origem e não alterar trackers.
- [x] Não chamar hide, restore, captura do Desktop ou `SHChangeNotify`.
- [x] Preservar `ItemId`, diretório do store, `originalPath` e metadados de restore.

### Testes

- [x] Um item A → B: zero chamada ao serviço de move.
- [x] Cem itens A → B: um save e zero I/O de payload.
- [x] Falha de save: modelo e UI permanecem em A.
- [x] Corpo transfere; chrome/recolhida mantém comportamento atual.
- [x] Pausar/Sair depois da transferência restaura todos ao Desktop.

**Saída:** transferência visual não pode mais falhar por lock ou movimentação física desnecessária.

---

## 7. Marco 6.4 — processamento em lote

### Orquestração

- [x] Criar pipeline único `Capture → Resolve → Plan → Journal → Execute → Commit → Apply UI → Notify`.
- [x] Fazer no máximo uma captura do Desktop por gesto que precise dela.
- [x] Resolver, normalizar e deduplicar a seleção inteira antes do primeiro move.
- [x] Bloquear reentrada para `ItemId` que participe de operação ativa.
- [x] Executar entrada com semântica tudo ou nada e compensação em falha.
- [x] Reusar o mesmo coordenador em ejetar, Pausar, Sair e remover fence.
- [x] Fazer um commit do layout por gesto.
- [x] Notificar a Shell uma vez por diretório afetado, somente no final.
- [x] Atualizar a UI depois do commit, em uma única aplicação de lote.

### Performance

- [x] Substituir buscas lineares críticas por índices do marco 6.1.
- [x] Eliminar chamadas redundantes a hide/save feitas dentro e depois do `foreach`.
- [x] Comparar 1, 10, 50 e 100 itens com o baseline estrutural.
- [x] Confirmar que um item no mesmo volume não teve regressão perceptível.
- [x] Confirmar zero moves físicos entre fences.
- [x] Registrar contadores de captura, save, notificação e move nos testes.
- [x] Confirmar que os checkpoints duráveis do journal não crescem com a quantidade de itens.

### Testes

- [x] Entrada em lote bem-sucedida: uma captura, um save, uma notificação.
- [x] Falha no meio: lote não aparece parcialmente na UI/layout.
- [x] Saída em lote: um save e destinos não destrutivos.
- [x] Pausar/Sair/remover fence compartilham as mesmas garantias.
- [x] Multi-seleção, reorder e namespace não sofrem regressão.

**Saída:** custo deixa de crescer por repetição de capturas e saves dentro do mesmo gesto.

---

## 8. Validação integrada

### Automatizada

- [x] Todos os testes existentes continuam verdes.
- [x] Novos testes Core cobrem modelo, migração, journal, recovery e lote.
- [x] Regressão cobre layout v1 misto: payloads já restaurados ao Desktop e payloads ainda no store antigo.
- [x] Testes de orquestração usam doubles para contar captura, move, save e notificação.
- [x] Build Debug e Release sem erros nem novos avisos.
- [x] Nenhum novo pacote NuGet sem decisão documentada.

### Windows 11 real

- [x] Migrar layout/store real de teste com duas fences.
- [x] Executar a matriz de 1, 10, 50 e 100 itens.
- [x] Transferir blocos entre fences e observar que os paths físicos não mudam.
- [x] Ejetar, Pausar, Retomar, remover fence e Sair.
- [x] Matar o processo em cada checkpoint de teste e validar recovery.
- [x] Testar layout principal corrompido e backup válido.
- [x] Testar arquivo bloqueado durante entrada e durante restore.
- [x] Reiniciar Explorer, mudar DPI e usar Win+D.
- [x] Verificar manualmente que nenhum arquivo foi sobrescrito ou apagado.

### Documentação ao concluir

- [x] Atualizar `docs/SESSION-HEADER.md` com a Fase 6 fechada e validada.
- [x] Atualizar `docs/SPEC.md` para tornar o schema v2 o contrato vigente.
- [x] Atualizar `docs/plano-implementacao.md` com os itens efetivamente concluídos.
- [x] Atualizar `README.md` se paths, recovery ou comportamento visível mudarem.
- [x] Registrar medições antes/depois e limitações conhecidas.

---

## 9. Gate final da Fase 6

O desenvolvedor valida no Windows 11:

- [x] migração v1 → v2 sem perda;
- [x] entrada e saída em lote;
- [x] transferência entre fences sem I/O físico;
- [x] falhas e encerramentos recuperados no próximo arranque;
- [x] fallback para backup de layout;
- [x] Pausar/Sair/remover fence restaurando corretamente;
- [x] performance mantida ou melhorada frente ao baseline;
- [x] Fase 5 continua funcionando.

Gate cumprido e Fase 6 encerrada. A Fase 7 pode ser autorizada por novo pedido explícito. Os itens mantidos em stand-by continuam registrados na auditoria como melhorias futuras e não bloqueiam esta entrega.

---

## 10. Stand-by — não implementar nesta fase

- validação completa de `IFileOperation` e operações parciais genéricas;
- trabalho assíncrono, progresso e cancelamento para pasta grande/outro volume/rede;
- política para arquivos externos ao Desktop;
- proteção específica para OneDrive, placeholders e Desktop redirecionado;
- alinhamento geral do protocolo OLE;
- cache progressivo de ícones e outras otimizações não medidas;
- instalador, atualização e desinstalação.

Esses pontos permanecem em [auditoria-fluxo-itens-performance-release.md](auditoria-fluxo-itens-performance-release.md) como riscos ou melhorias futuras.
