using System.Text.RegularExpressions;
using FreeWilly.Core.Engine;
using FreeWilly.Core.Licensing;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// The licence is the feature, and the attribution is a compliance requirement rather than a
/// courtesy. Both are asserted here, including against the files actually committed.
/// </summary>
public sealed class AttributionTests
{
    private static string RepositoryFile(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "FreeWilly.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.True(directory is not null, "the repository root was not found above the test binaries");
        return Path.Combine(directory!.FullName, relative);
    }

    // ---- every download is attributed -----------------------------------------------------------

    [Fact]
    public void Every_artefact_the_installer_downloads_has_an_attribution_entry()
    {
        // The guard that matters. Adding a download without its attribution is the way a
        // distribution stops being compliant without anybody noticing.
        var manifest = EngineManifest.Current;
        var attributed = Attribution.Components(manifest).Select(c => c.ArtefactId).ToList();

        Assert.Equal(manifest.Artefacts.Select(a => a.Id), attributed);
    }

    [Fact]
    public void Each_entry_carries_a_version_a_licence_and_where_it_came_from()
    {
        foreach (var component in Attribution.Components(EngineManifest.Current))
        {
            Assert.False(string.IsNullOrWhiteSpace(component.Name));
            Assert.False(string.IsNullOrWhiteSpace(component.Version));
            Assert.False(string.IsNullOrWhiteSpace(component.Licence));
            Assert.StartsWith("https://", component.Origin, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(component.Note));
        }
    }

    [Fact]
    public void The_version_and_source_are_the_manifest_s_and_not_a_second_copy()
    {
        // Two places stating a version is one place stating it wrong after the next bump.
        var manifest = EngineManifest.Current;
        var components = Attribution.Components(manifest);

        foreach (var artefact in manifest.Artefacts)
        {
            var component = components.Single(c => c.ArtefactId == artefact.Id);
            Assert.Equal(artefact.Version, component.Version);
            Assert.Equal(artefact.Url, component.Origin);
        }
    }

