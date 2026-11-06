using System.Text;
using SkiaSharp;

public sealed class AsciiConverter
{
    private const string Palette = "@%#*+=-:. ";
    private const int MaxPixels = 16_000_000;
    private const int MaxRows = 2_000;

    public string Convert(Stream stream, int width, CancellationToken cancellationToken)
    {
        using var codec = SKCodec.Create(stream) ?? throw new NotSupportedException();
        var info = codec.Info;
        if ((long)info.Width * info.Height > MaxPixels)
            throw new ImageDimensionsException("Изображение должно содержать не больше 16 мегапикселей.");

        var origin = codec.EncodedOrigin;
        var swapsAxes = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var aspect = swapsAxes ? (double)info.Width / info.Height : (double)info.Height / info.Width;
        var height = Math.Max(1, (int)Math.Round(aspect * width / 2, MidpointRounding.AwayFromZero));
        if (height > MaxRows)
            throw new ImageDimensionsException("Изображение слишком высокое для этой детализации.");

        cancellationToken.ThrowIfCancellationRequested();
        var decodeInfo = new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var image = new SKBitmap(decodeInfo);
        if (codec.GetPixels(decodeInfo, image.GetPixels()) != SKCodecResult.Success)
            throw new InvalidDataException();

        cancellationToken.ThrowIfCancellationRequested();
        var sampling = new SKSamplingOptions(SKCubicResampler.Mitchell);
        var resizedInfo = new SKImageInfo(swapsAxes ? height : width, swapsAxes ? width : height,
            SKColorType.Rgba8888, SKAlphaType.Premul);
        using var resized = image.Resize(resizedInfo, sampling)
            ?? throw new InvalidDataException();

        var brightness = new float[width * height];
        var pixels = resized.GetPixelSpan();
        for (var y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < width; x++)
            {
                var (sourceX, sourceY) = origin switch
                {
                    SKEncodedOrigin.TopRight => (width - 1 - x, y),
                    SKEncodedOrigin.BottomRight => (width - 1 - x, height - 1 - y),
                    SKEncodedOrigin.BottomLeft => (x, height - 1 - y),
                    SKEncodedOrigin.LeftTop => (y, x),
                    SKEncodedOrigin.RightTop => (y, width - 1 - x),
                    SKEncodedOrigin.RightBottom => (height - 1 - y, width - 1 - x),
                    SKEncodedOrigin.LeftBottom => (height - 1 - y, x),
                    _ => (x, y)
                };
                var offset = sourceY * resized.RowBytes + sourceX * 4;
                var light = pixels[offset] * .2126f + pixels[offset + 1] * .7152f + pixels[offset + 2] * .0722f;
                brightness[y * width + x] = light + 255 - pixels[offset + 3];
            }
        }

        var output = new StringBuilder((width + 1) * height);
        for (var y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (y > 0) output.Append('\n');
            for (var x = 0; x < width; x++)
            {
                var light = SharpenBrightness(brightness, x, y, width, height);
                output.Append(Palette[(int)MathF.Round(light * (Palette.Length - 1) / 255, MidpointRounding.AwayFromZero)]);
            }
        }
        return output.ToString();
    }

    private static float SharpenBrightness(float[] brightness, int x, int y, int width, int height)
    {
        var cell = y * width + x;
        var light = brightness[cell];
        var left = brightness[x == 0 ? cell : cell - 1];
        var right = brightness[x == width - 1 ? cell : cell + 1];
        var above = brightness[y == 0 ? cell : cell - width];
        var below = brightness[y == height - 1 ? cell : cell + width];
        var difference = light - (left + right + above + below) / 4;
        var boost = MathF.Sign(difference) * Math.Clamp((MathF.Abs(difference) - 2) * .7f, 0, 14);
        return Math.Clamp(light + boost, 0, 255);
    }
}

public sealed class ImageDimensionsException(string message) : Exception(message);
