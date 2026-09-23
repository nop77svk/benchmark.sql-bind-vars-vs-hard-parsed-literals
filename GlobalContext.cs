namespace BenchmarkBindVarsAndHardcodesInOracle;

using Oracle.ManagedDataAccess.Client;

public static class GlobalContext
{
    public const string BenchmarkDbUser = "BENCHMARK";
    public const string BenchmarkDbPw = "BENCHMARK";
    public const string SysDbPw = "BENCHMARK";
    public const int ContainerHostPort = 1529;

    public const int RowsToSeed = 160_000;

    public static string BasicDatabaseConnectionDataSource
        => BenchmarkDatabaseContainerInitializer.GetBasicDatabaseConnectionDataSource("127.0.0.1", ContainerHostPort, "freepdb1");

    public static string SysDbaConnectionString
        => new OracleConnectionStringBuilder()
        {
            DataSource = BasicDatabaseConnectionDataSource,
            UserID = "SYS",
            Password = SysDbPw,
            DBAPrivilege = "SYSDBA"
        }.ConnectionString;

    public static string BenchMarkUserConnectionString
        => new OracleConnectionStringBuilder()
        {
            DataSource = BasicDatabaseConnectionDataSource,
            UserID = BenchmarkDbUser,
            Password = BenchmarkDbPw
        }.ConnectionString;
}
