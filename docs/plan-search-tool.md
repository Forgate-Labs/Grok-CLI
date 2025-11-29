# Planejamento: SearchTool

## Objetivo
Implementar um tool call para pesquisa de texto em arquivos usando `ripgrep` (rg) no Linux/macOS/Bash e `Select-String` no PowerShell, detectando automaticamente a plataforma.

## Requisitos

### Funcionais
- Pesquisar texto/padrão em arquivos dentro de diretórios
- Suportar busca recursiva em subdiretórios
- Suportar regex patterns
- Suportar filtros por tipo de arquivo (extensões)
- Mostrar contexto (linhas antes/depois do match)
- Mostrar número de linha onde o texto foi encontrado
- Detectar automaticamente se está em Linux/macOS (ripgrep) ou Windows (PowerShell)
- Respeitar o diretório de trabalho atual
- Suportar case-sensitive e case-insensitive search

### Não Funcionais
- Performance: busca rápida mesmo em projetos grandes
- Cross-platform: funcionar em Windows, Linux e macOS
- Timeout: limitar tempo máximo de busca para evitar travamento
- Output formatting: resultados bem formatados e fáceis de ler

## Arquitetura

### 1. Classe SearchTool
**Localização**: `/GrokCLI/GrokCLI.tui/Tools/SearchTool.cs`

**Implementação**:
```csharp
public class SearchTool : ITool
{
    private readonly IWorkingDirectoryService _workingDirService;
    private readonly ISearchService _searchService;

    public string Name => "search";
    public string Description => "Search for text patterns in files using ripgrep (Linux/macOS) or Select-String (Windows)";

    // Implementar GetChatTool() e ExecuteAsync()
}
```

**Schema JSON** para o tool:
```json
{
    "type": "object",
    "properties": {
        "pattern": {
            "type": "string",
            "description": "The text or regex pattern to search for"
        },
        "path": {
            "type": "string",
            "description": "Directory or file to search in (default: current directory)"
        },
        "file_type": {
            "type": "string",
            "description": "Filter by file extension (e.g., 'cs', 'txt', 'json')"
        },
        "case_sensitive": {
            "type": "boolean",
            "description": "Whether the search should be case-sensitive (default: false)"
        },
        "context_lines": {
            "type": "integer",
            "description": "Number of context lines to show before and after match (default: 0)"
        },
        "max_results": {
            "type": "integer",
            "description": "Maximum number of results to return (default: 100)"
        },
        "regex": {
            "type": "boolean",
            "description": "Treat pattern as regex (default: false, literal search)"
        }
    },
    "required": ["pattern"]
}
```

### 2. Serviço de Pesquisa
**Localização**: `/GrokCLI/GrokCLI.tui/Services/ISearchService.cs` e `SearchService.cs`

**Interface**:
```csharp
public interface ISearchService
{
    Task<SearchResult> SearchAsync(SearchOptions options);
    bool IsRipgrepAvailable();
    string GetPlatformType();
}

public class SearchOptions
{
    public string Pattern { get; set; }
    public string SearchPath { get; set; } = ".";
    public string? FileType { get; set; }
    public bool CaseSensitive { get; set; } = false;
    public int ContextLines { get; set; } = 0;
    public int MaxResults { get; set; } = 100;
    public bool IsRegex { get; set; } = false;
    public int TimeoutSeconds { get; set; } = 30;
}

public class SearchResult
{
    public bool Success { get; set; }
    public List<SearchMatch> Matches { get; set; } = new();
    public int TotalMatches { get; set; }
    public string SearchCommand { get; set; }
    public string? Error { get; set; }
    public string Platform { get; set; }
}

public class SearchMatch
{
    public string FilePath { get; set; }
    public int LineNumber { get; set; }
    public string LineContent { get; set; }
    public List<string> ContextBefore { get; set; } = new();
    public List<string> ContextAfter { get; set; } = new();
}
```

### 3. Detecção de Plataforma

```csharp
public class SearchService : ISearchService
{
    private readonly PlatformType _platform;

    public SearchService()
    {
        _platform = DetectPlatform();
    }

    private PlatformType DetectPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return PlatformType.Windows;
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return PlatformType.Linux;
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return PlatformType.MacOS;
        else
            return PlatformType.Unknown;
    }

    public string GetPlatformType()
    {
        return _platform.ToString();
    }
}

public enum PlatformType
{
    Windows,
    Linux,
    MacOS,
    Unknown
}
```

