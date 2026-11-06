using SkiaSharp;

public sealed class AsciiConverterTests
{
    private readonly AsciiConverter converter = new();

    [Theory]
    [InlineData(0, 255, '@')]
    [InlineData(255, 255, ' ')]
    [InlineData(0, 0, ' ')]
    public void ConvertsBrightnessAndTransparency(byte shade, byte alpha, char glyph)
    {
        using var image = new MemoryStream(TestImages.SolidPng(8, 8, new SKColor(shade, shade, shade, alpha)));
        var rows = converter.Convert(image, 32, TestContext.Current.CancellationToken).Split('\n');

        Assert.Equal(16, rows.Length);
        Assert.All(rows, row => Assert.Equal(new string(glyph, 32), row));
    }

    [Fact]
    public void PreservesThinContourOnAnEvenBackground()
    {
        using var bitmap = new SKBitmap(32, 32);
        bitmap.Erase(new SKColor(174, 174, 174));
        for (var y = 0; y < 32; y++)
            bitmap.SetPixel(16, y, new SKColor(157, 157, 157));

        using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        using var image = png.AsStream();
        var rows = converter.Convert(image, 32, TestContext.Current.CancellationToken).Split('\n');

        Assert.Equal(16, rows.Length);
        Assert.All(rows, row => Assert.Equal(new string('-', 16) + "=" + new string('-', 15), row));
    }

    [Fact]
    public void RotatesJpegUsingExifOrientation()
    {
        using var bitmap = new SKBitmap(40, 20);
        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint { Color = SKColors.Black };
        canvas.Clear(SKColors.White);
        canvas.DrawRect(0, 0, 20, 20, paint);
        canvas.Flush();
        using var jpeg = bitmap.Encode(SKEncodedImageFormat.Jpeg, 100);
        var bytes = jpeg.ToArray();
        // APP1 with a little-endian TIFF orientation tag set to 6 (90° clockwise).
        var exif = System.Convert.FromHexString("FFE1002245786966000049492A0008000000010012010300010000000600000000000000");
        using var image = new MemoryStream([.. bytes[..2], .. exif, .. bytes[2..]]);
        var rows = converter.Convert(image, 32, TestContext.Current.CancellationToken).Split('\n');

        Assert.Equal(32, rows.Length);
        Assert.Equal(new string('@', 32), rows[0]);
        Assert.Equal(new string(' ', 32), rows[^1]);
    }
}
