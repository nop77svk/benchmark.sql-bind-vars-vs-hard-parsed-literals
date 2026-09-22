internal static class Program
{
    private const string OracleDockerImageUri = @"container-registry.oracle.com/database/free:latest";
    private const string OracleDockerSysPassword = @"abc";
    private const string OracleDockerDatabaseCharset = @"AL32UTF8";

    private static async Task Main(string[] args)
    {
        Console.WriteLine("Initialising");
        Testcontainers.Oracle.OracleBuilder builder = new Testcontainers.Oracle.OracleBuilder(OracleDockerImageUri);
        Testcontainers.Oracle.OracleContainer container = builder
            .WithAutoRemove(true)
            .WithCleanUp(true)
            .WithPortBinding(hostPort: 1529, containerPort: 1521)
            .WithEnvironment(@"ORACLE_PWD", OracleDockerSysPassword)
            .WithEnvironment(@"ORACLE_CHARACTERSET", OracleDockerDatabaseCharset)
            .WithEnvironment(@"ENABLE_ARCHIVELOG", @"false")
            .WithEnvironment(@"ENABLE_FORCE_LOGGING", @"false")
            .Build();

        try
        {
            Console.WriteLine("Starting Oracle DB");
            await container.StartAsync();

            Console.WriteLine("Press any key 😁 to finish");
            Console.ReadKey();
        }
        finally
        {
            await container.StopAsync();
        }
    }
}