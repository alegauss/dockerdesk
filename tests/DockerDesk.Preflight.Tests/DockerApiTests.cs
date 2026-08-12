using DockerDesk.Core.Api;
using Xunit;

namespace DockerDesk.Preflight.Tests;

/// <summary>
/// The client, over a real named pipe. The happy path is one test; the rest are the answers a
/// daemon actually gives when something is wrong, because those are what a UI has to render.
/// </summary>
public sealed class DockerApiTests
{
    private const string VersionJson = """
        {"Platform":{"Name":"Docker Engine - Community"},"Version":"29.7.2",
         "ApiVersion":"1.55","MinAPIVersion":"1.24","Os":"linux","Arch":"amd64",
         "KernelVersion":"6.6.87.2","GitCommit":"6a43e3d"}
        """;

    private const string ContainersJson = """
        [{"Id":"a1b2c3d4e5f60718293a4b5c6d7e8f90","Names":["/hopeful_wozniak"],
          "Image":"nginx:alpine","State":"running","Status":"Up 3 minutes",
          "Ports":[{"IP":"0.0.0.0","PrivatePort":80,"PublicPort":8080,"Type":"tcp"},
                   {"PrivatePort":443,"Type":"tcp"}]},
         {"Id":"ff00112233445566778899aabbccddee","Names":[],
          "Image":"hello-world","State":"exited","Status":"Exited (0) 2 minutes ago",
          "Ports":[]}]
        """;

    /// <summary>What a real daemon returns for one `-p 8099:80`: the same port, twice.</summary>
    private const string DualStackJson = """
        [{"Id":"d7c06c1092c59ff199799c6e94da3f4273f51cf4d9ac6ef44a8c496df175b562",
          "Names":["/dd4-nginx"],"Image":"nginx:alpine","State":"running","Status":"Up 1 second",
          "Ports":[{"IP":"0.0.0.0","PrivatePort":80,"PublicPort":8099,"Type":"tcp"},
                   {"IP":"::","PrivatePort":80,"PublicPort":8099,"Type":"tcp"}]}]
        """;

    private static string Path(string endpoint) => $"/{DockerApi.ApiVersion}/{endpoint}";

    [Fact]
    public async Task A_port_published_on_both_address_families_is_shown_once()
    {
        // Measured against a real engine: `-p 8099:80` came back as two entries and printed as
        // "8099->80/tcp, 8099->80/tcp". The fixture above is that response.
        await using var daemon = new FakeDockerDaemon()
            .Json(Path("containers/json?all=1"), DualStackJson);
        using var api = new DockerApi(daemon.PipeName);

        var container = (await api.ContainersAsync())[0];

        Assert.Equal(2, container.Ports.Count);
        Assert.Equal(["8099->80/tcp"], container.PublishedPorts);
    }


    // ---- the happy path -----------------------------------------------------------------------

    [Fact]
    public async Task Version_is_read_off_a_real_pipe()
    {
        await using var daemon = new FakeDockerDaemon().Json(Path("version"), VersionJson);
        using var api = new DockerApi(daemon.PipeName);

        var version = await api.VersionAsync();

        Assert.Equal("29.7.2", version.Version);
        Assert.Equal("1.55", version.ApiVersion);
        Assert.Equal("1.24", version.MinApiVersion);
        Assert.Equal("linux", version.Os);
        Assert.Equal("amd64", version.Arch);
    }

