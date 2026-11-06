using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/convert")]
public sealed class ConvertController(AsciiConverter converter) : ControllerBase
{
    private const int MaxFileBytes = 8 * 1024 * 1024;
    private const int MaxRequestBytes = MaxFileBytes + 64 * 1024;

    [HttpPost]
    [Consumes("multipart/form-data")]
    [Produces("text/plain", "application/problem+json")]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public IActionResult Convert([FromForm] IFormFile? image, [FromForm] int width = 96,
        CancellationToken cancellationToken = default)
    {
        if (image is null || image.Length == 0)
            return Problem(statusCode: 400, title: "Выберите изображение.");

        if (image.Length > MaxFileBytes)
            return Problem(statusCode: 413, title: "Файл должен быть не больше 8 МиБ.");

        if (width is < 32 or > 240)
            return Problem(statusCode: 400, title: "Ширина должна быть от 32 до 240 символов.");

        try
        {
            using var stream = image.OpenReadStream();
            var ascii = converter.Convert(stream, width, cancellationToken);
            return Content(ascii, "text/plain; charset=utf-8");
        }
        catch (ImageDimensionsException ex)
        {
            return Problem(statusCode: 422, title: ex.Message);
        }
        catch (NotSupportedException)
        {
            return Problem(statusCode: 415, title: "Этот формат изображения не поддерживается.");
        }
        catch (InvalidDataException)
        {
            return Problem(statusCode: 400, title: "Не удалось прочитать изображение.");
        }
    }
}
