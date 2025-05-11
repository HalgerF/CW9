using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Tutorial9.Model;

namespace Tutorial9.Services;

public class DbService : IDbService
{
    private readonly IConfiguration _configuration;
    public DbService(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    public async Task<bool> ProductExistsAsync(int idProduct)
    {
        await using SqlConnection connection = new SqlConnection(_configuration.GetConnectionString("Default"));
        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Product WHERE IdProduct = @IdProduct";
        command.Parameters.AddWithValue("@IdProduct", idProduct);
        command.CommandType = CommandType.Text;
        await connection.OpenAsync();
        var result = await command.ExecuteScalarAsync();
        return (int)result != 0;
    }

    public async Task<bool> WarehouseExistsAsync(int idWarehouse)
    {
        await using SqlConnection connection = new SqlConnection(_configuration.GetConnectionString("Default"));
        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Warehouse WHERE IdWarehouse = @IdWarehouse";
        command.Parameters.AddWithValue("@IdWarehouse", idWarehouse);
        command.CommandType = CommandType.Text;
        await connection.OpenAsync();
        var result = await command.ExecuteScalarAsync();
        return (int)result != 0;
    }

    public async Task<Order?> GetOrderAsync(int idProduct, int amount, DateTime createdAt)
    {
        await using SqlConnection connection = new SqlConnection(_configuration.GetConnectionString("Default"));
        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = @"SELECT TOP 1 *
                                FROM [Order]
                                WHERE IdProduct = @IdProduct
                                  AND Amount = @Amount
                                  AND CreatedAt < @CreatedAt
                                  AND FulfilledAt IS NULL";
        command.Parameters.AddWithValue("@IdProduct", idProduct);
        command.Parameters.AddWithValue("@Amount", amount);
        command.Parameters.AddWithValue("@CreatedAt", createdAt);
        command.CommandType = CommandType.Text;
        await connection.OpenAsync();

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Order
            {
                IdOrder    = reader.GetInt32(reader.GetOrdinal("IdOrder")),
                IdProduct  = reader.GetInt32(reader.GetOrdinal("IdProduct")),
                Amount     = reader.GetInt32(reader.GetOrdinal("Amount")),
                CreatedAt  = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
            };
        }

        return null;
    }

    public async Task<bool> WasRealizedAsync(int idOrder)
    {
        await using SqlConnection connection = new SqlConnection(_configuration.GetConnectionString("Default"));
        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Product_Warehouse WHERE IdOrder = @IdOrder";
        command.Parameters.AddWithValue("@IdOrder", idOrder);
        command.CommandType = CommandType.Text;
        await connection.OpenAsync();
        var result = await command.ExecuteScalarAsync();
        return (int)result != 0;
    }

    public async Task<int> UpdateOrderAsync(int IdWarehouse, int IdProduct, int IdOrder, int Amount)
    {
        await using SqlConnection connection = new SqlConnection(_configuration.GetConnectionString("Default"));
        await using SqlCommand command = connection.CreateCommand();
        await connection.OpenAsync();

    
        command.CommandText = "UPDATE [Order] SET FulfilledAt = @Time WHERE IdOrder = @IdOrder";
        command.Parameters.AddWithValue("@Time", DateTime.Now);
        command.Parameters.AddWithValue("@IdOrder", IdOrder);
        await command.ExecuteNonQueryAsync();

        command.Parameters.Clear();
        command.CommandText = @"INSERT INTO Product_Warehouse
                                (IdWarehouse, IdProduct, IdOrder, Amount, Price, CreatedAt)
                                VALUES
                                (@IdWarehouse, @IdProduct, @IdOrder, @Amount, @Price, @CreatedAt);
                                SELECT CAST(SCOPE_IDENTITY() AS INT);";
        command.Parameters.AddWithValue("@IdWarehouse", IdWarehouse);
        command.Parameters.AddWithValue("@IdProduct", IdProduct);
        command.Parameters.AddWithValue("@IdOrder", IdOrder);
        command.Parameters.AddWithValue("@Amount", Amount);
        command.Parameters.AddWithValue("@Price", Amount);
        command.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

        var newIdObj = await command.ExecuteScalarAsync();
        return Convert.ToInt32(newIdObj);
    }
}
