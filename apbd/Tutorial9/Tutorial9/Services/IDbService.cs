namespace Tutorial9.Services;
using System.Data;
using Microsoft.Data.SqlClient;
using Tutorial9.Model;

public interface IDbService
{
    Task<bool> ProductExistsAsync(int idProduct);
    Task<bool> WarehouseExistsAsync(int idWarehouse);
    Task<Order?> GetOrderAsync(int idProduct, int amount, DateTime createdAt);
    Task<bool> WasRealizedAsync(int idOrder);
    Task<int> UpdateOrderAsync(int idWarehouse, int idProduct, int idOrder, int amount);
}
