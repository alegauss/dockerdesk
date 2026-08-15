using FreeWilly.Core.Builds;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// The build a <c>docker-desktop://</c> link names, read out of it (DD126).
/// </summary>
/// <remarks>
/// This is reached from a registered protocol handler, so the argument is whatever any process on
/// the machine put in a link. Most of what is asserted here is therefore refusal.
/// </remarks>
public class BuildAddressTests
{
    private const string Ref = "default/default/i93abaotri2m3vdda5unxeimu";

    [Fact]
    public void The_link_buildx_prints_resolves_to_the_ref_buildx_history_uses()
    {
        // The exact line, and the exact ref: `history ls` reports <builder>/<node>/<id>, which is
        // the tail of the URL — so the whole tail is taken and not the last segment. Measured
        // against the pinned Buildx on a real build.
        Assert.Equal(Ref, BuildAddress.RefIn($"docker-desktop://dashboard/build/{Ref}"));
    }

    [Fact]
    public void A_bare_ref_is_accepted_so_the_verb_is_usable_by_hand()
    {
        Assert.Equal(Ref, BuildAddress.RefIn(Ref));
        Assert.Equal("i93abaotri2m3vdda5unxeimu", BuildAddress.RefIn("i93abaotri2m3vdda5unxeimu"));
    }

    [Fact]
    public void The_scheme_is_matched_without_regard_to_case()
    {
        // Windows hands a handler the URL as the caller wrote it, and a scheme is case-insensitive.
        Assert.Equal(Ref, BuildAddress.RefIn($"DOCKER-DESKTOP://dashboard/build/{Ref}"));
    }

    [Fact]
    public void Quotes_and_spaces_around_it_are_not_part_of_the_ref()
    {
        Assert.Equal(Ref, BuildAddress.RefIn($"  \"docker-desktop://dashboard/build/{Ref}\"  "));
    }

    [Fact]
    public void A_trailing_slash_or_a_query_is_not_part_of_the_ref()
    {
        // Neither is written today. A link that grows one must not become a lookup for a ref that
        // does not exist.
        Assert.Equal(Ref, BuildAddress.RefIn($"docker-desktop://dashboard/build/{Ref}/"));
        Assert.Equal(Ref, BuildAddress.RefIn($"docker-desktop://dashboard/build/{Ref}?tab=logs"));
        Assert.Equal(Ref, BuildAddress.RefIn($"docker-desktop://dashboard/build/{Ref}#steps"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("docker-desktop://dashboard/settings")]
    [InlineData("docker-desktop://dashboard/build/")]
    [InlineData("https://example.invalid/build/abc")]
    public void Anything_that_does_not_name_a_build_is_refused(string? argument) =>
        Assert.Null(BuildAddress.RefIn(argument));

    [Theory]
    [InlineData("docker-desktop://dashboard/build/../../windows/system32")]
    [InlineData(@"docker-desktop://dashboard/build/a\b")]
    [InlineData("docker-desktop://dashboard/build/a b")]
    [InlineData("docker-desktop://dashboard/build/a;calc")]
    [InlineData("docker-desktop://dashboard/build/a&b")]
    [InlineData(@"C:\Windows\System32\calc.exe")]
    public void A_ref_that_is_not_shaped_like_one_never_reaches_the_CLI(string argument)
    {
        // The ref becomes a subprocess argument, so what it may contain is decided here rather than
        // downstream. A traversal, a backslash, a separator or a shell character is not a build id,
        // and this is the only place that has to know it.
        Assert.Null(BuildAddress.RefIn(argument));
    }

    [Fact]
    public void An_absurdly_long_argument_is_refused_rather_than_passed_on()
    {
        Assert.Null(BuildAddress.RefIn(new string('a', 500)));
    }
}
