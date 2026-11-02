namespace BenchmarkBindVarsAndHardcodesInOracle;

public static class GlobalContext
{
    public const string BenchmarkDbUser = "BENCHMARK";
    public const string BenchmarkDbPw = "BENCHMARK";
    public const string SysDbPw = "BENCHMARK";
    public const int ContainerHostPort = 1529;

    public const int RowsToSeed = 160_000;

    public static DatabaseContainerInitialiser? ContainerInitialiser { get; set; } = null;
}
