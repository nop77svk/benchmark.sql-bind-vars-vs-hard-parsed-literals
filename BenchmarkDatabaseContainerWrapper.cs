namespace BenchmarkBindVarsAndHardcodesInOracle;

using System;
using System.Text.RegularExpressions;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Testcontainers.Oracle;

internal class BenchmarkDatabaseContainerWrapper : IAsyncDisposable
{
    private const string OracleDockerImageUri = @"container-registry.oracle.com/database/free:latest";
    private const string OracleDockerSysPassword = @"abc";
    private const string OracleDockerDatabaseCharset = @"AL32UTF8";

    private static readonly Regex _rxDatabaseIsReadyToUse = new Regex(@"^\s*DATABASE\s+IS\s+READY\s+TO\s+USE\s*!\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline, TimeSpan.FromSeconds(5));
    private static readonly Regex _rxCustomScriptsExecutionStarted = new Regex(@"^\s*Executing\s+user\s+defined\s+scripts\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline, TimeSpan.FromSeconds(5));
    private static readonly Regex _rxCustomScriptsExecutionFinished = new Regex(@"^\s*DONE:\s*Executing\s+user\s+defined\s+scripts\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline, TimeSpan.FromSeconds(5));

    private static readonly Regex _rxLoggedExceptions = new Regex(@"^(\d{1,4}-\d{1,2}-\d{1,2}T\d{1,2}:\d{1,2}:\d{1,2}(\.\d+)?\S+\s*)?(ORA|TNS|SP2)-\d+\s*:", RegexOptions.Compiled | RegexOptions.Multiline);

    private bool _disposedValue;

    public OracleContainer Container { get; }

    public static async Task<BenchmarkDatabaseContainerWrapper> CreateAsync()
    {
        BenchmarkDatabaseContainerWrapper result = new BenchmarkDatabaseContainerWrapper();
        await result.Container.StartAsync();
        await EnsureNoErrorsInContainerLogs(result.Container);
        return result;
    }

    private BenchmarkDatabaseContainerWrapper()
    {
        OracleBuilder builder = new OracleBuilder(OracleDockerImageUri);

        Container = builder
            .WithAutoRemove(true)
            .WithCleanUp(true)
            .WithName("testcontainer")
            .WithPortBinding(1521)
            .WithEnvironment(@"ORACLE_PWD", OracleDockerSysPassword)
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

    private static async Task EnsureNoErrorsInContainerLogs(IContainer container)
    {
        (string containerStdOut, string containerStdErr) = await container.GetLogsAsync();
        if (_rxLoggedExceptions.IsMatch(containerStdOut) || _rxLoggedExceptions.IsMatch(containerStdErr))
        {
            throw new ContainerNotRunningException(container.Id, containerStdOut, containerStdErr, 0, null);
        }
    }
}
