using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// The one collection every test that touches the process's console belongs to (DD64).
/// </summary>
/// <remarks>
/// <c>Console.Out</c> is process-global, and xUnit runs test classes in parallel. Three classes here
/// capture it by swapping it and restoring it in a <c>finally</c>, and a fourth drives a verb that
/// writes to it without capturing anything — so two of them running at once interleave in ways that
/// have nothing to do with what either is asserting.
///
/// <para><b>Measured, not theorised.</b> A full suite went red on
/// <c>An_unknown_argument_is_refused_rather_than_dropped(--plan --nonsense)</c> in two runs out of
/// five, and all five cases of that theory passed in isolation every time. The verb had written its
/// refusal; the writer it landed in was not the one the assertion read, because another class's
/// <c>finally</c> had restored <c>Console.Out</c> over the capture in between.</para>
///
/// <para>A collection is the fix rather than a lock, because a lock would still let a class that
/// merely <em>writes</em> to the console — <see cref="WindowCaptureTests"/>, which asserts on exit
/// codes and captures nothing — land its output inside somebody else's capture and inflate a token
/// estimate. Membership of this collection is the rule: touch the console, join it.</para>
///
/// <para>The cost is that these four classes run one at a time. They are refusal tests measured in
/// milliseconds, so what is lost is nothing next to a gate that cries wolf.</para>
/// </remarks>
[CollectionDefinition(Name)]
public sealed class ConsoleCollection
{
    /// <summary>The collection name, so no class spells the string twice.</summary>
    public const string Name = "console";
}
