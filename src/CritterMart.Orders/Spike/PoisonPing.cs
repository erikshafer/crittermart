using Microsoft.AspNetCore.Http;
using Wolverine.Http;

namespace CritterMart.Orders.Spike;

// ════════════════════════════════════════════════════════════════════════════════════════════════
//  CW-TELEMETRY SPIKE (research/cw-telemetry-spike) — NOT round-one baseline. On-demand only.
//  See docs/research/cw-telemetry-fodder.md.
// ════════════════════════════════════════════════════════════════════════════════════════════════

// A self-contained POISON message for exercising the two CritterWatch surfaces a healthy system
// never populates: the Dead Letters tab and the Projection-Statuses "Error" column (always `—` on
// the baseline). The handler always throws; Wolverine exhausts its attempts and moves the message to
// the durable dead-letter store the console reads. A DEDICATED message type so it never corrupts a
// real domain stream — the failure is isolated and repeatable.
public record PoisonPing(string Note);

public static class PoisonPingHandler
{
    // Always throws. With no matching retry policy Wolverine dead-letters it — exactly the signal
    // we want CritterWatch to surface.
    public static void Handle(PoisonPing message) =>
        throw new InvalidOperationException(
            $"CW-telemetry spike: intentional poison message — {message.Note}");
}

public static class PoisonEndpoint
{
    // Cascade a PoisonPing onto the bus. Wolverine.Http treats the IResult as the HTTP response and
    // publishes the tuple's message member; the local handler above then throws and dead-letters it.
    [WolverinePost("/spike/poison")]
    public static (IResult, PoisonPing) Post() =>
        (Results.Accepted(), new PoisonPing($"triggered at {DateTimeOffset.UtcNow:O}"));
}
