# Planejamento: ChangeDirectoryTool

## Objetivo
Implementar um tool call que permite navegar entre diretórios de forma virtual, mantendo estado do diretório atual durante a sessão, sempre começando do diretório onde a aplicação foi iniciada.

## Requisitos

### Funcionais
- Permitir mudança de diretório relativo e absoluto
- Manter estado persistente do diretório atual durante a sessão
- Sempre iniciar no diretório onde a aplicação foi executada
- Suportar navegação para diretórios pais (`..`)
- Suportar navegação para diretório home (`~`)
- Validar se o diretório de destino existe antes de navegar
- Retornar o caminho completo do diretório atual após a mudança

### Não Funcionais
- Thread-safe (múltiplas chamadas simultâneas devem ser seguras)
- Performance: validação de diretório deve ser rápida
- Cross-platform: funcionar em Windows, Linux e macOS

## Arquitetura

### 1. Classe ChangeDirectoryTool
**Localização**: `/GrokCLI/GrokCLI.tui/Tools/ChangeDirectoryTool.cs`

**Implementação**:
```csharp
public class ChangeDirectoryTool : ITool
{
    private readonly IWorkingDirectoryService _workingDirService;

    public string Name => "change_directory";
    public string Description => "Changes the current working directory";

    // Implementar GetChatTool() e ExecuteAsync()
}
```

**Schema JSON** para o tool:
```json
{
    "type": "object",
    "properties": {
        "path": {
            "type": "string",
            "description": "The target directory path (relative or absolute)"
        }
    },
    "required": ["path"]
}
```

### 2. Serviço de Diretório de Trabalho
**Localização**: `/GrokCLI/GrokCLI.tui/Services/IWorkingDirectoryService.cs` e `WorkingDirectoryService.cs`

**Interface**:
```csharp
public interface IWorkingDirectoryService
{
    string GetCurrentDirectory();
    void SetCurrentDirectory(string path);
    string ResolveRelativePath(string path);
    bool DirectoryExists(string path);
}
```

**Implementação**:
```csharp
public class WorkingDirectoryService : IWorkingDirectoryService
{
    private string _currentDirectory;
    private readonly string _initialDirectory;
    private readonly object _lock = new object();

    public WorkingDirectoryService()
    {
        _initialDirectory = Directory.GetCurrentDirectory();
        _currentDirectory = _initialDirectory;
    }

    // Implementar métodos thread-safe
}
```

### 3. Lógica de Resolução de Paths

**Regras de resolução**:
1. **Path absoluto**: Usar diretamente (ex: `/home/user/docs` ou `C:\Users\user\docs`)
2. **Path relativo**: Resolver a partir do `_currentDirectory` atual
3. **Path com `..`**: Resolver navegação para diretório pai
4. **Path com `~`**: Expandir para diretório home do usuário
5. **Path com `.`**: Manter diretório atual

**Método de resolução**:
```csharp
public string ResolveRelativePath(string path)
{
    // 1. Expandir ~ para home directory
    if (path.StartsWith("~"))
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        path = path.Replace("~", homeDir);
    }

    // 2. Se for path absoluto, usar diretamente
    if (Path.IsPathRooted(path))
    {
        return Path.GetFullPath(path);
    }

    // 3. Combinar com diretório atual e normalizar
    var combined = Path.Combine(_currentDirectory, path);
    return Path.GetFullPath(combined);
}
```

## Estrutura de Dados

### Resposta de Sucesso
```json
{
    "success": true,
    "current_directory": "/home/user/documents",
    "message": "Changed directory to /home/user/documents"
}
```

### Resposta de Erro
```json
{
    "success": false,
    "current_directory": "/home/user",
    "error": "Directory not found: /home/user/nonexistent"
}
```

## Fluxo de Execução

```
1. Grok chama change_directory com path
   ↓
2. ChangeDirectoryTool recebe argumentsJson
   ↓
3. Parse JSON e extrai "path"
   ↓
4. WorkingDirectoryService.ResolveRelativePath(path)
   ↓
5. Verificar se diretório existe
   ↓
6. Se existe:
   - SetCurrentDirectory(resolvedPath)
   - Retornar sucesso com novo diretório
   Se não existe:
   - Retornar erro mantendo diretório atual
```

## Casos de Teste

### 1. Navegação Relativa
```
Current: /home/user
Command: cd documents
Result: /home/user/documents
```

### 2. Navegação Absoluta
```
Current: /home/user/documents
Command: cd /tmp
Result: /tmp
```

### 3. Navegação para Parent
```
Current: /home/user/documents/work
Command: cd ..
Result: /home/user/documents
```

### 4. Navegação para Home
```
Current: /tmp
Command: cd ~
Result: /home/user
```

### 5. Diretório Inexistente
```
Current: /home/user
Command: cd nonexistent
Result: Error, mantém /home/user
```

### 6. Path com Múltiplos ..
```
Current: /home/user/documents/work/project
Command: cd ../../..
Result: /home/user
```

## Integração com Program.cs

**Registrar serviço e tool**:
```csharp
services.AddSingleton<IWorkingDirectoryService, WorkingDirectoryService>();
services.AddSingleton<ITool, ChangeDirectoryTool>();
```

## Considerações de Segurança

1. **Path Traversal**: Validar que o path normalizado não escape de áreas permitidas (se necessário)
2. **Permissions**: Verificar se o usuário tem permissão de leitura no diretório
3. **Symlinks**: Decidir se permite ou bloqueia symlinks

## Cross-Platform Considerations

1. **Separadores de Path**:
   - Windows: `\` e `/` (ambos aceitos)
   - Linux/macOS: `/`
   - Solução: Usar `Path.DirectorySeparatorChar` e métodos de `Path` class

2. **Case Sensitivity**:
   - Windows: Case-insensitive
   - Linux/macOS: Case-sensitive
   - Solução: Usar `StringComparison.OrdinalIgnoreCase` em Windows

3. **Home Directory**:
   - `Environment.SpecialFolder.UserProfile` funciona em todas as plataformas

## Melhorias Futuras

1. **Histórico de navegação**: Implementar `cd -` para voltar ao diretório anterior
2. **Autocompletar**: Sugerir diretórios disponíveis
3. **Permissões**: Verificar permissões antes de navegar
4. **Bookmarks**: Permitir salvar diretórios favoritos
5. **List Directory**: Adicionar comando `ls` ou `dir` para listar conteúdo

## Checklist de Implementação

- [ ] Criar `IWorkingDirectoryService.cs`
- [ ] Criar `WorkingDirectoryService.cs`
- [ ] Implementar resolução de paths (absoluto, relativo, ~, ..)
- [ ] Implementar thread-safety com locks
- [ ] Criar `ChangeDirectoryTool.cs`
- [ ] Implementar `GetChatTool()` com schema correto
- [ ] Implementar `ExecuteAsync()` com tratamento de erros
- [ ] Registrar serviço e tool em `Program.cs`
- [ ] Criar testes unitários para resolução de paths
- [ ] Criar testes unitários para navegação
- [ ] Testar em Windows
- [ ] Testar em Linux
- [ ] Testar em macOS (se possível)
- [ ] Documentar uso no README.md
