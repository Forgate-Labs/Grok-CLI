# Roadmap de Implementação - GrokCLI Tools

Este documento serve como índice para os planos de implementação das novas funcionalidades do GrokCLI.

## Visão Geral

O GrokCLI está sendo expandido para incluir ferramentas de manipulação de arquivos e sistema, com suporte completo para múltiplas plataformas (Windows, Linux, macOS).

## Funcionalidades Planejadas

### 1. ChangeDirectoryTool
**Status**: Planejado
**Arquivo**: [plan-change-directory-tool.md](./plan-change-directory-tool.md)

Navegação virtual de diretórios com estado persistente durante a sessão.

**Principais recursos**:
- Navegação relativa e absoluta
- Suporte para `..` (parent) e `~` (home)
- Thread-safe
- Integração com `IWorkingDirectoryService`

**Dependências**:
- Nenhuma (pode ser implementado primeiro)

---

### 2. EditFileTool
**Status**: Planejado
**Arquivo**: [plan-edit-file-tool.md](./plan-edit-file-tool.md)

Edição de arquivos de texto com múltiplas operações (replace, insert, append, delete, write).

**Principais recursos**:
- Operações atômicas
- Sistema de backup automático
- Preservação de encoding e line endings
- Integração com diretório de trabalho virtual

**Dependências**:
- `IWorkingDirectoryService` (do ChangeDirectoryTool)
- `IPlatformService` (do Cross-Platform Support)

---

### 3. SearchTool
**Status**: Planejado
**Arquivo**: [plan-search-tool.md](./plan-search-tool.md)

Pesquisa de texto em arquivos usando `ripgrep` (Linux/macOS) ou `Select-String` (Windows).

**Principais recursos**:
- Busca recursiva em diretórios
- Suporte para regex e literal search
- Filtros por tipo de arquivo
- Contexto de linhas (antes/depois)
- Fallback automático quando ripgrep não disponível

**Dependências**:
- `IPlatformService` (do Cross-Platform Support)
- `IWorkingDirectoryService` (do ChangeDirectoryTool)

---

### 4. Cross-Platform Support
**Status**: Planejado
**Arquivo**: [plan-cross-platform-support.md](./plan-cross-platform-support.md)

Infraestrutura para suportar Windows (PowerShell), Linux (Bash) e macOS (Bash/Zsh).

**Principais recursos**:
- Detecção automática de plataforma
- Normalização de paths e line endings
- Adaptador de comandos shell
- Executor de comandos com timeout
- Helpers para diferenças de filesystem

**Dependências**:
- Nenhuma (infraestrutura base)

---

## Ordem de Implementação Recomendada

### Fase 1: Infraestrutura Base
1. **Cross-Platform Support** ✅ Implementar primeiro
   - `IPlatformService` e `PlatformService`
   - `IShellExecutor` e `ShellExecutor`
   - `ICommandAdapter` e `CommandAdapter`
   - Helpers (LineEndingDetector, FileSystemHelper)

### Fase 2: Navegação de Diretório
2. **ChangeDirectoryTool**
   - `IWorkingDirectoryService` e `WorkingDirectoryService`
   - `ChangeDirectoryTool`
   - Testes unitários

### Fase 3: Manipulação de Arquivos
3. **EditFileTool**
   - `IFileEditService` e `FileEditService`
   - `EditFileTool`
   - Sistema de backup
   - Testes unitários

### Fase 4: Busca
4. **SearchTool**
   - `ISearchService` e `SearchService`
   - Implementação para ripgrep (Linux/macOS)
   - Implementação para PowerShell (Windows)
   - `SearchTool`
   - Testes unitários

### Fase 5: Integração e Testes
5. **Integração Completa**
   - Registrar todos os serviços em `Program.cs`
   - Atualizar `CodeExecutionTool` para usar novos serviços
   - Testes de integração cross-platform
   - Documentação no README

---

## Grafo de Dependências

```
Cross-Platform Support (PlatformService)
    │
    ├─→ ChangeDirectoryTool (WorkingDirectoryService)
    │       │
    │       ├─→ EditFileTool (FileEditService)
    │       │
    │       └─→ SearchTool (SearchService)
    │
    └─→ CodeExecutionTool (atualização)
```