    [Fact]
    public void The_root_filesystem_does_not_claim_one_licence_it_cannot_support()
    {
        // A minirootfs is a collection of separately licensed packages. Naming a single SPDX id
        // for it would be a claim this project has no basis for.
        var rootfs = Attribution.Components(EngineManifest.Current)
            .Single(c => c.ArtefactId == EngineManifest.Current.Rootfs.Id);

        Assert.Contains("multiple", rootfs.Licence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GPL-2.0-only", rootfs.Note, StringComparison.Ordinal);
        Assert.Contains("MIT", rootfs.Note, StringComparison.Ordinal);
    }

    [Fact]
    public void The_engine_archive_says_that_not_all_of_it_is_apache()
    {
        // docker-init is tini, under MIT. An "Apache-2.0" line with nothing under it would be
        // accurate about most of the archive and wrong about the rest.
        var engine = Attribution.Components(EngineManifest.Current)
            .Single(c => c.ArtefactId == EngineManifest.Current.Engine.Id);

        Assert.Equal("Apache-2.0", engine.Licence);
        Assert.Contains("MIT", engine.Note, StringComparison.Ordinal);
    }

    // ---- the claim is scoped honestly -------------------------------------------------------------

    [Fact]
    public void The_scope_says_what_is_this_project_s_and_what_is_not()
    {
        Assert.Contains("Apache-2.0", Attribution.Scope, StringComparison.Ordinal);
        Assert.Contains("free at any headcount", Attribution.Scope, StringComparison.Ordinal);

        // Overstating this would be the same category of surprise the project exists to remove.
        Assert.Contains("not redistributed", Attribution.Scope, StringComparison.Ordinal);
        Assert.Contains("own terms", Attribution.Scope, StringComparison.Ordinal);
    }

    // ---- what is actually committed ----------------------------------------------------------------

    [Fact]
    public void The_committed_NOTICE_is_what_the_manifest_renders()
    {
        // Generated, not hand-written: a bumped version that did not reach NOTICE is a compliance
        // gap wearing the clothes of an up-to-date file.
        var committed = File.ReadAllText(RepositoryFile("NOTICE")).ReplaceLineEndings("\n");

        Assert.Equal(Attribution.Notice(EngineManifest.Current), committed);
    }

    [Fact]
    public void The_LICENSE_file_is_the_apache_2_text()
    {
        var licence = File.ReadAllText(RepositoryFile("LICENSE"));

        Assert.Contains("Apache License", licence, StringComparison.Ordinal);
        Assert.Contains("Version 2.0, January 2004", licence, StringComparison.Ordinal);

        // The patent grant is the reason this is Apache-2.0 rather than MIT.
        Assert.Contains("3. Grant of Patent License", licence, StringComparison.Ordinal);
        Assert.Contains(Attribution.Holder, licence, StringComparison.Ordinal);
    }

    [Fact]
    public void The_README_states_the_terms_in_its_first_paragraph()
    {
        // A visitor who cannot establish the terms in the first ten seconds assumes the same trap
        // they came here to escape, so this is about position and not only presence.
        var readme = File.ReadAllText(RepositoryFile("README.md")).ReplaceLineEndings("\n");
        var firstParagraph = readme.Split("\n\n").First(part =>
            !part.TrimStart().StartsWith('#') && part.Trim().Length > 0);

        Assert.Contains("Apache-2.0", firstParagraph, StringComparison.Ordinal);
        Assert.Contains("headcount", firstParagraph, StringComparison.Ordinal);
    }

    [Fact]
    public void The_README_links_the_licence_and_the_notice()
    {
        var readme = File.ReadAllText(RepositoryFile("README.md"));

        Assert.Contains("(LICENSE)", readme, StringComparison.Ordinal);
        Assert.Contains("(NOTICE)", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void The_README_repeats_the_non_goals_the_roadmap_binds_the_project_to()
    {
        // They are binding rather than aspirational, and a reader deciding whether to adopt this
        // needs the ones that would rule it out.
        var readme = File.ReadAllText(RepositoryFile("README.md"));

        Assert.Contains("macOS and Linux", readme, StringComparison.Ordinal);
        Assert.Contains("Telemetry, accounts or a sign-in", readme, StringComparison.Ordinal);
        Assert.Contains("Feature parity with Docker Desktop", readme, StringComparison.Ordinal);
    }

    // ---- the window's About -------------------------------------------------------------------------

    [Fact]
    public void About_states_the_licence_the_freedom_and_the_version()
    {
        var about = Attribution.About(EngineManifest.Current, "1.2.3");

        Assert.Contains("FreeWilly 1.2.3", about, StringComparison.Ordinal);
        Assert.Contains("Apache-2.0", about, StringComparison.Ordinal);
        Assert.Contains("free at any headcount", about, StringComparison.Ordinal);
        Assert.Contains("patent grant", about, StringComparison.Ordinal);
    }

    [Fact]
    public void About_names_every_upstream_component_with_its_licence()
    {
        var about = Attribution.About(EngineManifest.Current, "1.2.3");

        foreach (var component in Attribution.Components(EngineManifest.Current))
        {
            Assert.Contains(component.Name, about, StringComparison.Ordinal);
            Assert.Contains(component.Version, about, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void About_and_NOTICE_cannot_disagree_because_they_read_the_same_list()
    {
        var manifest = EngineManifest.Current;
        var about = Attribution.About(manifest, "1.2.3");
        var notice = Attribution.Notice(manifest);

        foreach (var component in Attribution.Components(manifest))
        {
            Assert.Contains(component.Name, notice, StringComparison.Ordinal);
            Assert.Contains(component.Name, about, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_version_is_read_off_the_assembly_rather_than_written_down_twice()
    {
        var version = BuildVersion.Current;

        Assert.False(string.IsNullOrWhiteSpace(version));

        // The SDK appends "+<commit>"; a hash is not what somebody reads out of an About box.
        Assert.DoesNotContain('+', version);
        Assert.Matches(new Regex(@"^\d+\.\d+"), version);
    }

    // ---- the NOTICE reads as a file rather than a blob ------------------------------------------------

    [Fact]
    public void The_notice_wraps_so_it_is_readable_in_a_terminal()
    {
        var notice = Attribution.Notice(EngineManifest.Current);

        // URLs are exempt: breaking one to fit a column makes it useless.
        var tooLong = notice.Split('\n')
            .Where(line => line.Length > 96 && !line.Contains("https://", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(tooLong);
    }

    [Fact]
    public void The_notice_names_the_project_and_the_copyright_holder_first()
    {
        var lines = Attribution.Notice(EngineManifest.Current).Split('\n');

        Assert.Equal("FreeWilly", lines[0]);
        Assert.Contains(Attribution.Holder, lines[1], StringComparison.Ordinal);
    }
}
