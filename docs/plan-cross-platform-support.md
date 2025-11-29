# Planejamento: Cross-Platform Support (Bash & PowerShell)

## Objetivo
Fazer o GrokCLI funcionar perfeitamente tanto em sistemas Linux/macOS (Bash) quanto Windows (PowerShell), detectando automaticamente o sistema operacional e adaptando comandos e comportamentos conforme necessário.

## Requisitos

### Funcionais
- Detectar automaticamente o sistema operacional no início da aplicação
- Adaptar comandos shell para cada plataforma (Bash vs PowerShell)
- Usar separadores de path corretos para cada SO
- Usar line endings corretos (LF vs CRLF)
- Suportar diferenças de encoding (UTF-8 universal)
- Adaptar comandos de tools para cada plataforma
- Fornecer mensagens de erro específicas da plataforma
- Suportar paths tanto com `/` quanto `\` no Windows

### Não Funcionais
- Zero configuração manual: detecção automática
- Consistent UX: mesma experiência em todas as plataformas
- Performance: detecção de plataforma sem overhead
- Robustez: fallbacks quando comandos não estão disponíveis

## Arquitetura

### 1. Serviço de Detecção de Plataforma
**Localização**: `/GrokCLI/GrokCLI.tui/Services/IPlatformService.cs` e `PlatformService.cs`

**Interface**:
```csharp
public interface IPlatformService
{
    PlatformType Platform { get; }
    string ShellType { get; }
    string PathSeparator { get; }
    string LineEnding { get; }
    string HomeDirectory { get; }
    bool IsWindows { get; }
    bool IsLinux { get; }
    bool IsMacOS { get; }

    string NormalizePath(string path);
    string GetShellCommand(string command);
    ProcessStartInfo CreateShellProcess(string command);
}

public enum PlatformType
{
    Windows,
    Linux,
    MacOS,
    Unknown
}
```

**Implementação**:
```csharp
public class PlatformService : IPlatformService
{
    public PlatformType Platform { get; }
    public string ShellType { get; }
    public string PathSeparator { get; }
    public string LineEnding { get; }
    public string HomeDirectory { get; }

    public bool IsWindows => Platform == PlatformType.Windows;
    public bool IsLinux => Platform == PlatformType.Linux;
    public bool IsMacOS => Platform == PlatformType.MacOS;

    public PlatformService()
    {
        Platform = DetectPlatform();
        ShellType = GetShellType();
        PathSeparator = Path.DirectorySeparatorChar.ToString();
        LineEnding = IsWindows ? "\r\n" : "\n";
        HomeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    private PlatformType DetectPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return PlatformType.Windows;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return PlatformType.Linux;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return PlatformType.MacOS;
        return PlatformType.Unknown;
    }

    private string GetShellType()
    {
        return Platform switch
        {
            PlatformType.Windows => "PowerShell",
            PlatformType.Linux => "Bash",
            PlatformType.MacOS => "Bash/Zsh",
            _ => "Unknown"
        };
    }

    public string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        // Expandir ~
        if (path.StartsWith("~"))
        {
            path = path.Replace("~", HomeDirectory);
        }

        // Normalizar separadores
        if (IsWindows)
        {
            // Windows aceita ambos / e \
            path = path.Replace('/', '\\');
        }
        else
        {
            // Linux/macOS usa apenas /
            path = path.Replace('\\', '/');
        }

        // Normalizar path completo
        return Path.GetFullPath(path);
    }

    public ProcessStartInfo CreateShellProcess(string command)
    {
        if (IsWindows)
        {
            return new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -Command \"{command.Replace("\"", "`\"")}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
        }
        else
        {
            // Linux/macOS
            return new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{command.Replace("\"", "\\\"")}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
        }
    }

    public string GetShellCommand(string command)
    {
        // Pode ser usado para traduzir comandos entre plataformas
        // Por exemplo, traduzir comandos Linux para PowerShell equivalents
        return command;
    }
}
```

### 2. Adaptador de Comandos Cross-Platform
**Localização**: `/GrokCLI/GrokCLI.tui/Services/ICommandAdapter.cs` e `CommandAdapter.cs`

**Interface**:
```csharp
public interface ICommandAdapter
{
    string ListDirectory(string path);
    string ChangeDirectory(string path);
    string ReadFile(string path);
    string WriteFile(string path, string content);
    string DeleteFile(string path);
    string CopyFile(string source, string destination);
    string MoveFile(string source, string destination);
    string CreateDirectory(string path);
    string FindFiles(string pattern, string path);
    string SearchInFiles(string pattern, string path);
}
```

**Implementação**:
```csharp
public class CommandAdapter : ICommandAdapter
{
    private readonly IPlatformService _platformService;

    public CommandAdapter(IPlatformService platformService)
    {
        _platformService = platformService;
    }

