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
                return BadRequest("Файл не найден");

            int lineCount;
            try
            {
                // Подсчёт количества строк в файле
                using (var reader = new StreamReader(file.OpenReadStream()))
                {
                    lineCount = 0;
                    while (reader.ReadLine() != null)
                    {
                        lineCount++;
                    }
                }

                // Учитываем заголовок, который должен быть первой строкой
                if (lineCount < 2 || lineCount > 10_001) // Минимум 1 строка данных + 1 строка заголовка
                    return BadRequest("Файл должен содержать от 1 до 10 000 строк данных (без учёта заголовка).");
            }
            catch
            {
                return BadRequest("Ошибка при подсчёте строк в файле.");
            }

            var values = new List<Value>();
            try
            {
                using (var reader = new StreamReader(file.OpenReadStream()))
                using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    Delimiter = ";"
                }))
                {
                    csv.Read();
                    csv.ReadHeader();

                    while (csv.Read())
                    {
                        //Записываем в переменные данные из файла
                        var date = csv.GetField<DateTime>("Date");
                        var executionTime = csv.GetField<double>("ExecutionTime");
                        var value = csv.GetField<double>("Value");

                        // Валидация строки
                        if (date < new DateTime(2000, 1, 1) || date > DateTime.UtcNow)
                            return BadRequest("Дата должна быть между 01.01.2000 и текущей датой.");
                        if (executionTime < 0)
                            return BadRequest("Время выполнения не может быть меньше 0.");
                        if (value < 0)
                            return BadRequest("Значение показателя не может быть меньше 0.");

                        //Добавляем данные в список для значений
                        values.Add(new Value
                        {
                            Date = date.ToUniversalTime(),
                            ExecutionTime = executionTime,
                            IndicatorValue = value,
                            FileName = file.FileName
                        });
                    }
                }
            }
            catch
            {
                return BadRequest("Ошибка парсинга CSV файла.");
            }

            // Дальнейшая обработка
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Удаляем существующие записи для текущего файла
                var existingRecords = _context.Value.Where(v => v.FileName == file.FileName);
                _context.Value.RemoveRange(existingRecords);
                await _context.SaveChangesAsync();

                // Сохраняем новые записи
                await _context.Value.AddRangeAsync(values);
                await _context.SaveChangesAsync();

                // Расчёты для таблицы Results
                var minDate = values.Min(v => v.Date);
                var maxDate = values.Max(v => v.Date);
                var avgExecutionTime = values.Average(v => v.ExecutionTime);
                var avgValue = values.Average(v => v.IndicatorValue);
                var orderedValues = values.OrderBy(v => v.IndicatorValue).Select(v => v.IndicatorValue).ToList();
                var medianValue = (orderedValues.Count % 2 == 0)
                    ? (orderedValues[orderedValues.Count / 2 - 1] + orderedValues[orderedValues.Count / 2]) / 2
                    : orderedValues[orderedValues.Count / 2];
                var maxValue = values.Max(v => v.IndicatorValue);
                var minValue = values.Min(v => v.IndicatorValue);

                //Записываем данные в список для результатов
                var result = new Models.Results
                {
                    FileName = file.FileName,
                    DeltaTime = (maxDate - minDate).TotalSeconds,
                    MinDate = minDate,
                    AvgExecutionTime = avgExecutionTime,
                    AvgValue = avgValue,
                    MedianValue = medianValue,
                    MaxValue = maxValue,
                    MinValue = minValue
                };
                // Сохраняем новые записи
                await _context.Results.AddAsync(result);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return BadRequest("Ошибка обработки данных.");
            }

            return Ok("Файл обработан успешно.");
        }
        [HttpGet("results")]
        public async Task<IActionResult> GetFilteredResults(
            string? fileName = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            double? avgValueMin = null,
            double? avgValueMax = null,
            double? avgExecutionTimeMin = null,
            double? avgExecutionTimeMax = null)
        {
            try
            {
                // Получаем базовый запрос из таблицы Results
                var query = _context.Results.AsQueryable();

                // Применяем фильтры
                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    query = query.Where(r => r.FileName == fileName);
                }

                if (startDate.HasValue)
                {
                    query = query.Where(r => r.MinDate >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(r => r.MinDate <= endDate.Value);
                }

                if (avgValueMin.HasValue)
                {
                    query = query.Where(r => r.AvgValue >= avgValueMin.Value);
                }

                if (avgValueMax.HasValue)
                {
                    query = query.Where(r => r.AvgValue <= avgValueMax.Value);
                }

                if (avgExecutionTimeMin.HasValue)
                {
                    query = query.Where(r => r.AvgExecutionTime >= avgExecutionTimeMin.Value);
                }

                if (avgExecutionTimeMax.HasValue)
                {
                    query = query.Where(r => r.AvgExecutionTime <= avgExecutionTimeMax.Value);
                }

                // Выполняем запрос
                var results = await query.ToListAsync();

                // Возвращаем результат
                return Ok(results);
            }
            catch (Exception ex)
            {
                return BadRequest($"Ошибка получения данных: {ex.Message}");
            }
        }
    }
}
