using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using Tutorial9.Model.DTOs;

namespace Tutorial9.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WarehouseController : ControllerBase
{
    private readonly string _connStr;

    public WarehouseController(IConfiguration config)
    {
        _connStr = config.GetConnectionString("DefaultConnection");
    }
    
    [HttpPost("add-inline")]
    public async Task<ActionResult<AddToWarehouseResponseDto>> AddInline([FromBody] AddToWarehouseRequestDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (dto.Amount <= 0)
            return BadRequest("Amount must be greater than 0");

        using var conn = new SqlConnection(_connStr);
        await conn.OpenAsync();

        using var tran = conn.BeginTransaction();
        try
        {
            using (var cmd = new SqlCommand("SELECT COUNT(1) FROM Product WHERE Id = @pid", conn, tran))
            {
                cmd.Parameters.AddWithValue("@pid", dto.ProductId);
                if ((int)await cmd.ExecuteScalarAsync() == 0)
                    return NotFound($"Product {dto.ProductId} not found");
            }

            using (var cmd = new SqlCommand("SELECT COUNT(1) FROM Warehouse WHERE Id = @wid", conn, tran))
            {
                cmd.Parameters.AddWithValue("@wid", dto.WarehouseId);
                if ((int)await cmd.ExecuteScalarAsync() == 0)
                    return NotFound($"Warehouse {dto.WarehouseId} not found");
            }
            
            using (var cmd = new SqlCommand(@"
                SELECT Id, Amount, CreatedAt 
                FROM [Order] 
                WHERE ProductId = @pid AND Amount = @amt", conn, tran))
            {
                cmd.Parameters.AddWithValue("@pid", dto.ProductId);
                cmd.Parameters.AddWithValue("@amt", dto.Amount);

                using var reader = await cmd.ExecuteReaderAsync();
                if (!reader.Read())
                    return BadRequest("No matching Order found");

                var orderId = reader.GetInt32(0);
                var orderDate = reader.GetDateTime(2);
                reader.Close();

                if (orderDate >= dto.CreatedAt)
                    return BadRequest("Order.CreatedAt must be before request.CreatedAt");

                using (var cmd2 = new SqlCommand("SELECT COUNT(1) FROM Product_Warehouse WHERE OrderId = @oid", conn, tran))
                {
                    cmd2.Parameters.AddWithValue("@oid", orderId);
                    if ((int)await cmd2.ExecuteScalarAsync() > 0)
                        return BadRequest("Order already fulfilled");
                }
                
                using (var cmd3 = new SqlCommand("UPDATE [Order] SET FullfilledAt = @now WHERE Id = @oid", conn, tran))
                {
                    cmd3.Parameters.AddWithValue("@now", DateTime.UtcNow);
                    cmd3.Parameters.AddWithValue("@oid", orderId);
                    await cmd3.ExecuteNonQueryAsync();
                }
                
                int newPwId;
                using (var cmd4 = new SqlCommand(@"
                    INSERT INTO Product_Warehouse
                      (ProductId, WarehouseId, OrderId, Price, Amount, CreatedAt)
                    VALUES
                      (@pid, @wid, @oid,
                       (SELECT Price FROM Product WHERE Id = @pid) * @amt,
                       @amt,
                       @created)
                    SELECT CAST(SCOPE_IDENTITY() AS INT);", conn, tran))
                {
                    cmd4.Parameters.AddWithValue("@pid", dto.ProductId);
                    cmd4.Parameters.AddWithValue("@wid", dto.WarehouseId);
                    cmd4.Parameters.AddWithValue("@oid", orderId);
                    cmd4.Parameters.AddWithValue("@amt", dto.Amount);
                    cmd4.Parameters.AddWithValue("@created", dto.CreatedAt);

                    newPwId = (int)await cmd4.ExecuteScalarAsync();
                }

                tran.Commit();
                return CreatedAtAction(nameof(AddInline),
                    new { id = newPwId },
                    new AddToWarehouseResponseDto { ProductWarehouseId = newPwId });
            }
        }
        catch
        {
            tran.Rollback();
            return StatusCode(500, "Internal error");
        }
    }
    
    [HttpPost("add-sp")]
    public async Task<ActionResult<AddToWarehouseResponseDto>> AddStoredProc([FromBody] AddToWarehouseRequestDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        using var conn = new SqlConnection(_connStr);
        await conn.OpenAsync();

        using var cmd = new SqlCommand("proc_AddProductToWarehouse", conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@ProductId", dto.ProductId);
        cmd.Parameters.AddWithValue("@WarehouseId", dto.WarehouseId);
        cmd.Parameters.AddWithValue("@Amount", dto.Amount);
        cmd.Parameters.AddWithValue("@CreatedAt", dto.CreatedAt);

        var outParam = new SqlParameter("@NewId", SqlDbType.Int)
        {
            Direction = ParameterDirection.Output
        };
        cmd.Parameters.Add(outParam);

        try
        {
            await cmd.ExecuteNonQueryAsync();
            var newId = (int)outParam.Value;
            return CreatedAtAction(nameof(AddStoredProc),
                new { id = newId },
                new AddToWarehouseResponseDto { ProductWarehouseId = newId });
        }
        catch (SqlException ex) when (ex.Number == 50000) 
        {
            return BadRequest(ex.Message);
        }
        catch
        {
            return StatusCode(500, "Internal error");
        }
    }
}