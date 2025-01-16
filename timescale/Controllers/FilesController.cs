using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using timescale.Data;
using timescale.Models;

namespace timescale.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FilesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public FilesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadCsv(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("File is missing.");

            var values = new List<Value>();
            try
            {
                using (var reader = new StreamReader("path/to/your/file.csv"))
                using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true, // Указываем, что файл содержит заголовок
                    Delimiter = ";"         // Разделитель для CSV
                }))
                {
                    csv.Read();          // Считываем первую строку
                    csv.ReadHeader();    // Читаем заголовок (обязательно перед GetField)

                    while (csv.Read())   // Проходим по строкам после заголовка
                    {
                        var date = csv.GetField<DateTime>("Date");
                        var executionTime = csv.GetField<double>("ExecutionTime");
                        var value = csv.GetField<double>("Value");


                        // Здесь идет обработка строки
                        Console.WriteLine($"Date: {date}, ExecutionTime: {executionTime}, Value: {value}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing CSV: {ex.Message}");
            }

            if (values.Count < 1 || values.Count > 10_000)
                return BadRequest("The file must contain between 1 and 10,000 rows.");

            // Save values to database
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Delete existing records for the same file
                var existingRecords = _context.Values.Where(v => v.FileName == file.FileName);
                _context.Values.RemoveRange(existingRecords);
                await _context.SaveChangesAsync();

                // Add new records
                _context.Values.AddRange(values);
                await _context.SaveChangesAsync();

                // Calculate results
                var minDate = values.Min(v => v.Date);
                var maxDate = values.Max(v => v.Date);
                var avgExecutionTime = values.Average(v => v.ExecutionTime);
                var avgValue = values.Average(v => v.IndicatorValue);
                var medianValue = values.OrderBy(v => v.IndicatorValue)
                    .Select(v => v.IndicatorValue)
                    .Skip(values.Count / 2)
                    .First();
                var maxValue = values.Max(v => v.IndicatorValue);
                var minValue = values.Min(v => v.IndicatorValue);

                var result = new Models.Results
                {
                    FileName = file.FileName,
                    DeltaTime = maxDate - minDate,
                    MinDate = minDate,
                    AvgExecutionTime = avgExecutionTime,
                    AvgValue = avgValue,
                    MedianValue = medianValue,
                    MaxValue = maxValue,
                    MinValue = minValue
                };

                // Save results
                _context.Results.Add(result);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest($"Error saving data: {ex.Message}");
            }

            return Ok("File processed successfully.");
        }
    }
}