### 4. Implementação de Busca - Linux/macOS (ripgrep)

```csharp
private async Task<SearchResult> SearchWithRipgrepAsync(SearchOptions options)
{
    var args = new List<string>();

    // Pattern e flags básicas
    args.Add(options.IsRegex ? "-e" : "-F"); // -e regex, -F fixed string
    args.Add(options.Pattern);

    // Case sensitivity
    if (!options.CaseSensitive)
        args.Add("-i");

    // Context lines
    if (options.ContextLines > 0)
        args.Add($"-C {options.ContextLines}");

    // Número de linha
    args.Add("-n");

    // Max count
    args.Add($"-m {options.MaxResults}");

    // File type
    if (!string.IsNullOrEmpty(options.FileType))
        args.Add($"-t {options.FileType}");

    // JSON output para parsing mais fácil
    args.Add("--json");

    // Path
    args.Add(options.SearchPath);

    var command = $"rg {string.Join(" ", args)}";

    var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = "rg",
            Arguments = string.Join(" ", args),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = options.SearchPath
        }
    };

    var result = new SearchResult
    {
        SearchCommand = command,
        Platform = "Linux/macOS (ripgrep)"
    };

    try
    {
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        var timeout = Task.Delay(TimeSpan.FromSeconds(options.TimeoutSeconds));
        var processTask = process.WaitForExitAsync();

        if (await Task.WhenAny(processTask, timeout) == timeout)
        {
            process.Kill();
            result.Error = "Search timed out";
            return result;
        }

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode == 0 || process.ExitCode == 1) // 1 = no matches found
        {
            result.Matches = ParseRipgrepJsonOutput(output);
            result.TotalMatches = result.Matches.Count;
            result.Success = true;
        }
        else
        {
            result.Error = error;
        }
    }
    catch (Exception ex)
    {
        result.Error = $"Error executing ripgrep: {ex.Message}";
    }

    return result;
}

private List<SearchMatch> ParseRipgrepJsonOutput(string jsonOutput)
{
    var matches = new List<SearchMatch>();
    var lines = jsonOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    foreach (var line in lines)
    {
        try
        {
            var json = JsonDocument.Parse(line);
            var root = json.RootElement;

            if (root.GetProperty("type").GetString() == "match")
            {
                var data = root.GetProperty("data");
                var match = new SearchMatch
                {
                    FilePath = data.GetProperty("path").GetProperty("text").GetString(),
                    LineNumber = data.GetProperty("line_number").GetInt32(),
                    LineContent = data.GetProperty("lines").GetProperty("text").GetString()
                };
                matches.Add(match);
            }
        }
        catch
        {
            // Skip malformed JSON lines
        }
    }

    return matches;
}
```

### 5. Implementação de Busca - Windows (PowerShell)

```csharp
private async Task<SearchResult> SearchWithPowerShellAsync(SearchOptions options)
{
    var psCommand = BuildPowerShellSearchCommand(options);

    var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -Command \"{psCommand}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = options.SearchPath
        }
    };

    var result = new SearchResult
    {
        SearchCommand = psCommand,
        Platform = "Windows (PowerShell)"
    };

    try
    {
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        var timeout = Task.Delay(TimeSpan.FromSeconds(options.TimeoutSeconds));
        var processTask = process.WaitForExitAsync();

        if (await Task.WhenAny(processTask, timeout) == timeout)
        {
            process.Kill();
            result.Error = "Search timed out";
            return result;
        }

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode == 0)
        {
            result.Matches = ParsePowerShellOutput(output);
            result.TotalMatches = result.Matches.Count;
            result.Success = true;
        }
        else
        {
            result.Error = error;
        }
    }
    catch (Exception ex)
    {
        result.Error = $"Error executing PowerShell: {ex.Message}";
    }

    return result;
}

private string BuildPowerShellSearchCommand(SearchOptions options)
{
    var pattern = options.Pattern.Replace("\"", "`\""); // Escape quotes

    var cmd = new StringBuilder();
    cmd.Append($"Get-ChildItem -Path '{options.SearchPath}' -Recurse");

    // File type filter
    if (!string.IsNullOrEmpty(options.FileType))
    {
        cmd.Append($" -Filter '*.{options.FileType}'");
    }

    cmd.Append(" | Select-String");

    // Pattern
    if (options.IsRegex)
    {
        cmd.Append($" -Pattern \"{pattern}\"");
    }
    else
    {
        cmd.Append($" -Pattern \"{Regex.Escape(pattern)}\"");
    }

    // Case sensitivity
    if (!options.CaseSensitive)
    {
        cmd.Append(" -CaseSensitive:$false");
    }

    // Context
    if (options.ContextLines > 0)
    {
        cmd.Append($" -Context {options.ContextLines},{options.ContextLines}");
    }

    // Max results
    cmd.Append($" | Select-Object -First {options.MaxResults}");

    // Format output as JSON for easier parsing
    cmd.Append(" | ConvertTo-Json -Depth 3");

    return cmd.ToString();
}

