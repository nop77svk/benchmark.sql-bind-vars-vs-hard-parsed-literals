namespace BenchmarkBindVarsAndHardcodesInOracle;
#pragma warning disable S112
#pragma warning disable SA1116

using System.Data;
using BenchmarkBindVarsAndHardcodesInOracle.Models;
using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;

public class TheBenchmark
    : IAsyncDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private OracleConnection? _connection;
    private AppDbContext? _dbContext;

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
                servicePort: GlobalContext.ContainerHostPort,
                serviceName: "freepdb1"
            ),
            UserID = GlobalContext.BenchmarkDbUser,
            Password = GlobalContext.BenchmarkDbPw
        };

        string connectionString = connectionStringBuilder.ConnectionString;
        Console.WriteLine($"*** Connection string = {connectionString}");

        _connection = new OracleConnection(connectionString);
        await _connection.OpenAsync(_cancellationTokenSource.Token);

        _dbContext = new AppDbContext()
        {
            ConnectionString = connectionString
        };

        _seededRows = GlobalContext.RowsToSeed;
        _lastId = -1;

        await FlushSharedPoolAsync(_cancellationTokenSource.Token);
    }

    [GlobalCleanup]
    public async Task GlobalCleanup()
    {
        Console.WriteLine("*** Global cleanup");

        await FlushSharedPoolAsync(_cancellationTokenSource.Token);

        if (_connection != null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }

        Console.WriteLine($"*** last id used = {_lastId}");
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }

        if (_cancellationTokenSource != null)
        {
            _cancellationTokenSource.Dispose();
        }
    }

    [Benchmark]
    public async ValueTask AdoNetEmptyBenchmark()
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
        int? fetchedId = _lastId + 1; // note: This is the place where normally the query execution would take place.

        if (fetchedId != _lastId + 1)
        {
            throw new Exception($"Fetched id {fetchedId} != last id {_lastId} + 1");
        }
    }

#pragma warning disable S2077
    [Benchmark]
    public async ValueTask AdoNetWithHardCodedLiterals()
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
    public async ValueTask AdoNetWithBindVariables()
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

    [Benchmark]
    public async ValueTask EntityFrameworkCoreWithLinq()
    {
        ArgumentNullException.ThrowIfNull(_dbContext);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_seededRows, 0);

        _lastId = (_lastId + 1) % _seededRows;

        int? fetchedId = await _dbContext.TestData
            .AsNoTracking()
            .Where(x => x.Id == _lastId + 1)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(_cancellationTokenSource.Token);

        if (fetchedId != _lastId + 1)
        {
            throw new Exception($"Fetched id {fetchedId} != last id {_lastId} + 1");
        }
    }

    [Benchmark]
    public async ValueTask EntityFrameworkCoreFromSql()
    {
        ArgumentNullException.ThrowIfNull(_dbContext);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_seededRows, 0);

        _lastId = (_lastId + 1) % _seededRows;

        int? fetchedId = await _dbContext.TestData
            .FromSql($"""
                select id
                from t_test_data
                where id = {_lastId} + 1
                """)
            .AsNoTracking()
            .Select(x => x.Id)
            .FirstOrDefaultAsync(_cancellationTokenSource.Token);

        if (fetchedId != _lastId + 1)
        {
            throw new Exception($"Fetched id {fetchedId} != last id {_lastId} + 1");
        }
    }

    [Benchmark]
    public async ValueTask EntityFrameworkCoreFromSqlInterpolated()
    {
        ArgumentNullException.ThrowIfNull(_dbContext);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_seededRows, 0);

        _lastId = (_lastId + 1) % _seededRows;

        int? fetchedId = await _dbContext.TestData
            .FromSqlInterpolated($"""
                select id
                from t_test_data
                where id = {_lastId} + 1
                """)
            .AsNoTracking()
            .Select(x => x.Id)
            .FirstOrDefaultAsync(_cancellationTokenSource.Token);

        if (fetchedId != _lastId + 1)
        {
            throw new Exception($"Fetched id {fetchedId} != last id {_lastId} + 1");
        }
    }

    [Benchmark]
    public async ValueTask EntityFrameworkCoreFromSqlRaw()
    {
        ArgumentNullException.ThrowIfNull(_dbContext);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_seededRows, 0);

        _lastId = (_lastId + 1) % _seededRows;

        int? fetchedId = await _dbContext.TestData
            .FromSqlRaw("""
                select id
                from t_test_data
                where id = {0}
                """,
                _lastId + 1
            )
            .AsNoTracking()
            .Select(x => x.Id)
            .FirstOrDefaultAsync(_cancellationTokenSource.Token);

        if (fetchedId != _lastId + 1)
        {
            throw new Exception($"Fetched id {fetchedId} != last id {_lastId} + 1");
        }
    }

    [Benchmark]
    public async ValueTask EntityFrameworkCoreFromSqlRawHardCoded()
    {
        ArgumentNullException.ThrowIfNull(_dbContext);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_seededRows, 0);

        _lastId = (_lastId + 1) % _seededRows;

        int? fetchedId = await _dbContext.TestData
            .FromSqlRaw($"""
                select id
                from t_test_data
                where id = {_lastId + 1}
                """
            )
            .AsNoTracking()
            .Select(x => x.Id)
            .FirstOrDefaultAsync(_cancellationTokenSource.Token);

        if (fetchedId != _lastId + 1)
        {
            throw new Exception($"Fetched id {fetchedId} != last id {_lastId} + 1");
        }
    }

    private static async Task FlushSharedPoolAsync(CancellationToken cancellationToken)
    {
        await using OracleConnection sysConnection = new OracleConnection(GlobalContext.SysDbaConnectionString);
        await sysConnection.OpenAsync(cancellationToken);

        await using OracleCommand flushSharedPool = sysConnection.CreateCommand();
        flushSharedPool.CommandText = "alter system flush shared_pool";
        flushSharedPool.CommandType = CommandType.Text;

        await flushSharedPool.ExecuteNonQueryAsync(cancellationToken);

        await sysConnection.CloseAsync();
    }
}
