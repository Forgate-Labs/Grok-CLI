# Planejamento: EditFileTool

## Objetivo
Implementar um tool call que permite fazer mudanças em arquivos de texto de forma segura e eficiente, com suporte a múltiplas operações de edição.

## Requisitos

### Funcionais
- Ler conteúdo de arquivo de texto
- Substituir texto específico por novo texto
- Inserir texto em posição específica (linha)
- Anexar texto ao final do arquivo
- Deletar linhas específicas
- Suportar encoding UTF-8
- Criar backup antes de editar (opcional)
- Validar se arquivo existe antes de editar
- Respeitar o diretório de trabalho atual (integração com `IWorkingDirectoryService`)

### Não Funcionais
- Atomic operations: edições devem ser atômicas (sucesso total ou rollback)
- Segurança: não permitir edição de arquivos fora do projeto (opcional sandbox)
- Performance: otimizado para arquivos de texto de tamanho médio (< 10 MB)
- Preservar line endings originais (CRLF vs LF)

## Arquitetura

### 1. Classe EditFileTool
**Localização**: `/GrokCLI/GrokCLI.tui/Tools/EditFileTool.cs`

**Implementação**:
```csharp
public class EditFileTool : ITool
{
    private readonly IWorkingDirectoryService _workingDirService;
    private readonly IFileEditService _fileEditService;

    public string Name => "edit_file";
    public string Description => "Edits text files with various operations (replace, insert, append, delete)";

    // Implementar GetChatTool() e ExecuteAsync()
}
```

**Schema JSON** para o tool:
```json
{
    "type": "object",
    "properties": {
        "file_path": {
            "type": "string",
            "description": "Path to the file to edit (relative or absolute)"
        },
        "operation": {
            "type": "string",
            "enum": ["replace", "insert", "append", "delete", "write"],
            "description": "The edit operation to perform"
        },
        "search_text": {
            "type": "string",
            "description": "Text to search for (required for 'replace' operation)"
        },
        "replacement_text": {
            "type": "string",
            "description": "Text to replace with (required for 'replace' operation)"
        },
        "content": {
            "type": "string",
            "description": "Content to insert/append/write (required for 'insert', 'append', 'write')"
        },
        "line_number": {
            "type": "integer",
            "description": "Line number for insert/delete operations (1-based index)"
        },
        "create_backup": {
            "type": "boolean",
            "description": "Create backup before editing (default: true)"
        }
    },
    "required": ["file_path", "operation"]
}
```

### 2. Serviço de Edição de Arquivos
**Localização**: `/GrokCLI/GrokCLI.tui/Services/IFileEditService.cs` e `FileEditService.cs`

**Interface**:
```csharp
public interface IFileEditService
{
    Task<FileEditResult> ReplaceTextAsync(string filePath, string searchText, string replacementText, bool createBackup = true);
    Task<FileEditResult> InsertTextAsync(string filePath, int lineNumber, string content, bool createBackup = true);
    Task<FileEditResult> AppendTextAsync(string filePath, string content, bool createBackup = true);
    Task<FileEditResult> DeleteLinesAsync(string filePath, int startLine, int endLine, bool createBackup = true);
    Task<FileEditResult> WriteFileAsync(string filePath, string content, bool createBackup = true);
    Task<string> ReadFileAsync(string filePath);
}
```

**Classe FileEditResult**:
```csharp
public class FileEditResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public string FilePath { get; set; }
    public int LinesModified { get; set; }
    public string? BackupPath { get; set; }
    public string? Error { get; set; }
}
```

### 3. Operações de Edição

#### 3.1 Replace Text
Substitui todas as ocorrências de um texto por outro.

```csharp
public async Task<FileEditResult> ReplaceTextAsync(
    string filePath,
    string searchText,
    string replacementText,
    bool createBackup = true)
{
    // 1. Ler arquivo completo
    var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);

    // 2. Criar backup se solicitado
    if (createBackup)
    {
        await CreateBackupAsync(filePath);
    }

    // 3. Contar ocorrências
    var occurrences = CountOccurrences(content, searchText);
    if (occurrences == 0)
    {
        return new FileEditResult
        {
            Success = false,
            Error = "Search text not found"
        };
    }

    // 4. Substituir
    var newContent = content.Replace(searchText, replacementText);

    // 5. Escrever atomicamente
    await File.WriteAllTextAsync(filePath, newContent, Encoding.UTF8);

    return new FileEditResult
    {
        Success = true,
        LinesModified = occurrences
    };
}
```

