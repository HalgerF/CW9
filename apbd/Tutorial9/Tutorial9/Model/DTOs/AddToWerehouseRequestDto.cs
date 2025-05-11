using System.ComponentModel.DataAnnotations;

namespace Tutorial9.Model.DTOs;

public class AddToWarehouseRequestDto
{
    [Required] public int ProductId { get; set; }
    [Required] public int WarehouseId { get; set; }
    [Required] public int Amount { get; set; }
    [Required] public DateTime CreatedAt { get; set; }
}

public class AddToWarehouseResponseDto
{
    public int ProductWarehouseId { get; set; }
}
