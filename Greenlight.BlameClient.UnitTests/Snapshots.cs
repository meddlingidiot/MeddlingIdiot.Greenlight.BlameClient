using Greenlight.Sdk.Protocol;

namespace Greenlight.BlameClient.UnitTests;

/// <summary>
/// Builds the SDK's records without the ceremony. Both of them take a dozen positional
/// arguments, nearly all of which are irrelevant to anything being tested here, and a test
/// that spells all twelve out every time is a test nobody can read the point of.
/// </summary>
internal static class Snapshots
{
    /// <summary>A fixed moment, so that a test's arithmetic reads as arithmetic.</summary>
    public static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A completed run that failed, which is what this app exists to notice.</summary>
    public static GreenlightBuild Broken(
        long id,
        string triggeredBy = "Jamie Doe",
        DateTimeOffset? finishedAt = null,
        string pipeline = "CI",
        bool acknowledged = false) =>
        new(
            id,
            pipeline,
            "Greenlight",
            "main",
            GreenlightBuildStatus.Completed,
            GreenlightBuildResult.Failed,
            (finishedAt ?? Noon).AddMinutes(-4),
            finishedAt ?? Noon,
            triggeredBy,
            $"https://example.com/build/{id}",
            GreenlightProvider.AzureDevOps,
            acknowledged);

    /// <summary>The same run, but green.</summary>
    public static GreenlightBuild Passed(long id, string pipeline = "CI") =>
        Broken(id, pipeline: pipeline) with { Result = GreenlightBuildResult.Succeeded };

    /// <summary>A run that has not finished. Never a break, whatever it turns into later.</summary>
    public static GreenlightBuild Running(long id, string pipeline = "CI") =>
        Broken(id, pipeline: pipeline) with
        {
            Status = GreenlightBuildStatus.InProgress,
            Result = GreenlightBuildResult.Unknown,
            FinishedAt = null,
        };

    public static GreenlightSnapshot Of(params GreenlightBuild[] builds) =>
        new(
            builds.Any(b => b.Result == GreenlightBuildResult.Failed)
                ? GreenlightStatus.Red
                : GreenlightStatus.Green,
            IsBuilding: false,
            Reason: "Because a test said so.",
            Details: [],
            Builds: builds,
            PullRequests: [],
            Connections: [],
            CapturedAt: Noon,
            AppVersion: "1.0.0-test");
}
