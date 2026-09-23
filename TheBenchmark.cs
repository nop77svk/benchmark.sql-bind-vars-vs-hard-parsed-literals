namespace BenchmarkBindVarsAndHardcodesInOracle;
#pragma warning disable S112

using System.Data;
using BenchmarkDotNet.Attributes;
using Microsoft.Diagnostics.Utilities;
using Oracle.ManagedDataAccess.Client;

public class TheBenchmark
    : IAsyncDisposable
{
    private readonly Lock lastIdLock = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private OracleConnection? _connection;

    private int _lastId = -1;
    private int _seededRows = -1;

    public static async Task<int> SeedTheTestDataAsync(OracleConnection benchmarkUserConnection, int rowsToSeed, CancellationToken cancellationToken)
    {
        int result = 0;
        await using OracleTransaction transaction = (OracleTransaction)await benchmarkUserConnection.BeginTransactionAsync(cancellationToken);

        using (OracleCommand truncateTable = benchmarkUserConnection.CreateCommand())
        {
            truncateTable.CommandText = "truncate table t_test_data";
            truncateTable.CommandType = CommandType.Text;
            truncateTable.CommandTimeout = 0;
            truncateTable.Transaction = transaction;
            await truncateTable.ExecuteNonQueryAsync(cancellationToken);
        }

        using (OracleCommand seedTheData = benchmarkUserConnection.CreateCommand())
        {
            seedTheData.CommandText = """
                -- note: Inserts a shitload of rows. Takes some time. Be patient!
                insert into t_test_data
                    by name
                with rows_square_rooted$ as (
                    select level as xx
                    from dual
                    connect by level <= ceil(sqrt(:total_rows))
                ),
                rows$ as (
                    select (A.xx - 1) * ceil(sqrt(:total_rows)) + B.xx as id
                    from rows_square_rooted$ A
                        cross join rows_square_rooted$ B
                )
                select *
                from rows$
                where id <= :total_rows
                """;

            seedTheData.CommandType = CommandType.Text;
            seedTheData.CommandTimeout = 0;
            seedTheData.Transaction = transaction;

            OracleParameter totalRowsParam = seedTheData.Parameters.Add("total_rows", OracleDbType.Int32);
            totalRowsParam.Value = rowsToSeed;

            result = await seedTheData.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        Console.WriteLine("*** Global setup");
        var connectionStringBuilder = new OracleConnectionStringBuilder()
        {
            DataSource = BenchmarkDatabaseContainerInitializer.GetBasicDatabaseConnectionDataSource(
                hostAddress: "127.0.0.1",
                servicePort: BenchmarkConfig.ContainerHostPort,
                serviceName: "freepdb1"
            ),
            UserID = BenchmarkConfig.BenchmarkDbUser,
            Password = BenchmarkConfig.BenchmarkDbPw
        };
        _connection = new OracleConnection(connectionStringBuilder.ConnectionString);
        Console.WriteLine($"*** Connection string = {_connection.ConnectionString}");
        await _connection.OpenAsync(_cancellationTokenSource.Token);

        _seededRows = BenchmarkConfig.RowsToSeed;
        _lastId = -1;
    }

    [GlobalCleanup]
    public async Task GlobalCleanup()
    {
        Console.WriteLine("*** Global cleanup");

        if (_connection != null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }

        Console.WriteLine($"*** last id used = {_lastId}");
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }

        if (_cancellationTokenSource != null)
        {
            _cancellationTokenSource.Dispose();
        }
    }

#pragma warning disable S2077
    [Benchmark]
    public async Task AdoNetWithHardCodedLiterals()
    {
        ArgumentNullException.ThrowIfNull(_connection);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_seededRows, 0);

        _lastId = (_lastId + 1) % _seededRows;

        using OracleCommand command = _connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = $"""
            select id
            from t_test_data
            where id = {_lastId + 1}
            """;

        int? fetchedId = (int?)(decimal?)await command.ExecuteScalarAsync(_cancellationTokenSource.Token);

        if (fetchedId != _lastId + 1)
        {
            throw new Exception($"Fetched id {fetchedId} != last id {_lastId} + 1");
        }
    }
#pragma warning restore S2077

    [Benchmark]
    public async Task AdoNetWithBindVariables()
    {
        ArgumentNullException.ThrowIfNull(_connection);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_seededRows, 0);

        _lastId = (_lastId + 1) % _seededRows;

        using OracleCommand command = _connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = """
            select id
            from t_test_data
            where id = :id
            """;

        command.Parameters.Add("id", OracleDbType.Int32, _lastId + 1, ParameterDirection.Input);
        int? fetchedId = (int?)(decimal?)await command.ExecuteScalarAsync(_cancellationTokenSource.Token);

        if (fetchedId != _lastId + 1)
        {
            throw new Exception($"Fetched id {fetchedId} != last id {_lastId} + 1");
        }
    }
}
