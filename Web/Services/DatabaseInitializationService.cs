using MySqlConnector;

namespace Web.Services;

/// <summary>
/// 数据库初始化服务
/// </summary>
public class DatabaseInitializationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseInitializationService> _logger;

    public DatabaseInitializationService(
        IConfiguration configuration,
        ILogger<DatabaseInitializationService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// 初始化数据库
    /// </summary>
    public async Task InitializeAsync()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString))
        {
            _logger.LogError("数据库连接字符串未配置");
            throw new InvalidOperationException("数据库连接字符串未配置");
        }

        // 解析连接字符串获取数据库名
        var builder = new MySqlConnectionStringBuilder(connectionString);
        var databaseName = builder.Database;
        var serverConnectionString = connectionString.Replace($"Database={databaseName};", "");

        _logger.LogInformation("开始初始化数据库: {DatabaseName}", databaseName);

        try
        {
            // 1. 检查并创建数据库
            await CreateDatabaseIfNotExistsAsync(serverConnectionString, databaseName);

            // 2. 执行SQL文件
            await ExecuteSqlFilesAsync(connectionString);

            _logger.LogInformation("数据库初始化完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "数据库初始化失败");
            throw;
        }
    }

    /// <summary>
    /// 检查并创建数据库
    /// </summary>
    private async Task CreateDatabaseIfNotExistsAsync(string serverConnectionString, string databaseName)
    {
        await using var connection = new MySqlConnection(serverConnectionString);
        await connection.OpenAsync();

        // 检查数据库是否存在
        var checkDbCommand = connection.CreateCommand();
        checkDbCommand.CommandText = $"SELECT SCHEMA_NAME FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = '{databaseName}'";
        
        var result = await checkDbCommand.ExecuteScalarAsync();
        
        if (result == null)
        {
            // 数据库不存在，创建它
            _logger.LogInformation("数据库 {DatabaseName} 不存在，正在创建...", databaseName);
            
            var createDbCommand = connection.CreateCommand();
            createDbCommand.CommandText = $@"
                CREATE DATABASE `{databaseName}` 
                CHARACTER SET utf8mb4 
                COLLATE utf8mb4_unicode_ci";
            
            await createDbCommand.ExecuteNonQueryAsync();
            
            _logger.LogInformation("数据库 {DatabaseName} 创建成功", databaseName);
        }
        else
        {
            _logger.LogInformation("数据库 {DatabaseName} 已存在", databaseName);
        }
    }

    /// <summary>
    /// 执行SQL目录下的所有SQL文件
    /// </summary>
    private async Task ExecuteSqlFilesAsync(string connectionString)
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "sql");
        
        if (!Directory.Exists(sqlDirectory))
        {
            _logger.LogWarning("SQL目录不存在: {SqlDirectory}", sqlDirectory);
            return;
        }

        var sqlFiles = Directory.GetFiles(sqlDirectory, "*.sql", SearchOption.AllDirectories)
                               .OrderBy(f => f)
                               .ToList();

        if (sqlFiles.Count == 0)
        {
            _logger.LogInformation("SQL目录中没有找到SQL文件");
            return;
        }

        _logger.LogInformation("找到 {Count} 个SQL文件，开始执行...", sqlFiles.Count);

        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();

        foreach (var sqlFile in sqlFiles)
        {
            try
            {
                var fileName = Path.GetFileName(sqlFile);
                _logger.LogInformation("正在执行SQL文件: {FileName}", fileName);

                var sqlContent = await File.ReadAllTextAsync(sqlFile);
                
                // 分割SQL语句（以分号分隔，但要处理存储过程等特殊情况）
                var sqlStatements = SplitSqlStatements(sqlContent);

                foreach (var sql in sqlStatements)
                {
                    if (string.IsNullOrWhiteSpace(sql))
                        continue;

                    var command = connection.CreateCommand();
                    command.CommandText = sql;
                    
                    try
                    {
                        await command.ExecuteNonQueryAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "执行SQL语句时出现警告（可能是表已存在等正常情况）: {Sql}", 
                            sql.Length > 100 ? sql.Substring(0, 100) + "..." : sql);
                    }
                }

                _logger.LogInformation("SQL文件执行成功: {FileName}", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行SQL文件失败: {SqlFile}", sqlFile);
                // 继续执行其他文件
            }
        }

        _logger.LogInformation("所有SQL文件执行完成");
    }

    /// <summary>
    /// 分割SQL语句
    /// </summary>
    private List<string> SplitSqlStatements(string sqlContent)
    {
        var statements = new List<string>();
        var lines = sqlContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var currentStatement = new System.Text.StringBuilder();
        var inDelimiter = false;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // 跳过注释行
            if (trimmedLine.StartsWith("--") || trimmedLine.StartsWith("#"))
                continue;

            // 检查是否是DELIMITER语句
            if (trimmedLine.StartsWith("DELIMITER", StringComparison.OrdinalIgnoreCase))
            {
                inDelimiter = !inDelimiter;
                continue;
            }

            currentStatement.AppendLine(line);

            // 如果不在DELIMITER块中，且行以分号结尾，则认为是一个完整的语句
            if (!inDelimiter && trimmedLine.EndsWith(";"))
            {
                var statement = currentStatement.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(statement))
                {
                    statements.Add(statement);
                }
                currentStatement.Clear();
            }
        }

        // 添加最后一个语句（如果有）
        var lastStatement = currentStatement.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(lastStatement))
        {
            statements.Add(lastStatement);
        }

        return statements;
    }
}