    public string ListDirectory(string path)
    {
        return _platformService.IsWindows
            ? $"Get-ChildItem -Path '{path}' | Format-Table Name, Length, LastWriteTime"
            : $"ls -lah '{path}'";
    }

    public string ChangeDirectory(string path)
    {
        return _platformService.IsWindows
            ? $"Set-Location '{path}'"
            : $"cd '{path}'";
    }

    public string ReadFile(string path)
    {
        return _platformService.IsWindows
            ? $"Get-Content -Path '{path}' -Encoding UTF8"
            : $"cat '{path}'";
    }

    public string WriteFile(string path, string content)
    {
        var escapedContent = content.Replace("'", "''");
        return _platformService.IsWindows
            ? $"Set-Content -Path '{path}' -Value '{escapedContent}' -Encoding UTF8"
            : $"echo '{escapedContent}' > '{path}'";
    }

    public string DeleteFile(string path)
    {
        return _platformService.IsWindows
            ? $"Remove-Item -Path '{path}' -Force"
            : $"rm -f '{path}'";
    }

    public string CopyFile(string source, string destination)
    {
        return _platformService.IsWindows
            ? $"Copy-Item -Path '{source}' -Destination '{destination}' -Force"
            : $"cp '{source}' '{destination}'";
    }

    public string MoveFile(string source, string destination)
    {
        return _platformService.IsWindows
            ? $"Move-Item -Path '{source}' -Destination '{destination}' -Force"
            : $"mv '{source}' '{destination}'";
    }

    public string CreateDirectory(string path)
    {
        return _platformService.IsWindows
            ? $"New-Item -ItemType Directory -Path '{path}' -Force"
            : $"mkdir -p '{path}'";
    }

    public string FindFiles(string pattern, string path)
    {
        return _platformService.IsWindows
            ? $"Get-ChildItem -Path '{path}' -Filter '{pattern}' -Recurse -File"
            : $"find '{path}' -name '{pattern}' -type f";
    }

    public string SearchInFiles(string pattern, string path)
    {
        return _platformService.IsWindows
            ? $"Get-ChildItem -Path '{path}' -Recurse -File | Select-String -Pattern '{pattern}'"
            : $"grep -r '{pattern}' '{path}'";
    }
}
```

### 3. Executor de Comandos Cross-Platform
**Localização**: `/GrokCLI/GrokCLI.tui/Services/IShellExecutor.cs` e `ShellExecutor.cs`

**Interface**:
```csharp
public interface IShellExecutor
{
    Task<ShellResult> ExecuteAsync(string command, int timeoutSeconds = 30);
    Task<ShellResult> ExecuteAsync(string command, string workingDirectory, int timeoutSeconds = 30);
}

public class ShellResult
{
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string Output { get; set; } = "";
    public string Error { get; set; } = "";
    public string Command { get; set; } = "";
    public string Platform { get; set; } = "";
}
```

**Implementação**:
```csharp
public class ShellExecutor : IShellExecutor
{
    private readonly IPlatformService _platformService;

    public ShellExecutor(IPlatformService platformService)
    {
        _platformService = platformService;
    }

    public async Task<ShellResult> ExecuteAsync(
        string command,
        int timeoutSeconds = 30)
    {
        return await ExecuteAsync(command, Directory.GetCurrentDirectory(), timeoutSeconds);
    }

    public async Task<ShellResult> ExecuteAsync(
        string command,
        string workingDirectory,
        int timeoutSeconds = 30)
    {
        var processInfo = _platformService.CreateShellProcess(command);
        processInfo.WorkingDirectory = workingDirectory;

        var result = new ShellResult
        {
            Command = command,
            Platform = _platformService.ShellType
        };

        try
        {
            using var process = new Process { StartInfo = processInfo };

            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            var timeout = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
            var processTask = process.WaitForExitAsync();

            if (await Task.WhenAny(processTask, timeout) == timeout)
            {
                process.Kill(true);
                result.Error = $"Command timed out after {timeoutSeconds} seconds";
                result.ExitCode = -1;
                return result;
            }

            result.Output = await outputTask;
            result.Error = await errorTask;
            result.ExitCode = process.ExitCode;
            result.Success = process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            result.Error = $"Error executing command: {ex.Message}";
            result.ExitCode = -1;
        }

        return result;
    }
}
```

## Diferenças Entre Plataformas

### 1. Separadores de Path

| Plataforma | Separador | Exemplo |
|------------|-----------|---------|
| Windows    | `\` ou `/` | `C:\Users\user\documents` |
| Linux      | `/` | `/home/user/documents` |
| macOS      | `/` | `/Users/user/documents` |

**Solução**: Usar `Path.Combine()` e `Path.GetFullPath()` sempre.

### 2. Line Endings

| Plataforma | Line Ending | Código |
|------------|-------------|--------|
| Windows    | CRLF | `\r\n` |
| Linux      | LF | `\n` |
| macOS      | LF | `\n` |

**Solução**: Detectar line ending do arquivo original e preservar.

```csharp
public class LineEndingDetector
{
    public static string DetectLineEnding(string content)
    {
        if (content.Contains("\r\n"))
            return "\r\n";
        if (content.Contains("\n"))
            return "\n";
        return Environment.NewLine;
    }