---

## Estimativa de Esforço

| Funcionalidade | Complexidade | Estimativa |
|----------------|--------------|------------|
| Cross-Platform Support | Alta | ~4-6 horas |
| ChangeDirectoryTool | Baixa | ~2-3 horas |
| EditFileTool | Média | ~3-4 horas |
| SearchTool | Alta | ~4-5 horas |
| **Total** | - | **~13-18 horas** |

---

## Critérios de Aceitação

### Cross-Platform Support
- [x] Planejamento completo
- [ ] Detecta corretamente Windows, Linux e macOS
- [ ] Normaliza paths corretamente em cada plataforma
- [ ] Executa comandos shell nativos de cada plataforma
- [ ] Testes passam em Windows, Linux e macOS

### ChangeDirectoryTool
- [x] Planejamento completo
- [ ] Navega para paths relativos e absolutos
- [ ] Suporta `..` e `~`
- [ ] Mantém estado persistente
- [ ] Valida existência de diretórios
- [ ] Thread-safe

### EditFileTool
- [x] Planejamento completo
- [ ] Operações: replace, insert, append, delete, write
- [ ] Cria backups automaticamente
- [ ] Preserva encoding original
- [ ] Preserva line endings originais
- [ ] Operações são atômicas

### SearchTool
- [x] Planejamento completo
- [ ] Funciona com ripgrep no Linux/macOS
- [ ] Funciona com Select-String no Windows
- [ ] Fallback automático quando ripgrep não disponível
- [ ] Suporta regex e literal search
- [ ] Filtros por tipo de arquivo funcionam
- [ ] Timeout previne buscas infinitas

---

## Riscos e Mitigações

### Risco 1: Ripgrep não instalado
**Mitigação**: Implementar fallback para `grep` (Linux/macOS) ou `Select-String` (Windows)

### Risco 2: Diferenças de encoding entre plataformas
**Mitigação**: Forçar UTF-8 universalmente e detectar encoding original de arquivos

### Risco 3: Paths com espaços ou caracteres especiais
**Mitigação**: Sempre usar aspas ao construir comandos shell

### Risco 4: Operações em arquivos grandes
**Mitigação**: Implementar limites de tamanho e usar streams para arquivos grandes

### Risco 5: Permissões de arquivo
**Mitigação**: Verificar permissões antes de operações e retornar erros claros

---

## Testes Necessários

### Testes Unitários
- [ ] PlatformService detecta plataforma corretamente
- [ ] Path normalization funciona em cada plataforma
- [ ] WorkingDirectoryService resolve paths corretamente
- [ ] FileEditService executa cada operação corretamente
- [ ] SearchService constrói comandos corretos para cada plataforma

### Testes de Integração
- [ ] ChangeDirectoryTool + EditFileTool trabalham juntos
- [ ] ChangeDirectoryTool + SearchTool trabalham juntos
- [ ] Todos os tools funcionam com paths relativos e absolutos

### Testes Cross-Platform
- [ ] Todos os testes passam no Windows
- [ ] Todos os testes passam no Linux
- [ ] Todos os testes passam no macOS

---

## Documentação Necessária

- [ ] Atualizar README.md com novos tools
- [ ] Adicionar exemplos de uso de cada tool
- [ ] Documentar requisitos (ripgrep opcional)
- [ ] Adicionar seção de troubleshooting
- [ ] Documentar diferenças entre plataformas

---

## Melhorias Futuras (Pós-MVP)

1. **List Directory Tool**: Tool para listar conteúdo de diretórios
2. **File Operations Tool**: Copiar, mover, deletar arquivos
3. **Git Integration Tool**: Comandos git básicos
4. **Diff Tool**: Comparar arquivos
5. **Compress/Extract Tool**: Trabalhar com arquivos comprimidos
6. **Network Tool**: Download de arquivos, HTTP requests
7. **Process Tool**: Listar e gerenciar processos

---

## Contato e Suporte

Para dúvidas sobre a implementação, consulte os arquivos de planejamento individuais ou abra uma issue no repositório.

---

**Última Atualização**: 2025-01-29
**Status Geral**: Planejamento Completo ✅
