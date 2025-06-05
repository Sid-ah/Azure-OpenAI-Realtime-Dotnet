using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using CsvHelper;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Formats.Asn1;
using Azure.Identity;
using System.Data.Common;

namespace DataImporter
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("NBA Statistics Data Importer");
            Console.WriteLine("============================");

            try
            {
                // Load configuration
                IConfiguration config = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
                    .Build();

                string connectionString = config.GetConnectionString("SqlServer");

                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException("Connection string not found in configuration");
                }

                string csvFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "2023-24 regular season statistics.csv");

                Console.WriteLine($"Importing data from {csvFilePath}...");

                // Import the data
                var importResults = await ImportCsvDataToSqlAsync(csvFilePath, connectionString);

                // Display results
                Console.WriteLine($"Import completed. {importResults.RowsImported} rows imported.");
                if (importResults.Errors.Any())
                {
                    Console.WriteLine($"{importResults.Errors.Count} errors occurred:");
                    foreach (var error in importResults.Errors.Take(5))
                    {
                        Console.WriteLine($"- {error}");
                    }

                    if (importResults.Errors.Count > 5)
                    {
                        Console.WriteLine($"... and {importResults.Errors.Count - 5} more errors");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Environment.Exit(1);
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        private static async Task<ImportResult> ImportCsvDataToSqlAsync(string csvFilePath, string connectionString)
        {
            var result = new ImportResult();

            // Determine CSV structure based on headers
            var csvRecords = ReadAndValidateCsvFile(csvFilePath);
            if (!csvRecords.Any())
            {
                result.Errors.Add("CSV file is empty or invalid");
                return result;
            }

            // Get column names from first record
            var columnNames = csvRecords.First().Keys.ToList();

            //var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                await ConnectAndImport(conn, csvRecords, columnNames, result);
            }

            return result;
        }

        private static async Task ConnectAndImport(SqlConnection connection, List<Dictionary<string, string>> csvRecords, List<string> columnNames, ImportResult result)
        {
            try
            {
                // Create the table if it doesn't exist
                bool tableExists = await CheckIfTableExistsAsync(connection, "NBAStats");
                if (!tableExists)
                {
                    await CreateTableAsync(connection, "NBAStats", columnNames);
                    Console.WriteLine("Created new table: NBAStats");
                }
                else
                {
                    Console.WriteLine("Using existing table: NBAStats");
                }

                // Import data in batches for better performance
                int batchSize = 1000;
                int totalImported = 0;
                int batchCount = 0;

                for (int i = 0; i < csvRecords.Count; i += batchSize)
                {
                    int currentBatchSize = Math.Min(batchSize, csvRecords.Count - i);
                    var batch = csvRecords.GetRange(i, currentBatchSize);

                    batchCount++;
                    Console.WriteLine($"Importing batch {batchCount} ({batch.Count} records)...");

                    int importedCount = await BulkInsertAsync(connection, "NBAStats", batch, columnNames, result.Errors);
                    totalImported += importedCount;

                    Console.WriteLine($"Imported {totalImported} records so far");
                }

                result.RowsImported = totalImported;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Database connection error: {ex.Message}");
                throw;
            }
        }

        private static List<Dictionary<string, string>> ReadAndValidateCsvFile(string csvFilePath)
        {
            var records = new List<Dictionary<string, string>>();

            using (var reader = new StreamReader(csvFilePath))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                // Read all records as dynamic
                var dynamicRecords = csv.GetRecords<dynamic>();

                // Convert each dynamic record to a dictionary
                foreach (var record in dynamicRecords)
                {
                    var dictionary = new Dictionary<string, string>();
                    foreach (var property in record as IDictionary<string, object>)
                    {
                        dictionary[property.Key] = property.Value?.ToString() ?? string.Empty;
                    }
                    records.Add(dictionary);
                }
            }

            return records;
        }

        private static async Task<bool> CheckIfTableExistsAsync(SqlConnection connection, string tableName)
        {
            string sql = @"
                SELECT COUNT(*) 
                FROM INFORMATION_SCHEMA.TABLES 
                WHERE TABLE_SCHEMA = 'dbo' 
                AND TABLE_NAME = @TableName";

            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@TableName", tableName);
                int count = (int)await command.ExecuteScalarAsync();
                return count > 0;
            }
        }

        private static async Task CreateTableAsync(SqlConnection connection, string tableName, List<string> columnNames)
        {
            // Create a table with all columns as NVARCHAR(MAX) initially
            // In a production application, you would want to determine proper data types
            var columnDefinitions = string.Join(", ", columnNames.Select(col => $"[{col}] NVARCHAR(MAX)"));

            string sql = $@"
                CREATE TABLE [dbo].[{tableName}] (
                    [Id] INT IDENTITY(1,1) PRIMARY KEY,
                    {columnDefinitions}
                )";

            using (var command = new SqlCommand(sql, connection))
            {
                await command.ExecuteNonQueryAsync();
            }
        }

        private static async Task<int> BulkInsertAsync(
            SqlConnection connection,
            string tableName,
            List<Dictionary<string, string>> batch,
            List<string> columnNames,
            List<string> errors)
        {
            try
            {
                // Create DataTable with all records
                var dataTable = new System.Data.DataTable();

                // Add columns
                foreach (var col in columnNames)
                {
                    dataTable.Columns.Add(col);
                }

                // Add rows
                foreach (var record in batch)
                {
                    var row = dataTable.NewRow();
                    foreach (var col in columnNames)
                    {
                        // Use safe conversion for database operations
                        if (record.TryGetValue(col, out var value))
                        {
                            row[col] = ConvertToSqlValue(value);
                        }
                        else
                        {
                            row[col] = DBNull.Value;
                        }
                    }
                    dataTable.Rows.Add(row);
                }

                // Use SqlBulkCopy to insert all records in one operation
                using (var bulkCopy = new SqlBulkCopy(connection))
                {
                    bulkCopy.DestinationTableName = $"[dbo].[{tableName}]";

                    // Map columns explicitly
                    foreach (var col in columnNames)
                    {
                        bulkCopy.ColumnMappings.Add(col, col);
                    }

                    // Set timeouts and batch size
                    bulkCopy.BulkCopyTimeout = 600; // 10 minutes
                    bulkCopy.BatchSize = 1000;

                    await bulkCopy.WriteToServerAsync(dataTable);
                }

                return batch.Count;
            }
            catch (Exception ex)
            {
                errors.Add($"Bulk insert error: {ex.Message}");
                Console.WriteLine($"Bulk insert error: {ex.Message}");
                return 0;
            }
        }

        // Proper null handling for SQL operations
        private static object ConvertToSqlValue(string value)
        {
            // Correctly handle null or empty strings for SQL operations
            if (string.IsNullOrEmpty(value))
            {
                return DBNull.Value;
            }
            return value;
        }
    }

    public class ImportResult
    {
        public int RowsImported { get; set; } = 0;
        public List<string> Errors { get; } = new List<string>();
    }
}