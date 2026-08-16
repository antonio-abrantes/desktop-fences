# Spec breve — hotfix de ícones de sistema

**Status:** implementado no código; gate Windows 11 pendente.

**Escopo:** Este Computador, Rede e Lixeira. Nenhuma mudança no fluxo de arquivos, pastas ou atalhos.

## Diagnóstico

A causa está confirmada no fluxo da Fase 6:

1. `DesktopCustodyBatch.PlanInbound` trata um item já classificado como `Namespace` antes de passar por `DesktopHide.For`.
2. O valor retornado pela Shell, normalmente `::{GUID}`, é encaminhado sem normalização para `SetNamespaceHidden`.
3. O Registro do Explorer exige o nome canônico `{GUID}`, sem o prefixo `::`.
4. A gravação de `::{GUID}` não falha, então a transação é confirmada e o item aparece na fence, mas o Explorer ignora a configuração e mantém o ícone no Desktop.

A inspeção somente leitura do Registro confirmou valores incorretos criados para os três itens:

```text
::{20D04FE0-3AEA-1069-A2D8-08002B30309D}
::{645FF040-5081-101B-9F08-00AA002F954E}
::{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}
```

Também existe um erro independente no fallback `shell:RecycleBinFolder`: o código usava `5084`, mas o CLSID correto da Lixeira contém `5081`.

## Correção aplicada

1. `DesktopHide.TryNamespaceKey` / `RequireNamespaceKey` / `IsCanonicalNamespaceKey` normalizam `::{GUID}`, `shell:…` ou `{GUID}` para `{GUID}` em maiúsculas.
2. `PlanInbound`, `PlanOutbound` e `SetNamespaceHidden` usam essa normalização; valor inválido falha o planejamento ou o apply — nunca é gravado no Registro.
3. CLSID da Lixeira corrigido para `{645FF040-5081-101B-9F08-00AA002F954E}`.
4. Ao ocultar/restaurar, remove-se o valor legado `::{GUID}` nas chaves `NewStartPanel` e `ClassicStartMenu`.
5. Grava-se `1` para ocultar e `0` para restaurar só com o CLSID canônico; o apply só sucede se a leitura confirmar o valor e a ausência do legado.
6. O Path na UI continua `::{GUID}` (`ToShellParsingName`) para o Shell abrir o ícone; o Registro usa só `{GUID}`.
7. `FlushShell` no fim do lote (coordenador) mantém-se; falha de apply compensa o lote e não confirma layout/UI.

Não se usam coordenadas fora da tela, reinício do Explorer, polling nem move físico para esses itens.

## Testes e gate

- [x] Normalização de `::{GUID}`, `{GUID}` e aliases `shell:` dos três itens.
- [x] Rejeição de chave não normalizável e garantia de que `::{GUID}` nunca permanece após apply.
- [x] Inbound/Outbound de namespace via lote: escreve canónico e limpa legado (GUID sintético no Registro).
- [ ] Gate Windows 11: testar individualmente Este Computador, Rede e Lixeira, incluindo reinício do Explorer e novo arranque do app.

**Complexidade:** baixa a média. **Impacto de performance:** desprezível; duas gravações de Registro e uma atualização da Shell por lote, sem trabalho contínuo.
