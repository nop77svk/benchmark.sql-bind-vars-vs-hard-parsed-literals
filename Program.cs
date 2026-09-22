namespace BenchmarkBindVarsAndHardcodesInOracle;

using System.Data.Common;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Columns;
using Docker.DotNet.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class Program
{
    private const string OracleDockerImageUri = @"container-registry.oracle.com/database/free:latest";
    private const string OracleDockerSysPassword = @"abc";
    private const string OracleDockerDatabaseCharset = @"AL32UTF8";

    private static readonly Regex _rxDatabaseIsReadyToUse = new Regex(@"^\s*DATABASE\s+IS\s+READY\s+TO\s+USE\s*!\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline, TimeSpan.FromSeconds(5));
    private static readonly Regex _rxCustomScriptsExecutionStarted = new Regex(@"^\s*Executing\s+user\s+defined\s+scripts\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline, TimeSpan.FromSeconds(5));
    private static readonly Regex _rxCustomScriptsExecutionFinished = new Regex(@"^\s*DONE:\s*Executing\s+user\s+defined\s+scripts\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline, TimeSpan.FromSeconds(5));

    private static readonly Regex _rxLoggedExceptions = new Regex(@"^(\d{1,4}-\d{1,2}-\d{1,2}T\d{1,2}:\d{1,2}:\d{1,2}(\.\d+)?\S+\s*)?(ORA|TNS|SP2)-\d+\s*:", RegexOptions.Compiled | RegexOptions.Multiline);

    private static async Task Main(string[] args)
    {
        Console.WriteLine("Initialising");
        Testcontainers.Oracle.OracleBuilder builder = new Testcontainers.Oracle.OracleBuilder(OracleDockerImageUri);
        Testcontainers.Oracle.OracleContainer container = builder
            .WithAutoRemove(true)
            .WithCleanUp(true)
            .WithName("testcontainer")
            .WithPortBinding(1521)
            .WithEnvironment(@"ORACLE_PWD", OracleDockerSysPassword)
            .WithEnvironment(@"ORACLE_CHARACTERSET", OracleDockerDatabaseCharset)
            .WithEnvironment(@"ENABLE_ARCHIVELOG", @"false")
            .WithEnvironment(@"ENABLE_FORCE_LOGGING", @"false")
            .WithResourceMapping(Path.Combine(AppContext.BaseDirectory, @"OracleStartup"), @"/opt/oracle/scripts/startup")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilMessageIsLogged(_rxDatabaseIsReadyToUse)
                .UntilMessageIsLogged(_rxCustomScriptsExecutionStarted)
                .UntilMessageIsLogged(_rxCustomScriptsExecutionFinished)
            )
            .Build();

        try
        {
            Console.WriteLine("Starting Oracle DB");
            await container.StartAsync();
            await container.EnsureNoErrorsInContainerLogs();

            Console.WriteLine("Press \"any key\" to finish");
            Console.ReadKey();
        }
        finally
        {
            await container.StopAsync();
        }
    }

    private static async Task EnsureNoErrorsInContainerLogs(this IContainer container)
    {
        (string containerStdOut, string containerStdErr) = await container.GetLogsAsync();
        if (_rxLoggedExceptions.IsMatch(containerStdOut) || _rxLoggedExceptions.IsMatch(containerStdErr))
        {
            throw new ContainerNotRunningException(container.Id, containerStdOut, containerStdErr, 0, null);
        }
    }
}
