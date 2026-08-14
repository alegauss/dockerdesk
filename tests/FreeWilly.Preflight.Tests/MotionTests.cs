using System.Windows.Shapes;
using FreeWilly.Tray.Ui;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// The motion that informs, and the three ways it is switched off (DD69, DD70).
/// </summary>
/// <remarks>
/// The capture cannot answer this one. Its fixture reports the engine as <c>Running</c> and there is
/// no fixture that reports <c>Starting</c>, so the picture never contains a breathing dot to compare
/// — which is exactly why the behaviour is asserted here rather than looked at.
/// </remarks>
[Collection(ConsoleCollection.Name)]
public sealed class MotionTests
{
    /// <summary>Run a body with motion suppressed, and put the flag back however it ends.</summary>
    private static void WhileStill(Action body)
    {
        var was = Motion.Still;
        Motion.Still = true;
        try
        {
            body();
        }
        finally
        {
            Motion.Still = was;
        }
    }

    /// <summary>
    /// Run a body on a thread WPF will talk to, and surface whatever it threw.
    /// </summary>
    /// <remarks>
    /// Constructing any <c>UIElement</c> reaches <c>InputManager</c>, which refuses off an STA
    /// thread, and xUnit's are MTA. A thread per test rather than a shared one: these assert a
    /// property on an element they made, so there is nothing to share and nothing to serialise.
    /// </remarks>
    private static void OnUiThread(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            throw new Xunit.Sdk.XunitException(failure.ToString());
        }
    }

    [Fact]
    public void A_capture_holds_the_dot_at_full_opacity_rather_than_mid_breath()
    {
        // The property the picture is made of. A dot left at the dip because an animation was
        // removed mid-cycle would read as a disabled control and would make two captures of one
        // build differ — which is the trap DD69 fell into with the water and this shares the fix for.
        OnUiThread(() =>
        {
            var dot = new Ellipse();
            WhileStill(() => Breathing.Set(dot, starting: true));
            Assert.Equal(1, dot.Opacity);
        });
    }

    [Fact]
    public void An_engine_that_is_not_starting_never_breathes()
    {
        // The motion is a second reading of one state, so it has to end when that state does —
        // otherwise it is decoration, which is the thing the window constitution refuses.
        OnUiThread(() =>
        {
            var dot = new Ellipse();
            Breathing.Set(dot, starting: false);
            Assert.Equal(1, dot.Opacity);
        });
    }

    [Fact]
    public void Stopping_restores_the_dot_from_wherever_the_breath_had_reached()
    {
        // Set by hand to the middle of a breath, which is what removing a running animation leaves
        // behind if nothing puts the value back.
        OnUiThread(() =>
        {
            var dot = new Ellipse { Opacity = 0.35 };
            Breathing.Set(dot, starting: false);
            Assert.Equal(1, dot.Opacity);
        });
    }

    [Fact]
    public void The_gate_is_one_question_asked_in_one_place()
    {
        // DD69 answered this for the water and DD70 needed it for the dot. Two surfaces disagreeing
        // about whether animation is allowed is worse than either answer: a machine with animation
        // off would have a still band and a breathing dot.
        WhileStill(() => Assert.False(Motion.Allowed));
    }
}
