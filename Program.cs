namespace BenchmarkBindVarsAndHardcodesInOracle;

internal static class Program
{
    private static readonly CancellationToken _cancellationToken = CancellationToken.None;

    private static async Task Main(string[] args)
    {
        Console.WriteLine("*** Starting the container DB");
        await using BenchmarkDatabaseContainerWrapper benchmarkDatabase = await BenchmarkDatabaseContainerWrapper.CreateAsync(_cancellationToken);

        Console.WriteLine("*** Seeding the container DB with test data");
        await benchmarkDatabase.SeedTheTestDataAsync(_cancellationToken);

        Console.WriteLine("*** Press \"any key\" to finish");
        Console.ReadKey();
    }
}
