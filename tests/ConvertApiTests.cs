using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using SkiaSharp;

public sealed class ConvertApiTests(TestWebApplication application) : IClassFixture<TestWebApplication>
{
    [Theory]
    [InlineData(null, 96)]
    [InlineData("120", 120)]
    public async Task ReturnsAsciiWithRequestedOrDefaultWidth(string? width, int expectedWidth)
    {
        using var client = application.CreateClient();
        using var form = MakeForm(TestImages.SolidPng(2, 2, SKColors.Black), width);
        using var response = await client.PostAsync("/api/convert", form, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        var ascii = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(string.Join('\n', Enumerable.Repeat(new string('@', expectedWidth), expectedWidth / 2)), ascii);
    }

    [Theory]
    [InlineData("31")]
    [InlineData("241")]
    [InlineData("abc")]
    public async Task RejectsInvalidWidth(string width)
    {
        await AssertProblem(TestImages.SolidPng(2, 2, SKColors.Black), width, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RejectsMissingOrEmptyImage()
    {
        await AssertProblem(null, "96", HttpStatusCode.BadRequest);
        await AssertProblem([], "96", HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RejectsUnsupportedFileRegardlessOfItsExtension()
    {
        await AssertProblem("not a PNG"u8.ToArray(), "96", HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task RejectsTruncatedPng()
    {
        var png = TestImages.SolidPng(8, 8, SKColors.Black);
        var dataStart = png.AsSpan().IndexOf("IDAT"u8) + 4;
        Assert.True(dataStart > 4);

        await AssertProblem(png[..dataStart], "96", HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RejectsFileLargerThanEightMebibytes()
    {
        await AssertProblem(new byte[8 * 1024 * 1024 + 1], "96", HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task RejectsTooManyPixelsEvenWhenFileIsSmall()
    {
        var png = TestImages.SolidPng(4096, 4096, SKColors.White);
        Assert.True(png.Length < 1024 * 1024);
        await AssertProblem(png, "96", HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task RejectsOutputThatWouldExceedRowLimit()
    {
        await AssertProblem(TestImages.SolidPng(1, 100, SKColors.Black), "96", HttpStatusCode.UnprocessableEntity);
    }

    private async Task AssertProblem(byte[]? bytes, string width, HttpStatusCode expected)
    {
        using var client = application.CreateClient();
        using var form = MakeForm(bytes, width);
        using var response = await client.PostAsync("/api/convert", form, TestContext.Current.CancellationToken);

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal((int)expected, problem?.Status);
        Assert.False(string.IsNullOrWhiteSpace(problem?.Title));
    }

    private static MultipartFormDataContent MakeForm(byte[]? bytes, string? width)
    {
        var form = new MultipartFormDataContent();
        if (bytes is not null)
        {
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(file, "image", "sample.png");
        }
        if (width is not null)
            form.Add(new StringContent(width), "width");
        return form;
    }
}

public sealed class TestWebApplication : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(logging => logging.ClearProviders());
    }
}