#### 3.2 Insert Text
Insere texto em uma linha específica.

```csharp
public async Task<FileEditResult> InsertTextAsync(
    string filePath,
    int lineNumber,
    string content,
    bool createBackup = true)
{
    // 1. Ler todas as linhas
    var lines = (await File.ReadAllLinesAsync(filePath, Encoding.UTF8)).ToList();

    // 2. Validar número de linha
    if (lineNumber < 1 || lineNumber > lines.Count + 1)
    {
        return new FileEditResult
        {
            Success = false,
            Error = $"Invalid line number: {lineNumber}"
        };
    }

    // 3. Criar backup
    if (createBackup)
    {
        await CreateBackupAsync(filePath);
    }

    // 4. Inserir (lineNumber é 1-based)
    lines.Insert(lineNumber - 1, content);

    // 5. Escrever
    await File.WriteAllLinesAsync(filePath, lines, Encoding.UTF8);

    return new FileEditResult { Success = true, LinesModified = 1 };
}
```

#### 3.3 Append Text
Adiciona texto ao final do arquivo.

```csharp
public async Task<FileEditResult> AppendTextAsync(
    string filePath,
    string content,
    bool createBackup = true)
{
    if (createBackup)
    {
        await CreateBackupAsync(filePath);
    }

    await File.AppendAllTextAsync(filePath, content, Encoding.UTF8);

    return new FileEditResult { Success = true };
}
```

#### 3.4 Delete Lines
Remove linhas específicas.

```csharp
public async Task<FileEditResult> DeleteLinesAsync(
    string filePath,
    int startLine,
    int endLine,
    bool createBackup = true)
{
    var lines = (await File.ReadAllLinesAsync(filePath, Encoding.UTF8)).ToList();

    // Validar
    if (startLine < 1 || endLine > lines.Count || startLine > endLine)
    {
        return new FileEditResult
        {
            Success = false,
            Error = "Invalid line range"
        };
    }

    if (createBackup)
    {
        await CreateBackupAsync(filePath);
    }

    // Remover (converter para 0-based)
    var count = endLine - startLine + 1;
    lines.RemoveRange(startLine - 1, count);

    await File.WriteAllLinesAsync(filePath, lines, Encoding.UTF8);

    return new FileEditResult { Success = true, LinesModified = count };
}
```

#### 3.5 Write File
Escreve conteúdo completo, substituindo arquivo existente.

```csharp
public async Task<FileEditResult> WriteFileAsync(
    string filePath,
    string content,
    bool createBackup = true)
{
    var fileExists = File.Exists(filePath);

    if (fileExists && createBackup)
    {
        await CreateBackupAsync(filePath);
    }

    await File.WriteAllTextAsync(filePath, content, Encoding.UTF8);

    return new FileEditResult
    {
        Success = true,
        Message = fileExists ? "File updated" : "File created"
    };
}
```

### 4. Sistema de Backup

```csharp
private async Task<string> CreateBackupAsync(string filePath)
{
    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
    var directory = Path.GetDirectoryName(filePath);
    var fileName = Path.GetFileName(filePath);
    var backupPath = Path.Combine(directory, $"{fileName}.backup_{timestamp}");

    await File.CopyAsync(filePath, backupPath);

    return backupPath;
}
```

## Estrutura de Resposta

### Sucesso
```json
{
    "success": true,
    "message": "File edited successfully",
    "file_path": "/home/user/project/test.txt",
    "lines_modified": 3,
    "backup_path": "/home/user/project/test.txt.backup_20250129_143022"
}
```

### Erro
```json
{
    "success": false,
    "error": "File not found: /home/user/project/missing.txt",
    "file_path": "/home/user/project/missing.txt"
}
```

## Fluxo de Execução

