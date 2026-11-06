using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class Program
{
    private const string Palette = "@%#*+=-:. ";
    private const int DefaultWidth = 96;

    [STAThread]
    private static int Main(string[] args)
    {
        if (args is ["--help"] or ["-h"])
        {
            PrintUsage(Console.Out);
            return 0;
        }

        var width = DefaultWidth;
        if (args.Length is < 1 or > 3 ||
            (args.Length >= 2 && (!int.TryParse(args[1], out width) || width is < 1 or > 240)))
        {
            PrintUsage(Console.Error);
            return 2;
        }

        try
        {
            if (args.Length == 3 && string.Equals(Path.GetFullPath(args[0]), Path.GetFullPath(args[2]),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Choose an output file different from the input image.");
            }

            var ascii = ConvertImage(args[0], width);
            if (args.Length == 3)
            {
                File.WriteAllText(args[2], ascii);
            }
            else
            {
                Console.Write(ascii);
            }

            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                   or FormatException or NotSupportedException or ArgumentException)
        {
            Console.Error.WriteLine($"Could not convert the image: {ex.Message}");
            return 1;
        }
    }

    private static string ConvertImage(string path, int width)
    {
        using var input = File.OpenRead(path);
        var frame = BitmapDecoder.Create(input, BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnDemand).Frames[0];

        var height = Math.Max(1, (int)Math.Round(frame.PixelHeight * (double)width / frame.PixelWidth / 2));
        if (height > 2_000)
        {
            throw new ArgumentException("The output would exceed 2,000 rows. Choose a smaller width.");
        }

        var resized = new TransformedBitmap(frame,
            new ScaleTransform((double)width / frame.PixelWidth, (double)height / frame.PixelHeight));
        var bitmap = new FormatConvertedBitmap(resized, PixelFormats.Bgra32, null, 0);
        var stride = bitmap.PixelWidth * 4;
        var pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);

        var output = new StringBuilder();
        for (var row = 0; row < bitmap.PixelHeight; row++)
        {
            for (var column = 0; column < bitmap.PixelWidth; column++)
            {
                var pixel = row * stride + column * 4;
                var brightness = 0.0722 * pixels[pixel] + 0.7152 * pixels[pixel + 1] + 0.2126 * pixels[pixel + 2];
                var opacity = pixels[pixel + 3] / 255.0;
                var onWhite = brightness * opacity + 255 * (1 - opacity);
                output.Append(Palette[(int)Math.Round(onWhite * (Palette.Length - 1) / 255)]);
            }

            output.AppendLine();
        }

        return output.ToString();
    }

    private static void PrintUsage(TextWriter writer)
    {
        writer.WriteLine("Usage: ImageToAscii <image-path> [width] [output-path]");
        writer.WriteLine($"Width: 1–240 characters (default: {DefaultWidth}). Without an output path, prints to the terminal.");
    }
}
