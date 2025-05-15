using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using Tutorial9;

namespace WarehouseApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WarehouseController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public WarehouseController(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("Default");
        }

        [HttpPost]
        public IActionResult AddProductToWarehouse([FromBody] ProductWarehouseRequest request)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                // czy istnieje produkt
                using (SqlCommand checkProduct = new SqlCommand("SELECT 1 FROM Product WHERE IdProduct = @IdProduct", connection))
                {
                    checkProduct.Parameters.AddWithValue("@IdProduct", request.IdProduct);

                    if (checkProduct.ExecuteScalar() == null)
                    {
                        return NotFound("Produkt nie istnieje");
                    }
                }

                // czy istnieje magazyn
                using (SqlCommand checkWarehouse = new SqlCommand("SELECT 1 FROM Warehouse WHERE IdWarehouse = @IdWarehouse", connection))
                {
                    checkWarehouse.Parameters.AddWithValue("@IdWarehouse", request.IdWarehouse);

                    if (checkWarehouse.ExecuteScalar() == null)
                    {
                        return NotFound("Magazyn nie istnieje");
                    }
                }

                // czy istnieje zamówienie
                int? orderId = null;
                decimal productPrice = 0;

                using (SqlCommand checkOrder = new SqlCommand(@"
                    SELECT o.IdOrder, p.Price
                    FROM [Order] o JOIN Product p ON o.IdProduct = p.IdProduct
                    WHERE
                        o.IdProduct = @IdProduct AND
                        o.Amount = @Amount AND
                        o.CreatedAt < @CreatedAt AND
                        o.FulfilledAt IS NULL AND
                        NOT EXISTS (
                            SELECT 1 FROM Product_Warehouse pw
                            WHERE pw.IdOrder = o.IdOrder
                        )", connection))
                {
                    checkOrder.Parameters.AddWithValue("@IdProduct", request.IdProduct);
                    checkOrder.Parameters.AddWithValue("@Amount", request.Amount);
                    checkOrder.Parameters.AddWithValue("@CreatedAt", request.CreatedAt);

                    using (SqlDataReader reader = checkOrder.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            orderId = reader.GetInt32(0);
                            productPrice = reader.GetDecimal(1);
                        }
                        else
                        {
                            return NotFound("Nie znaleziono pasującego, niezrealizowanego zamówienia");
                        }
                    }
                }
                
                
                
                
                SqlTransaction transaction = connection.BeginTransaction();

                try
                {
                    // update FulfilledAt zamówienia
                    using (SqlCommand updateOrder = new SqlCommand(
                        "UPDATE [Order] SET FulfilledAt = @FulfilledAt WHERE IdOrder = @IdOrder",
                        connection,
                        transaction))
                    {
                        updateOrder.Parameters.AddWithValue("@FulfilledAt", DateTime.Now);
                        updateOrder.Parameters.AddWithValue("@IdOrder", orderId);

                        updateOrder.ExecuteNonQuery();
                    }

                    // wstawienie do Product_Warehouse
                    int idProductWarehouse;

                    using (SqlCommand insertProductWarehouse = new SqlCommand(@"
                        INSERT INTO Product_Warehouse (IdWarehouse, IdProduct, IdOrder, Amount, Price, CreatedAt)
                        OUTPUT INSERTED.IdProductWarehouse
                        VALUES (@IdWarehouse, @IdProduct, @IdOrder, @Amount, @Price, @CreatedAt)",
                        connection,
                        transaction))
                    {
                        insertProductWarehouse.Parameters.AddWithValue("@IdWarehouse", request.IdWarehouse);
                        insertProductWarehouse.Parameters.AddWithValue("@IdProduct", request.IdProduct);
                        insertProductWarehouse.Parameters.AddWithValue("@IdOrder", orderId);
                        insertProductWarehouse.Parameters.AddWithValue("@Amount", request.Amount);
                        insertProductWarehouse.Parameters.AddWithValue("@Price", productPrice * request.Amount);
                        insertProductWarehouse.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

                        idProductWarehouse = (int)insertProductWarehouse.ExecuteScalar();
                    }

                    transaction.Commit();

                    return Ok(idProductWarehouse);
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return StatusCode(500, "Wystąpił błąd: " + ex.Message);
                }
            }
        }

        [HttpPost("procedure")]
        public IActionResult AddProductToWarehouseUsingProcedure([FromBody] ProductWarehouseRequest request)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                using (SqlCommand command = new SqlCommand("AddProductToWarehouse", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    command.Parameters.AddWithValue("@IdProduct", request.IdProduct);
                    command.Parameters.AddWithValue("@IdWarehouse", request.IdWarehouse);
                    command.Parameters.AddWithValue("@Amount", request.Amount);
                    command.Parameters.AddWithValue("@CreatedAt", request.CreatedAt);

                    try
                    {
                        var result = command.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            return Ok(Convert.ToInt32(result));
                        }
                        return StatusCode(500, "Nie udało się uzyskać identyfikatora wstawionego rekordu");
                    }
                    catch (SqlException ex)
                    {
                        switch (ex.Number)
                        {
                            case 50000:
                                return BadRequest(ex.Message);
                            default:
                                return StatusCode(500, $"Błąd bazy danych: {ex.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        return StatusCode(500, $"Wystąpił błąd: {ex.Message}");
                    }
                }
            }
        }
    }
}