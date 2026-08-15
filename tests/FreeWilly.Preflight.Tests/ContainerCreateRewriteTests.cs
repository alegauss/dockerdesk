using System.Text;
using System.Text.Json;
using FreeWilly.Core.Engine;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// The create payload a Windows client sends, respelled for a Linux daemon (DD125).
/// </summary>
public class ContainerCreateRewriteTests
{
    private static byte[] Body(string json) => Encoding.UTF8.GetBytes(json);

    private static JsonDocument Rewritten(string json)
    {
        Assert.True(ContainerCreateRewrite.TryRewrite(Body(json), out var rewritten));
        return JsonDocument.Parse(rewritten);
    }

    private static string[] Binds(JsonDocument document) =>
        document.RootElement.GetProperty("HostConfig").GetProperty("Binds")
            .EnumerateArray().Select(entry => entry.GetString()!).ToArray();

    [Fact]
    public void A_drive_letter_bind_is_respelled_and_keeps_its_target_and_mode()
    {
        // The exact failure this was filed for, as the daemon reported it:
        // invalid volume specification: 'D:\…\repository:/opt/aem/crx-quickstart/repository:rw'
        var document = Rewritten("""
            {"Image":"aem-author","HostConfig":{"Binds":[
                "D:\\Git\\project\\volumes\\repository:/opt/aem/crx-quickstart/repository:rw"]}}
            """);

        Assert.Equal(
            "/mnt/d/Git/project/volumes/repository:/opt/aem/crx-quickstart/repository:rw",
            Assert.Single(Binds(document)));
    }

    [Fact]
    public void The_drive_letters_own_colon_does_not_split_the_spec()
    {
        // The whole difficulty of the short syntax, and the failure DD75 measured: scanning for the
        // first colon makes the source `D` and the mode `\project\data`, and the daemon answers
        // `invalid mode`. The source ends at the first colon AFTER the drive letter's.
        Assert.Equal(
            "/mnt/c/project/data:/data:ro",
            ContainerCreateRewrite.Respelled(@"C:\project\data:/data:ro"));
    }

    [Fact]
    public void A_bind_with_no_mode_is_respelled_too()
    {
        Assert.Equal(
            "/mnt/d/project:/work",
            ContainerCreateRewrite.Respelled(@"D:\project:/work"));
    }

    [Fact]
    public void A_named_volume_is_left_alone()
    {
        // Its source is a name the daemon owns, not a path. Respelling it would invent a directory
        // where the user asked for managed storage.
        Assert.Null(ContainerCreateRewrite.Respelled("aem-repository:/opt/aem/repository"));
    }

    [Fact]
    public void An_anonymous_volume_is_left_alone()
    {
        // A target and nothing else — there is no source to respell.
        Assert.Null(ContainerCreateRewrite.Respelled("/opt/aem/repository"));
    }

    [Fact]
    public void A_source_already_spelled_the_distributions_way_changes_nothing()
    {
        // The idempotence that matters: a client on a machine where this already ran, or a compose
        // file a user wrote by hand against /mnt, must not be rewritten into /mnt/mnt.
        Assert.Null(ContainerCreateRewrite.Respelled("/mnt/d/project:/work"));
        Assert.False(ContainerCreateRewrite.TryRewrite(
            Body("""{"HostConfig":{"Binds":["/mnt/d/project:/work"]}}"""), out _));
    }

    [Fact]
    public void A_Docker_Desktop_spelling_is_respelled_as_well()
    {
        // Wsl.WindowsFolderSpelledElsewhere already knows the two spellings Docker Desktop uses, so a
        // compose file or a script written against one of those lands here too.
        Assert.Equal(
            "/mnt/d/project:/work",
            ContainerCreateRewrite.Respelled("/run/desktop/mnt/host/d/project:/work"));
        Assert.Equal(
            "/mnt/c/project:/work",
            ContainerCreateRewrite.Respelled("/host_mnt/c/project:/work"));
    }

    [Fact]
    public void The_long_syntax_is_respelled_by_its_source_field()
    {
        var document = Rewritten("""
            {"HostConfig":{"Mounts":[
                {"Type":"bind","Source":"D:\\project\\data","Target":"/data"}]}}
            """);

        var mount = Assert.Single(
            document.RootElement.GetProperty("HostConfig").GetProperty("Mounts").EnumerateArray()
                .ToArray());

        Assert.Equal("/mnt/d/project/data", mount.GetProperty("Source").GetString());
        Assert.Equal("/data", mount.GetProperty("Target").GetString());
    }

    [Fact]
    public void A_volume_mount_in_the_long_syntax_is_left_alone()
    {
        // Type decides. A volume's Source is a name, and a tmpfs has none.
        Assert.False(ContainerCreateRewrite.TryRewrite(
            Body("""
                {"HostConfig":{"Mounts":[
                    {"Type":"volume","Source":"aem-repository","Target":"/data"}]}}
                """),
            out _));
    }

    [Fact]
    public void Everything_the_client_sent_that_was_not_a_bind_survives()
    {
        // The payload is the user's container. A rewrite that dropped a field would be this relay
        // quietly changing what they asked for, which is worse than the bug it fixes.
        var document = Rewritten("""
            {"Image":"aem-author","Env":["A=1","B=2"],"Cmd":["start"],
             "Labels":{"com.docker.compose.project":"deployment-aem-local"},
             "HostConfig":{"Binds":["D:\\p:/w"],"AutoRemove":true,"Memory":2147483648,
                           "PortBindings":{"4502/tcp":[{"HostPort":"4502"}]}}}
            """);

        var root = document.RootElement;
        Assert.Equal("aem-author", root.GetProperty("Image").GetString());
        Assert.Equal(["A=1", "B=2"], root.GetProperty("Env").EnumerateArray()
            .Select(e => e.GetString()!).ToArray());
        Assert.Equal("start", Assert.Single(root.GetProperty("Cmd").EnumerateArray().ToArray())
            .GetString());
        Assert.Equal(
            "deployment-aem-local",
            root.GetProperty("Labels").GetProperty("com.docker.compose.project").GetString());

        var hostConfig = root.GetProperty("HostConfig");
        Assert.True(hostConfig.GetProperty("AutoRemove").GetBoolean());
        Assert.Equal(2147483648, hostConfig.GetProperty("Memory").GetInt64());
        Assert.Equal(
            "4502",
            hostConfig.GetProperty("PortBindings").GetProperty("4502/tcp")
                .EnumerateArray().First().GetProperty("HostPort").GetString());
    }

    [Fact]
    public void Only_the_binds_that_need_it_move()
    {
        var document = Rewritten("""
            {"HostConfig":{"Binds":[
                "named:/a", "D:\\p:/b", "/mnt/c/already:/c"]}}
            """);

        Assert.Equal(["named:/a", "/mnt/d/p:/b", "/mnt/c/already:/c"], Binds(document));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("[1,2,3]")]
    [InlineData("""{"Image":"x"}""")]
    [InlineData("""{"HostConfig":{}}""")]
    [InlineData("""{"HostConfig":{"Binds":[]}}""")]
    [InlineData("""{"HostConfig":{"Binds":null}}""")]
    public void A_body_with_nothing_to_change_is_refused_rather_than_rebuilt(string json)
    {
        // False means "forward what the client sent". Re-serialising a payload this had no reason to
        // touch would put a JSON writer on the path of every create for no gain.
        Assert.False(ContainerCreateRewrite.TryRewrite(Body(json), out _));
    }
}