private List<SearchMatch> ParsePowerShellOutput(string jsonOutput)
{
    var matches = new List<SearchMatch>();

    try
    {
        var json = JsonDocument.Parse(jsonOutput);
        var items = json.RootElement.ValueKind == JsonValueKind.Array
            ? json.RootElement.EnumerateArray()
            : new[] { json.RootElement }.AsEnumerable();

        foreach (var item in items)
        {
            var match = new SearchMatch
            {
                FilePath = item.GetProperty("Path").GetString() ?? "",
                LineNumber = item.GetProperty("LineNumber").GetInt32(),
                LineContent = item.GetProperty("Line").GetString() ?? ""
            };

            // Parse context if present
            if (item.TryGetProperty("Context", out var context))
            {
                if (context.TryGetProperty("PreContext", out var pre))
                {
                    match.ContextBefore = pre.EnumerateArray()
                        .Select(e => e.GetString() ?? "")
                        .ToList();
                }
                if (context.TryGetProperty("PostContext", out var post))
                {
                    match.ContextAfter = post.EnumerateArray()
                        .Select(e => e.GetString() ?? "")
                        .ToList();
                }
            }

            matches.Add(match);
        }
    }
    catch
    {
        // If JSON parsing fails, try line-by-line parsing
    }

    return matches;
}
```

### 6. Verificação de Disponibilidade do Ripgrep

```csharp
public bool IsRipgrepAvailable()
{
    try
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "rg",
                Arguments = "--version",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        process.WaitForExit(1000);

        return process.ExitCode == 0;
    }
    catch
    {
        return false;
    }
}
```

## Estrutura de Resposta

### Sucesso
```json
{
    "success": true,
    "total_matches": 5,
    "platform": "Linux/macOS (ripgrep)",
    "search_command": "rg -F -i -n -m 100 'search term' .",
    "matches": [
        {
            "file_path": "./src/Program.cs",
            "line_number": 42,
            "line_content": "    var searchTerm = \"example\";",
            "context_before": [],
            "context_after": []
        }
    ]
}
```

### Erro
```json
{
    "success": false,
    "error": "Search timed out after 30 seconds",
    "platform": "Linux/macOS (ripgrep)",
    "search_command": "rg -F -i -n 'pattern' ."
}
```

## Fluxo de Execução

```
1. Grok chama search com pattern e opções
   ↓
2. SearchTool recebe argumentsJson
   ↓
3. Parse JSON e cria SearchOptions
   ↓
4. Resolver path com WorkingDirectoryService
   ↓
5. SearchService detecta plataforma
   ↓
6. Se Windows:
   - Usar SearchWithPowerShellAsync()
   Se Linux/macOS:
   - Verificar se ripgrep está disponível
   - Usar SearchWithRipgrepAsync()
   - Fallback para grep se rg não disponível
   ↓
7. Executar comando de busca
   ↓
8. Parse output (JSON ou texto)
   ↓
9. Limitar resultados a max_results
   ↓