    public static string NormalizeLineEndings(string content, string targetLineEnding)
    {
        return content.Replace("\r\n", "\n").Replace("\n", targetLineEnding);
    }
}
```

### 3. Comandos Shell Equivalentes

| Operação | Bash (Linux/macOS) | PowerShell (Windows) |
|----------|-------------------|---------------------|
| Listar | `ls -la` | `Get-ChildItem` ou `dir` |
| Copiar | `cp source dest` | `Copy-Item source dest` |
| Mover | `mv source dest` | `Move-Item source dest` |
| Deletar | `rm file` | `Remove-Item file` |
| Criar diretório | `mkdir -p dir` | `New-Item -ItemType Directory dir` |
| Ler arquivo | `cat file` | `Get-Content file` |
| Pesquisar | `grep pattern file` | `Select-String pattern file` |
| Encontrar | `find . -name '*.txt'` | `Get-ChildItem -Filter '*.txt' -Recurse` |

### 4. Executáveis Disponíveis

| Tool | Windows | Linux | macOS | Alternativa |
|------|---------|-------|-------|-------------|
| `python3` | ❌ (usar `python`) | ✅ | ✅ | `python` |
| `rg` (ripgrep) | ⚠️ (instalar) | ⚠️ (instalar) | ⚠️ (instalar) | `grep` ou `Select-String` |
| `git` | ✅ | ✅ | ✅ | - |
| `curl` | ⚠️ (instalar) | ✅ | ✅ | `Invoke-WebRequest` |

**Solução**: Implementar fallbacks.

```csharp
public class PythonExecutor
{
    private readonly IPlatformService _platform;
    private readonly string _pythonCommand;

    public PythonExecutor(IPlatformService platform)
    {
        _platform = platform;
        _pythonCommand = DetectPythonCommand();
    }

    private string DetectPythonCommand()
    {
        var candidates = _platform.IsWindows
            ? new[] { "python", "python3", "py" }
            : new[] { "python3", "python" };

        foreach (var cmd in candidates)
        {
            if (IsCommandAvailable(cmd))
                return cmd;
        }

        throw new Exception("Python is not installed or not in PATH");
    }

    private bool IsCommandAvailable(string command)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
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
}
```

### 5. Encoding

**Problema**: Windows historicamente usava encodings diferentes (Windows-1252, etc.).

**Solução**: Forçar UTF-8 em toda a aplicação.

```csharp
// No Program.cs
Console.OutputEncoding = new UTF8Encoding(false);
Console.InputEncoding = new UTF8Encoding(false);

// Em ProcessStartInfo
StartInfo = new ProcessStartInfo
{
    StandardOutputEncoding = Encoding.UTF8,
    StandardErrorEncoding = Encoding.UTF8
}
```

### 6. Case Sensitivity

| Plataforma | Filesystem | String Comparison |
|------------|------------|-------------------|
| Windows    | Case-insensitive | `StringComparison.OrdinalIgnoreCase` |
| Linux      | Case-sensitive | `StringComparison.Ordinal` |
| macOS      | Case-insensitive* | `StringComparison.OrdinalIgnoreCase` |

*macOS pode ser case-sensitive dependendo do filesystem (APFS vs HFS+).

**Solução**:
```csharp
public class FileSystemHelper
{
    private readonly IPlatformService _platform;

    public StringComparison PathComparison =>
        _platform.IsLinux
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

    public bool PathsEqual(string path1, string path2)
    {
        var normalized1 = _platform.NormalizePath(path1);
        var normalized2 = _platform.NormalizePath(path2);
        return string.Equals(normalized1, normalized2, PathComparison);
    }
}
```

## Integração com Tools Existentes

### Atualizar CodeExecutionTool

```csharp
public class CodeExecutionTool : ITool
{
    private readonly IPlatformService _platform;
    private readonly string _pythonCommand;

    public CodeExecutionTool(IPlatformService platform)
    {
        _platform = platform;
        _pythonCommand = platform.IsWindows ? "python" : "python3";
    }

    public async Task<ToolExecutionResult> ExecuteAsync(string argumentsJson)
    {
        var processInfo = _platform.CreateShellProcess($"{_pythonCommand} -c \"{code}\"");
        // ... rest of implementation
    }
}
```

### Usar em Novos Tools

```csharp
public class ChangeDirectoryTool : ITool
{
    private readonly IWorkingDirectoryService _workingDir;
    private readonly IPlatformService _platform;

