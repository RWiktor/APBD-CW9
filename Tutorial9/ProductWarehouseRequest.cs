using System.ComponentModel.DataAnnotations;

namespace Tutorial9;

public class ProductWarehouseRequest
{
    [Required]
    public int IdProduct { get; set; }

    [Required]
    public int IdWarehouse { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Ilość musi być większa od 0")]
    public int Amount { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; }
}