namespace realtime_api_dotnet.Services
{
    using Microsoft.Data.SqlClient;

    public class StatsImportService
    {
        private readonly string _connectionString;
        private readonly ILogger<StatsImportService> _logger;

        public StatsImportService(IConfiguration configuration, ILogger<StatsImportService> logger)
        {
            _connectionString = configuration.GetConnectionString("NBAStatsDb");
            _logger = logger;
        }

        public async Task ImportCsvToDatabase(string filePath)
        {
            try
            {
                // Read CSV file
                var lines = await File.ReadAllLinesAsync(filePath);
                var headers = lines[0].Split(',');

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Create a table if it doesn't exist
                var createTableQuery = @"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Players')
                BEGIN
                    CREATE TABLE Players (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        Name NVARCHAR(100),
                        Team NVARCHAR(50),
                        Games INT,
                        GamesStarted INT,
                        PointsPerGame FLOAT,
                        ReboundsPerGame FLOAT,
                        AssistsPerGame FLOAT,
                        MinutesPerGame FLOAT,
                        FieldGoalPct FLOAT,
                        FreeThrowPct FLOAT,
                        ThreePointPct FLOAT,
                        StealsPerGame FLOAT,
                        TurnoversPerGame FLOAT,
                        BlocksPerGame FLOAT,
                        PlusMinus INT,
                        DoubleDoubles INT,
                        TripleDoubles INT
                    )
                END";

                using (var cmd = new SqlCommand(createTableQuery, connection))
                {
                    await cmd.ExecuteNonQueryAsync();
                }

                // Clear existing data
                using (var cmd = new SqlCommand("DELETE FROM Players", connection))
                {
                    await cmd.ExecuteNonQueryAsync();
                }

                // Insert data
                for (int i = 1; i < lines.Length; i++)
                {
                    var values = lines[i].Split(',');
                    var insertQuery = @"
                    INSERT INTO Players (Name, Team, Games, GamesStarted, PointsPerGame, ReboundsPerGame, 
                    AssistsPerGame, MinutesPerGame, FieldGoalPct, FreeThrowPct, ThreePointPct, 
                    StealsPerGame, TurnoversPerGame, BlocksPerGame, PlusMinus, DoubleDoubles, TripleDoubles)
                    VALUES (@Name, @Team, @Games, @GamesStarted, @PointsPerGame, @ReboundsPerGame, 
                    @AssistsPerGame, @MinutesPerGame, @FieldGoalPct, @FreeThrowPct, @ThreePointPct, 
                    @StealsPerGame, @TurnoversPerGame, @BlocksPerGame, @PlusMinus, @DoubleDoubles, @TripleDoubles)";

                    using var cmd = new SqlCommand(insertQuery, connection);
                    cmd.Parameters.AddWithValue("@Name", values[0]);
                    cmd.Parameters.AddWithValue("@Team", values[1]);
                    cmd.Parameters.AddWithValue("@Games", int.Parse(values[2]));
                    cmd.Parameters.AddWithValue("@GamesStarted", int.Parse(values[3]));
                    cmd.Parameters.AddWithValue("@PointsPerGame", float.Parse(values[4]));
                    cmd.Parameters.AddWithValue("@ReboundsPerGame", float.Parse(values[5]));
                    cmd.Parameters.AddWithValue("@AssistsPerGame", float.Parse(values[6]));
                    cmd.Parameters.AddWithValue("@MinutesPerGame", float.Parse(values[7]));
                    cmd.Parameters.AddWithValue("@FieldGoalPct", float.Parse(values[8]));
                    cmd.Parameters.AddWithValue("@FreeThrowPct", float.Parse(values[9]));
                    cmd.Parameters.AddWithValue("@ThreePointPct", float.Parse(values[10]));
                    cmd.Parameters.AddWithValue("@StealsPerGame", float.Parse(values[11]));
                    cmd.Parameters.AddWithValue("@TurnoversPerGame", float.Parse(values[12]));
                    cmd.Parameters.AddWithValue("@BlocksPerGame", float.Parse(values[13]));
                    cmd.Parameters.AddWithValue("@PlusMinus", int.Parse(values[14]));
                    cmd.Parameters.AddWithValue("@DoubleDoubles", int.Parse(values[15]));
                    cmd.Parameters.AddWithValue("@TripleDoubles", int.Parse(values[16]));

                    await cmd.ExecuteNonQueryAsync();
                }

                _logger.LogInformation($"Successfully imported {lines.Length - 1} player records");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing CSV data to database");
                throw;
            }
        }
    }
}