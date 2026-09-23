namespace BenchmarkBindVarsAndHardcodesInOracle;

public static class BenchmarkConfig
{
    public const string BenchmarkDbUser = "BENCHMARK";
    public const string BenchmarkDbPw = "BENCHMARK";
    public const int ContainerHostPort = 1529;

    public const int RowsToSeed = 160_000;
}
