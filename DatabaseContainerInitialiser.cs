namespace BenchmarkBindVarsAndHardcodesInOracle;

using System;
using System.Data;
using System.Text.RegularExpressions;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Oracle.ManagedDataAccess.Client;
using Testcontainers.Oracle;

public class DatabaseContainerInitialiser : IAsyncDisposable
{
    private const string OracleDockerImageUri = @"container-registry.oracle.com/database/free:latest";
    private const string OracleDockerDatabaseCharset = @"AL32UTF8";

    private static readonly TimeSpan _logsRegexpParsingTimeOut = TimeSpan.FromSeconds(5);
    private static readonly Regex _rxDatabaseIsReadyToUse = new Regex(@"^\s*DATABASE\s+IS\s+READY\s+TO\s+USE\s*!\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline, _logsRegexpParsingTimeOut);
    private static readonly Regex _rxCustomScriptsExecutionStarted = new Regex(@"^\s*Executing\s+user\s+defined\s+scripts\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline, _logsRegexpParsingTimeOut);
    private static readonly Regex _rxCustomScriptsExecutionFinished = new Regex(@"^\s*DONE:\s*Executing\s+user\s+defined\s+scripts\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline, _logsRegexpParsingTimeOut);
    private static readonly Regex _rxLoggedExceptions = new Regex(@"^(\d{1,4}-\d{1,2}-\d{1,2}T\d{1,2}:\d{1,2}:\d{1,2}(\.\d+)?\S+\s*)?(ORA|TNS|SP2)-\d+\s*:", RegexOptions.Compiled | RegexOptions.Multiline, _logsRegexpParsingTimeOut);

    private bool _disposedValue;

    public OracleContainer Container { get; }

    public static async Task<DatabaseContainerInitialiser> CreateAsync(int hostPort, CancellationToken cancellationToken)
    {
        DatabaseContainerInitialiser result = new DatabaseContainerInitialiser(hostPort);
        await result.Container.StartAsync(cancellationToken);
        await EnsureNoErrorsInContainerLogs(result.Container, cancellationToken);
        return result;
    }

    public static string GetBasicDatabaseConnectionDataSource(string hostAddress, int servicePort, string serviceName, string protocol = "TCP")
        => $"(DESCRIPTION=(ADDRESS=(PROTOCOL={protocol})(HOST={hostAddress})(PORT={servicePort}))(CONNECT_DATA=(SERVICE_NAME={serviceName})))";

    private DatabaseContainerInitialiser(int? hostPort = null)
    {
        OracleBuilder builder = new OracleBuilder(OracleDockerImageUri)
            .WithAutoRemove(true)
            .WithCleanUp(true)
            .WithEnvironment(@"ORACLE_PWD", GlobalContext.SysDbPw)
            .WithEnvironment(@"ORACLE_CHARACTERSET", OracleDockerDatabaseCharset)
            .WithEnvironment(@"ENABLE_ARCHIVELOG", @"false")
            .WithEnvironment(@"ENABLE_FORCE_LOGGING", @"false")
            .WithResourceMapping(Path.Combine(AppContext.BaseDirectory, @"DatabaseStartup"), @"/opt/oracle/scripts/startup")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilMessageIsLogged(_rxDatabaseIsReadyToUse)
                .UntilMessageIsLogged(_rxCustomScriptsExecutionStarted)
                .UntilMessageIsLogged(_rxCustomScriptsExecutionFinished)
            );

        if (hostPort == null)
        {
            builder = builder.WithPortBinding(port: 1521, assignRandomHostPort: true);
        }
        else
        {
            builder = builder.WithPortBinding(hostPort: hostPort ?? 1521, containerPort: 1521);
        }

        Container = builder.Build();
    }

    public async ValueTask DisposeAsync()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        await DisposeAsync(disposing: true);
        GC.SuppressFinalize(this);
    }

    public OracleConnection GetDatabaseConnection(string userName, string password)
    {
        OracleConnectionStringBuilder connectionStringBuilder = new OracleConnectionStringBuilder();
        connectionStringBuilder.DataSource = GetBasicDatabaseConnectionDataSource(
            hostAddress: "127.0.0.1",
            servicePort: Container.GetMappedPublicPort(),
            serviceName: "freepdb1"
        );
        connectionStringBuilder.UserID = userName;
        connectionStringBuilder.Password = password;
        string connectionString = connectionStringBuilder.ConnectionString;
        Console.WriteLine($"Connection string = {connectionString}");

        OracleConnection result = new OracleConnection(connectionString)
        {
            AutoCommit = false
        };

        return result;
    }

    public OracleConnection GetSystemUserDatabaseConnection()
        => GetDatabaseConnection("SYSTEM", GlobalContext.SysDbPw);

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