    public async Task<ToolExecutionResult> ExecuteAsync(string argumentsJson)
    {
        var path = _platform.NormalizePath(targetPath);
        // ... rest of implementation
    }
}
```

## Testes Cross-Platform

### 1. Testes Condicionais

```csharp
[Fact]
public void Test_Windows_PathNormalization()
{
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        return; // Skip on non-Windows
    }

    var platform = new PlatformService();
    var path = platform.NormalizePath("C:/Users/Test");
    Assert.Equal("C:\\Users\\Test", path);
}

[Fact]
public void Test_Linux_PathNormalization()
{
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        return; // Skip on non-Linux
    }

    var platform = new PlatformService();
    var path = platform.NormalizePath("/home/user/test");
    Assert.Equal("/home/user/test", path);
}
```

### 2. Testes com Atributos

```csharp
public class WindowsOnlyFactAttribute : FactAttribute
{
    public WindowsOnlyFactAttribute()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Skip = "Windows only test";
        }
    }
}

[WindowsOnlyFact]
public void Test_PowerShell_Command()
{
    // Test PowerShell specific functionality
}
```

## Integração com Program.cs

```csharp
// Registrar serviços cross-platform
services.AddSingleton<IPlatformService, PlatformService>();
services.AddSingleton<ICommandAdapter, CommandAdapter>();
services.AddSingleton<IShellExecutor, ShellExecutor>();

// Atualizar tools existentes
services.AddSingleton<ITool>(sp =>
    new CodeExecutionTool(sp.GetRequiredService<IPlatformService>()));

// Novos tools já recebem o serviço
services.AddSingleton<ITool, ChangeDirectoryTool>();
services.AddSingleton<ITool, EditFileTool>();
services.AddSingleton<ITool, SearchTool>();
```

## Logging e Debugging

```csharp
var platform = serviceProvider.GetRequiredService<IPlatformService>();
Console.WriteLine($"Platform: {platform.Platform}");
Console.WriteLine($"Shell: {platform.ShellType}");
Console.WriteLine($"Path Separator: {platform.PathSeparator}");
Console.WriteLine($"Home Directory: {platform.HomeDirectory}");
```

## Checklist de Implementação

- [ ] Criar `IPlatformService.cs`
- [ ] Criar `PlatformService.cs`
- [ ] Implementar detecção de plataforma
- [ ] Implementar normalização de paths
- [ ] Implementar `CreateShellProcess()`
- [ ] Criar `ICommandAdapter.cs`
- [ ] Criar `CommandAdapter.cs`
- [ ] Implementar comandos equivalentes para todas as operações
- [ ] Criar `IShellExecutor.cs`
- [ ] Criar `ShellExecutor.cs`
- [ ] Implementar execução com timeout
- [ ] Criar `LineEndingDetector` helper
- [ ] Criar `FileSystemHelper` helper
- [ ] Atualizar `CodeExecutionTool` para usar `PlatformService`
- [ ] Atualizar todos os novos tools para usar `PlatformService`
- [ ] Configurar encoding UTF-8 em `Program.cs`
- [ ] Registrar todos os serviços em `Program.cs`
- [ ] Criar testes cross-platform
- [ ] Testar em Windows 10/11
- [ ] Testar em Ubuntu/Debian
- [ ] Testar em macOS (se possível)
- [ ] Documentar diferenças entre plataformas
- [ ] Adicionar seção no README sobre compatibilidade

## Problemas Conhecidos e Soluções

### 1. PowerShell Execution Policy
**Problema**: PowerShell pode bloquear execução de scripts.
**Solução**: Usar `-NoProfile` e `-ExecutionPolicy Bypass` se necessário.

### 2. Python Command
**Problema**: Windows usa `python`, Linux usa `python3`.
**Solução**: Detectar automaticamente qual está disponível.

### 3. Ripgrep Não Instalado
**Problema**: `rg` pode não estar instalado.
**Solução**: Fallback para `grep` (Linux/macOS) ou `Select-String` (Windows).

### 4. Console Encoding
**Problema**: Caracteres especiais podem aparecer incorretos.
**Solução**: Forçar UTF-8 no início da aplicação.

### 5. Path com Espaços
**Problema**: Paths com espaços quebram comandos.
**Solução**: Sempre usar aspas ao construir comandos shell.

## Melhorias Futuras

1. **Auto-install Tools**: Detectar e oferecer instalação de ferramentas faltantes
2. **WSL Support**: Detectar e usar WSL (Windows Subsystem for Linux) se disponível
3. **Container Detection**: Detectar se está rodando em Docker/container
4. **CI/CD Integration**: Detectar ambientes de CI (GitHub Actions, etc.)
5. **Performance Metrics**: Medir diferenças de performance entre plataformas
