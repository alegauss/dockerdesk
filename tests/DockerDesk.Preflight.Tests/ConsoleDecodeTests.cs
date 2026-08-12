using System.Text;
using DockerDesk.Core.Preflight.Windows;
using Xunit;

namespace DockerDesk.Preflight.Tests;

/// <summary>
/// wsl.exe writes UTF-16LE. Decoded as UTF-8 its output becomes a string with a NUL after every
/// character, which matches no version pattern and reads exactly like "WSL is not installed" — a
/// green machine reported red, on the one row a user cannot argue with.
/// </summary>
public sealed class ConsoleDecodeTests
{
    private const string Sample = "WSL version: 2.6.1.0\r\nKernel version: 6.6.87.2\r\n";

    [Fact]
    public void Utf16_with_a_byte_order_mark_decodes()
    {
        var bytes = Encoding.Unicode.GetPreamble()
            .Concat(Encoding.Unicode.GetBytes(Sample))
            .ToArray();

        Assert.Equal(Sample, ConsoleTool.Decode(bytes));
    }

    [Fact]
    public void Utf16_without_a_byte_order_mark_decodes()
    {
        Assert.Equal(Sample, ConsoleTool.Decode(Encoding.Unicode.GetBytes(Sample)));
    }

    [Fact]
    public void Utf8_decodes()
    {
        Assert.Equal(Sample, ConsoleTool.Decode(Encoding.UTF8.GetBytes(Sample)));
    }

    [Fact]
    public void Utf8_carrying_non_ascii_is_not_mistaken_for_utf16()
    {
        const string localized = "Version du noyau : 6.6.87.2 — installé\r\n";

        Assert.Equal(localized, ConsoleTool.Decode(Encoding.UTF8.GetBytes(localized)));
    }

    [Fact]
    public void Nothing_decodes_to_nothing()
    {
        Assert.Equal("", ConsoleTool.Decode([]));
    }

    [Fact]
    public void Decode_refuses_null() =>
        Assert.Throws<ArgumentNullException>(() => ConsoleTool.Decode(null!));
}
