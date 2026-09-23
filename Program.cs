namespace BenchmarkBindVarsAndHardcodesInOracle;

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using Oracle.ManagedDataAccess.Client;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        CancellationToken cancellationToken = CancellationToken.None;

        Console.WriteLine("*** Initializing the test container");
        await using BenchmarkDatabaseContainerInitializer containerInitializer = await BenchmarkDatabaseContainerInitializer.CreateAsync(GlobalContext.ContainerHostPort, cancellationToken);

        Console.WriteLine("*** Seeding the test data");
        using (OracleConnection benchmarkConnection = containerInitializer.GetDatabaseConnection(GlobalContext.BenchmarkDbUser, GlobalContext.BenchmarkDbPw))
        {
            await benchmarkConnection.OpenAsync(cancellationToken);
            int rowsSeeded = await TheBenchmark.SeedTheTestDataAsync(benchmarkConnection, GlobalContext.RowsToSeed, cancellationToken);
            await benchmarkConnection.CloseAsync();

            ArgumentOutOfRangeException.ThrowIfNotEqual(rowsSeeded, GlobalContext.RowsToSeed);
        }

        Console.WriteLine("*** Executing the benchmark");
        int invocationCount = (int)Math.Max(16, Math.Pow(2, Math.Ceiling(Math.Log2(Math.Sqrt(GlobalContext.RowsToSeed * 2)))));
        IConfig benchmarkConfig = ManualConfig.CreateMinimumViable()
            .AddJob(Job.Default
                .WithMaxIterationCount(GlobalContext.RowsToSeed * 2 / invocationCount)
                .WithMinIterationCount(GlobalContext.RowsToSeed * 1 / invocationCount)
                .WithInvocationCount(invocationCount)
            );
        BenchmarkRunner.Run<TheBenchmark>(benchmarkConfig);

        Console.WriteLine("*** Waiting for a key presss...");
        Console.ReadKey();
    }
}
