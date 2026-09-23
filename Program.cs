namespace BenchmarkBindVarsAndHardcodesInOracle;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        await using BenchmarkDatabaseContainerWrapper benchmarkDatabase = await BenchmarkDatabaseContainerWrapper.CreateAsync();

        Console.WriteLine("Press \"any key\" to finish");
        Console.ReadKey();
    }
}