```
1. Grok chama edit_file com parâmetros
   ↓
2. EditFileTool recebe argumentsJson
   ↓
3. Parse JSON e valida parâmetros
   ↓
4. Resolver path com WorkingDirectoryService
   ↓
5. Verificar se arquivo existe (exceto para 'write' que pode criar)
   ↓
6. FileEditService executa operação específica:
   - ReplaceTextAsync
   - InsertTextAsync
   - AppendTextAsync
   - DeleteLinesAsync
   - WriteFileAsync
   ↓
7. Criar backup se solicitado
   ↓
8. Executar operação atomicamente
   ↓
9. Retornar resultado (sucesso ou erro)
```

## Casos de Uso

### 1. Substituir Texto
```json
{
    "file_path": "src/Program.cs",
    "operation": "replace",
    "search_text": "Console.WriteLine(\"Hello\");",
    "replacement_text": "Console.WriteLine(\"Hello World\");",
    "create_backup": true
}
```

### 2. Inserir Linha
```json
{
    "file_path": "README.md",
    "operation": "insert",
    "line_number": 5,
    "content": "## New Section",
    "create_backup": true
}
```

### 3. Adicionar ao Final
```json
{
    "file_path": "log.txt",
    "operation": "append",
    "content": "\n2025-01-29 14:30:00 - New log entry",
    "create_backup": false
}
```

### 4. Deletar Linhas
```json
{
    "file_path": "config.json",
    "operation": "delete",
    "line_number": 10,
    "create_backup": true
}
```

### 5. Escrever Arquivo Completo
```json
{
    "file_path": "new_file.txt",
    "operation": "write",
    "content": "This is the complete content\nSecond line\nThird line",
    "create_backup": false
}
```

## Considerações de Segurança

1. **Path Traversal**: Validar paths para evitar acesso a arquivos fora do escopo
2. **File Size Limits**: Limitar tamanho máximo de arquivo (ex: 10 MB)
3. **Binary Files**: Detectar e rejeitar arquivos binários
4. **Permissions**: Verificar permissões de escrita antes de tentar editar
5. **Atomic Operations**: Usar temp files e rename para garantir atomicidade

## Otimizações

1. **Large Files**: Para arquivos grandes, usar streams ao invés de carregar tudo em memória
2. **Backup Cleanup**: Implementar limpeza automática de backups antigos
3. **Diff Output**: Retornar preview das mudanças antes de aplicar
4. **Undo Stack**: Manter histórico de mudanças para desfazer

## Integração com Program.cs

```csharp
services.AddSingleton<IFileEditService, FileEditService>();
services.AddSingleton<ITool, EditFileTool>();
```

## Cross-Platform Considerations

1. **Line Endings**:
   - Windows: `\r\n` (CRLF)
   - Linux/macOS: `\n` (LF)
   - Solução: Detectar e preservar line ending original

2. **Encoding**:
   - UTF-8 como padrão
   - Detectar BOM (Byte Order Mark) se presente
   - Preservar encoding original

3. **File Paths**:
   - Usar `Path.Combine()` para construir paths
   - Normalizar separadores com `Path.GetFullPath()`

## Melhorias Futuras

1. **Pattern Matching**: Suportar regex para replace
2. **Multi-file Edit**: Editar múltiplos arquivos em uma operação
3. **Preview Mode**: Mostrar preview antes de aplicar mudanças
4. **Patch Format**: Suportar formato de patch unificado
5. **Git Integration**: Auto-commit mudanças (opcional)
6. **Diff View**: Mostrar diff visual das mudanças

## Checklist de Implementação

- [ ] Criar `IFileEditService.cs`
- [ ] Criar `FileEditService.cs`
- [ ] Implementar `ReplaceTextAsync()`
- [ ] Implementar `InsertTextAsync()`
- [ ] Implementar `AppendTextAsync()`
- [ ] Implementar `DeleteLinesAsync()`
- [ ] Implementar `WriteFileAsync()`
- [ ] Implementar sistema de backup
- [ ] Implementar detecção de encoding
- [ ] Implementar preservação de line endings
- [ ] Criar `EditFileTool.cs`
- [ ] Implementar `GetChatTool()` com schema completo
- [ ] Implementar `ExecuteAsync()` com todas as operações
- [ ] Adicionar validação de file size
- [ ] Adicionar detecção de binary files
- [ ] Registrar serviço e tool em `Program.cs`
- [ ] Criar testes unitários para cada operação
- [ ] Testar com diferentes encodings
- [ ] Testar com diferentes line endings
- [ ] Documentar uso no README.md
