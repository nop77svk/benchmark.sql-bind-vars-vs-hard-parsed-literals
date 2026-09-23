namespace BenchmarkBindVarsAndHardcodesInOracle;

using System;
using System.Data;
using System.Text.RegularExpressions;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Oracle.ManagedDataAccess.Client;
using Testcontainers.Oracle;

internal class BenchmarkDatabaseContainerWrapper : IAsyncDisposable
{
    private const string OracleDockerImageUri = @"container-registry.oracle.com/database/free:latest";
    private const string OracleDockerDatabaseCharset = @"AL32UTF8";

    private static readonly char[] _oracleDockerDatabasePasswordChars = Enumerable.Range(33, 93)
        .Select(x => (char)(byte)x)
        .Where(char.IsAsciiLetterOrDigit)
        .ToArray();
    private static readonly string _oracleDockerDatabaseSysPassword = Random.Shared.GetString(_oracleDockerDatabasePasswordChars, 32);

    private static readonly TimeSpan _logsRegexpParsingTimeOut = TimeSpan.FromSeconds(5);
    private static readonly Regex _rxDatabaseIsReadyToUse = new Regex(@"^\s*DATABASE\s+IS\s+READY\s+TO\s+USE\s*!\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline, _logsRegexpParsingTimeOut);
    private static readonly Regex _rxCustomScriptsExecutionStarted = new Regex(@"^\s*Executing\s+user\s+defined\s+scripts\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline, _logsRegexpParsingTimeOut);
    private static readonly Regex _rxCustomScriptsExecutionFinished = new Regex(@"^\s*DONE:\s*Executing\s+user\s+defined\s+scripts\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline, _logsRegexpParsingTimeOut);
    private static readonly Regex _rxLoggedExceptions = new Regex(@"^(\d{1,4}-\d{1,2}-\d{1,2}T\d{1,2}:\d{1,2}:\d{1,2}(\.\d+)?\S+\s*)?(ORA|TNS|SP2)-\d+\s*:", RegexOptions.Compiled | RegexOptions.Multiline, _logsRegexpParsingTimeOut);

    private bool _disposedValue;

    public OracleContainer Container { get; }

    public static async Task<BenchmarkDatabaseContainerWrapper> CreateAsync(CancellationToken cancellationToken)
    {
        BenchmarkDatabaseContainerWrapper result = new BenchmarkDatabaseContainerWrapper();
        await result.Container.StartAsync(cancellationToken);
        await EnsureNoErrorsInContainerLogs(result.Container, cancellationToken);
        return result;
    }

    private BenchmarkDatabaseContainerWrapper()
    {
        OracleBuilder builder = new OracleBuilder(OracleDockerImageUri);

        Container = builder
            .WithAutoRemove(true)
            .WithCleanUp(true)
            .WithName("testcontainer")
            .WithPortBinding(1521, assignRandomHostPort: true)
            .WithEnvironment(@"ORACLE_PWD", _oracleDockerDatabaseSysPassword)
            .WithEnvironment(@"ORACLE_CHARACTERSET", OracleDockerDatabaseCharset)
            .WithEnvironment(@"ENABLE_ARCHIVELOG", @"false")
            .WithEnvironment(@"ENABLE_FORCE_LOGGING", @"false")
            .WithResourceMapping(Path.Combine(AppContext.BaseDirectory, @"DatabaseStartup"), @"/opt/oracle/scripts/startup")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilMessageIsLogged(_rxDatabaseIsReadyToUse)
                .UntilMessageIsLogged(_rxCustomScriptsExecutionStarted)
                .UntilMessageIsLogged(_rxCustomScriptsExecutionFinished)
            )
            .Build();
    }

    public async ValueTask DisposeAsync()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        await DisposeAsync(disposing: true);
        GC.SuppressFinalize(this);
    }

    public async Task SeedTheTestDataAsync(CancellationToken cancellationToken)
    {
        OracleConnectionStringBuilder connectionStringBuilder = new OracleConnectionStringBuilder();
        connectionStringBuilder.DataSource = $"(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=127.0.0.1)(PORT={Container.GetMappedPublicPort()}))(CONNECT_DATA=(SERVICE_NAME=freepdb1)))";
        connectionStringBuilder.UserID = "SYSTEM";
        connectionStringBuilder.Password = _oracleDockerDatabaseSysPassword;
        string connectionString = connectionStringBuilder.ConnectionString;

        using OracleConnection connection = new OracleConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using OracleTransaction transaction = (OracleTransaction)await connection.BeginTransactionAsync(cancellationToken);

        string pathToScripts = Path.Combine(AppContext.BaseDirectory, @"DatabaseSeed");
        foreach (var scriptName in Directory.EnumerateFiles(pathToScripts, "*.sql"))
        {
            string scriptContents = await File.ReadAllTextAsync(scriptName, cancellationToken);

            using OracleCommand command = connection.CreateCommand();
            command.CommandText = scriptContents;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 0;
            command.Transaction = transaction;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        await connection.CloseAsync();
    }

    protected virtual async ValueTask DisposeAsync(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                await Container.StopAsync();
                await Container.DisposeAsync();
            }

            _disposedValue = true;
        }
    }

    private static async Task EnsureNoErrorsInContainerLogs(IContainer container, CancellationToken cancellationToken)
    {
        (string containerStdOut, string containerStdErr) = await container.GetLogsAsync(ct: cancellationToken);
        if (_rxLoggedExceptions.IsMatch(containerStdOut) || _rxLoggedExceptions.IsMatch(containerStdErr))
        {
            throw new ContainerNotRunningException(container.Id, containerStdOut, containerStdErr, 0, null);
        }
    }
}
