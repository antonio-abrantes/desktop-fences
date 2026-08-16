# Plano complementar — Fase 7: instalador

**Versão alvo:** `v0.6.0`.

**Spec:** [spec-fase-7-instalador.md](spec-fase-7-instalador.md).

## Implementação

- [x] 7.1 — modo de manutenção e saída segura da instância em execução
- [x] 7.2 — liberação offline transacional e política manter/resetar/remover
- [x] 7.3 — idioma inicial e atualização do path estável no arranque
- [x] 7.4 — instalador/desinstalador Inno Setup x64 e ARM64
- [x] 7.5 — integração com a release e documentação
- [x] 7.6 — ajuste emergencial: conciliar somente referências de itens confirmadamente apagados enquanto o app esteve fechado
- [x] 197 testes, builds e publishes automatizados
- [x] compilação local dos dois instaladores com Inno Setup 6.7.3

## Gate manual no Windows 11

- [ ] instalação limpa em Português e em Inglês
- [ ] idioma escolhido chega ao app e continua alterável nas Configurações
- [ ] upgrade `v0.5.1` → `v0.6.0` preservando configurações
- [ ] instalação com “começar com configurações novas” restaura os itens e arquiva o estado anterior
- [ ] desinstalação mantendo configurações e reinstalação posterior
- [ ] desinstalação removendo tudo
- [ ] falha simulada na devolução cancela a limpeza e preserva Recovery/store
- [ ] inicialização com o Windows aponta para o path instalado após upgrade
- [ ] Aplicativos instalados mostra apenas uma entrada
- [ ] setups `win-x64` e `win-arm64` validados nas arquiteturas correspondentes

O gate manual permanece aberto até validação humana; a implementação automática não recebe parecer público final antes dele.
