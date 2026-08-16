# AGENTS.md — DesktopFences

Regras de comportamento para qualquer agente (humano ou LLM) que trabalhe neste repositório.

> Não inicie implementação antes de ler, nesta ordem:
> 1. `docs/SESSION-HEADER.md`
> 2. este arquivo (`AGENTS.md`)
> 3. `docs/SPEC.md`
> 4. `docs/plano-implementacao.md`
> 5. `README.md`

Documentos de apoio (ler quando o assunto for relevante):

- `docs/pos-mvp1.md` — mapa curto do ciclo pós-MVP 1 (não implementar sem pedido)
- `docs/adr/` — decisões arquiteturais já fechadas

---

## O que este projeto é

App nativo Windows 11 (C# / .NET 8 / WPF) que agrupa ícones reais da área de trabalho em “fences” translúcidas, no espírito do Stardock Fences. O **MVP 2** (`v0.3.1`) entrega N fences, Configurações (cores, idioma pt/en, iniciar com o Windows) e o hide/restore do MVP 1. As Fases 3–6 estão fechadas e validadas; a versão `v0.5.0` está preparada. A Fase 7 é o instalador e só avança com autorização no `SESSION-HEADER.md` e pedido explícito. Duplo clique no desktop e packs de tema estão fora deste ciclo.

Diferencial em relação aos clones open source: **esconder os ícones reais do `SysListView32` e desenhar a nossa grade por cima**. DeskFrame, NoFences e OpenFences não fazem isso — eles mostram atalhos/pastas em janelas flutuantes enquanto os ícones originais continuam no desktop.

---

## O que pode e não pode fazer

### Pode

- Implementar **apenas a etapa atual** marcada no `SESSION-HEADER.md`, quando o desenvolvedor pediu explicitamente.
- Refinar código/docs da etapa atual se encontrar inconsistência — e **reportar** a inconsistência antes de expandir o escopo.
- Adicionar testes para lógica de domínio (Core) sempre que criar regra nova.
- Manter `IntPtr` / P/Invoke **somente** em `src/DesktopFences.Native`.

### Não pode

- Avançar para a próxima etapa sem o desenvolvedor marcar o gate no `SESSION-HEADER.md` **e** pedir o avanço.
- Copiar código dos três clones (DeskFrame / NoFences / OpenFences). São MIT; mesmo assim reimplementamos os **padrões**, não os arquivos.
- Deixar `IntPtr`, structs Win32 ou `DllImport` vazar para `DesktopFences.Core` ou para code-behind WPF além de uma fachada fina.
- Introduzir Electron, WinUI 3, MAUI ou outra stack sem um ADR novo aceito pelo desenvolvedor.
- Commitar, taguear ou fazer push sem o desenvolvedor pedir.
- Assinar binário, alterar política de Defender, ou pedir privilégio de administrador como solução padrão. O app corre `asInvoker`.
- Esconder ícones reais de forma irreversível: toda operação de hide **tem** que ter restore no shutdown e em falha.

---

## Camadas (obrigatório)

```
DesktopFences.App      → WPF, XAML, animações. Não fala Win32 cru.
DesktopFences.Native   → P/Invoke, COM Shell, OLE drop, DWM, SysListView32.
DesktopFences.Core     → modelos, hit-test, persistência JSON. Sem Windows API.
```

Se uma feature precisa de Win32, a Native expõe um serviço com tipos de Core (records, enums). A App consome o serviço.

---

## Como gerenciar fases

1. A etapa vigente está em `docs/SESSION-HEADER.md` e o detalhe operacional em `docs/plano-implementacao.md`. O MVP 1, o MVP 2 (Fases 1–2) e as Fases 3–6 estão fechados. A Fase 7 é o instalador e ainda não foi iniciada. Duplo clique no desktop, packs de tema e empurrar vizinhos **não** entram neste ciclo.
2. Cada passo tem um **gate de validação do desenvolvedor**. O agente marca `[x]` só o que **ele** implementou e testou no código; o gate `[ ]` do desenvolvedor permanece até o humano validar no Windows 11 real.
3. Ao concluir uma etapa, o agente atualiza:
   - `docs/SESSION-HEADER.md` (contexto + checklist)
   - o passo correspondente em `docs/plano-implementacao.md`
   - `README.md` se o fluxo de build/run ou o que o produto faz mudou
   - `docs/pos-mvp1.md` se o recorte do pós-MVP mudou
4. Se o código divergir da spec, a spec ganha — a menos que o desenvolvedor peça mudança de decisão, e aí se cria/atualiza um ADR.

---

## Convenções de código

- C# 12, nullable enabled, implicit usings.
- Comentários em português só onde a API do Windows for não-óbvia (por que `WriteProcessMemory`, por que `GWL_HWNDPARENT`).
- Nomes de tipo/membro em inglês.
- Testes xUnit + FluentAssertions: domínio em `tests/DesktopFences.Core.Tests`; orquestração App/Native em `tests/DesktopFences.App.Tests`.
- Não adicionar pacote NuGet sem registrar o motivo no passo ou num ADR (exceção: pacotes de teste).

---

## Release

O workflow `.github/workflows/release.yml` dispara **somente** em push de tag `v*` (ex.: `v0.1.0`). Push de branch/commit **não** gera artefato. Não altere o `on:` desse workflow para incluir branches sem pedido explícito.