10. Retornar SearchResult formatado
```

## Exemplos de Uso

### 1. Busca Simples
```json
{
    "pattern": "TODO",
    "path": "."
}
```

### 2. Busca em Arquivos C#
```json
{
    "pattern": "public class",
    "file_type": "cs",
    "case_sensitive": true
}
```

### 3. Busca com Regex
```json
{
    "pattern": "function\\s+\\w+\\(",
    "regex": true,
    "file_type": "js"
}
```

### 4. Busca com Contexto
```json
{
    "pattern": "error",
    "context_lines": 2,
    "case_sensitive": false
}
```

## Comparação de Comandos

### Ripgrep (Linux/macOS)
```bash
# Busca simples
rg -F "pattern" .

# Busca com case-insensitive
rg -F -i "pattern" .

# Busca em arquivos .cs
rg -F -t cs "pattern" .

# Busca com contexto
rg -F -C 2 "pattern" .

# Busca com regex
rg -e "pattern.*regex" .

# JSON output
rg -F --json "pattern" .
```

### PowerShell (Windows)
```powershell
# Busca simples
Get-ChildItem -Recurse | Select-String "pattern"

# Busca em arquivos .cs
Get-ChildItem -Recurse -Filter "*.cs" | Select-String "pattern"

# Busca com contexto
Get-ChildItem -Recurse | Select-String "pattern" -Context 2,2

# Case-sensitive
Get-ChildItem -Recurse | Select-String "pattern" -CaseSensitive

# Output como JSON
Get-ChildItem -Recurse | Select-String "pattern" | ConvertTo-Json
```

## Considerações de Performance

1. **Timeout**: Sempre definir timeout para evitar buscas infinitas
2. **Max Results**: Limitar número de resultados para não sobrecarregar output
3. **File Type Filter**: Usar filtros de tipo para reduzir escopo
4. **Git Ignore**: Ripgrep respeita `.gitignore` automaticamente
5. **Binary Files**: Ripgrep pula arquivos binários por padrão

## Fallback Strategy

```csharp
public async Task<SearchResult> SearchAsync(SearchOptions options)
{
    if (_platform == PlatformType.Windows)
    {
        return await SearchWithPowerShellAsync(options);
    }
    else if (_platform == PlatformType.Linux || _platform == PlatformType.MacOS)
    {
        if (IsRipgrepAvailable())
        {
            return await SearchWithRipgrepAsync(options);
        }
        else
        {
            // Fallback to grep
            return await SearchWithGrepAsync(options);
        }
    }
    else
    {
        return new SearchResult
        {
            Success = false,
            Error = "Unsupported platform"
        };
    }
}
```

## Integração com Program.cs

```csharp
services.AddSingleton<ISearchService, SearchService>();
services.AddSingleton<ITool, SearchTool>();
```

## Melhorias Futuras

1. **Highlighting**: Destacar matches no output
2. **Statistics**: Mostrar estatísticas de busca (tempo, arquivos pesquisados)
3. **Exclude Patterns**: Suportar exclusão de diretórios/arquivos
4. **Replace Mode**: Permitir substituição após busca
5. **Save Results**: Salvar resultados em arquivo
6. **Interactive Mode**: Navegar pelos resultados interativamente

## Checklist de Implementação

- [ ] Criar `ISearchService.cs` com interfaces
- [ ] Criar models `SearchOptions`, `SearchResult`, `SearchMatch`
- [ ] Criar `SearchService.cs`
- [ ] Implementar detecção de plataforma
- [ ] Implementar `SearchWithRipgrepAsync()`
- [ ] Implementar parser de JSON do ripgrep
- [ ] Implementar `SearchWithPowerShellAsync()`
- [ ] Implementar parser de output do PowerShell
- [ ] Implementar `IsRipgrepAvailable()`
- [ ] Implementar fallback para `grep` (opcional)
- [ ] Implementar timeout mechanism
- [ ] Criar `SearchTool.cs`
- [ ] Implementar `GetChatTool()` com schema completo
- [ ] Implementar `ExecuteAsync()`
- [ ] Registrar serviço e tool em `Program.cs`
- [ ] Criar testes unitários
- [ ] Testar em Linux com ripgrep
- [ ] Testar em Windows com PowerShell
- [ ] Testar em macOS (se possível)
- [ ] Documentar uso no README.md
- [ ] Adicionar exemplos de uso