    [Fact]
    public async Task Every_request_carries_the_pinned_api_version()
    {
        // Unversioned paths are answered with the daemon's newest, so an engine upgrade could change
        // a response shape under a client that asked for nothing.
        await using var daemon = new FakeDockerDaemon().Json(Path("version"), VersionJson);
        using var api = new DockerApi(daemon.PipeName);

        await api.VersionAsync();

        Assert.Contains(daemon.Requested, line =>
            line.Contains($"/{DockerApi.ApiVersion}/version", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Containers_are_read_with_their_names_state_and_ports()
    {
        await using var daemon = new FakeDockerDaemon()
            .Json(Path("containers/json?all=1"), ContainersJson);
        using var api = new DockerApi(daemon.PipeName);

        var containers = await api.ContainersAsync();

        Assert.Equal(2, containers.Count);
        var running = containers[0];
        Assert.Equal("hopeful_wozniak", running.DisplayName);
        Assert.Equal("a1b2c3d4e5f6", running.ShortId);
        Assert.Equal("nginx:alpine", running.Image);
        Assert.Equal("running", running.State);
        Assert.Equal(["8080->80/tcp", "443/tcp"], running.Ports.Select(p => p.ToString()));
    }

    [Fact]
    public async Task A_container_with_no_name_falls_back_to_its_short_id()
    {
        await using var daemon = new FakeDockerDaemon()
            .Json(Path("containers/json?all=1"), ContainersJson);
        using var api = new DockerApi(daemon.PipeName);

        var containers = await api.ContainersAsync();

        Assert.Equal("ff0011223344", containers[1].DisplayName);
        Assert.Empty(containers[1].Ports);
    }

    [Fact]
    public async Task Asking_for_running_containers_only_leaves_the_all_flag_off()
    {
        await using var daemon = new FakeDockerDaemon()
            .Json(Path("containers/json"), "[]");
        using var api = new DockerApi(daemon.PipeName);

        await api.ContainersAsync(all: false);

        Assert.DoesNotContain(daemon.Requested, line =>
            line.Contains("all=1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ping_is_true_when_the_engine_answers()
    {
        await using var daemon = new FakeDockerDaemon().Json(Path("_ping"), "OK");
        using var api = new DockerApi(daemon.PipeName);

        Assert.True(await api.PingAsync());
    }

    // ---- what a daemon says when something is wrong ------------------------------------------

    [Fact]
    public async Task Ping_is_false_when_no_pipe_is_there_rather_than_throwing()
    {
        // The tray asks this on a timer. An exception per tick for a stopped engine is not an
        // answer, it is noise.
        using var api = new DockerApi($"dockerdesk-absent-{Guid.NewGuid():N}", TimeSpan.FromSeconds(3));

        Assert.False(await api.PingAsync());
    }

    [Fact]
    public async Task A_call_to_an_absent_engine_names_the_endpoint_it_could_not_reach()
    {
        using var api = new DockerApi($"dockerdesk-absent-{Guid.NewGuid():N}", TimeSpan.FromSeconds(3));

        var thrown = await Assert.ThrowsAsync<DockerApiException>(() => api.VersionAsync());

        Assert.Contains("did not answer", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("version", thrown.Message, StringComparison.Ordinal);
        Assert.Null(thrown.Status);
    }

    [Fact]
    public async Task A_refusal_carries_the_status_and_the_sentence_the_daemon_put_in_the_body()
    {
        await using var daemon = new FakeDockerDaemon()
            .Fails(Path("version"), "500 Internal Server Error", "something went wrong in the daemon");
        using var api = new DockerApi(daemon.PipeName);

        var thrown = await Assert.ThrowsAsync<DockerApiException>(() => api.VersionAsync());

        Assert.Equal(System.Net.HttpStatusCode.InternalServerError, thrown.Status);
        Assert.Contains("something went wrong in the daemon", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_endpoint_this_daemon_does_not_have_is_a_404_and_not_a_crash()
    {
        // An engine older than the pinned API version answers exactly like this.
        await using var daemon = new FakeDockerDaemon();
        using var api = new DockerApi(daemon.PipeName);

        var thrown = await Assert.ThrowsAsync<DockerApiException>(() => api.VersionAsync());

        Assert.Equal(System.Net.HttpStatusCode.NotFound, thrown.Status);
    }

    [Fact]
    public async Task A_body_that_is_not_the_json_expected_is_reported_as_that()
    {
        // A proxy or a captive portal answering on the pipe's behalf is far-fetched; a daemon
        // changing a shape is not. Either way "unexpected character" alone tells a user nothing.
        await using var daemon = new FakeDockerDaemon()
            .Json(Path("version"), "this is not json");
        using var api = new DockerApi(daemon.PipeName);

        var thrown = await Assert.ThrowsAsync<DockerApiException>(() => api.VersionAsync());

        Assert.Contains("not the JSON expected", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_streaming_endpoint_hands_back_a_body_that_is_read_as_it_arrives()
    {
        await using var daemon = new FakeDockerDaemon()
            .Json(Path("events"), "{\"status\":\"start\"}\n{\"status\":\"die\"}\n");
        using var api = new DockerApi(daemon.PipeName);

        await using var stream = await api.StreamAsync("events");
        using var reader = new StreamReader(stream);

        Assert.Equal("{\"status\":\"start\"}", await reader.ReadLineAsync());
        Assert.Equal("{\"status\":\"die\"}", await reader.ReadLineAsync());
    }

    [Fact]
    public async Task A_streaming_endpoint_that_is_refused_throws_before_handing_back_a_stream()
    {
        await using var daemon = new FakeDockerDaemon()
            .Fails(Path("events"), "400 Bad Request", "bad filter");
        using var api = new DockerApi(daemon.PipeName);

        var thrown = await Assert.ThrowsAsync<DockerApiException>(() => api.StreamAsync("events"));

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, thrown.Status);
        Assert.Contains("bad filter", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Several_calls_in_a_row_work_on_one_client()
    {
        await using var daemon = new FakeDockerDaemon()
            .Json(Path("version"), VersionJson)
            .Json(Path("containers/json?all=1"), ContainersJson)
            .Json(Path("_ping"), "OK");
        using var api = new DockerApi(daemon.PipeName);

        Assert.True(await api.PingAsync());
        Assert.Equal("29.7.2", (await api.VersionAsync()).Version);
        Assert.Equal(2, (await api.ContainersAsync()).Count);
        Assert.Equal("29.7.2", (await api.VersionAsync()).Version);

        Assert.Equal(4, daemon.Requested.Count);
    }
}
