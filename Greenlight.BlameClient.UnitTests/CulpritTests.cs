namespace Greenlight.BlameClient.UnitTests;

/// <summary>
/// The guessing that turns a build provider's idea of a name into a headline. Worth testing
/// carefully, because every way of getting it wrong ends with somebody's name spelled
/// strangely in letters a foot tall in front of their colleagues.
/// </summary>
public class CulpritTests
{
    [Theory]
    [InlineData("Jamie Doe", "Jamie Doe")]
    [InlineData("jamie.doe@example.com", "Jamie Doe")]
    [InlineData("jamie.doe", "Jamie Doe")]
    [InlineData("jamie_doe", "Jamie Doe")]
    [InlineData("JAMIE.DOE", "Jamie Doe")]
    [InlineData("jamie", "Jamie")]
    [InlineData("  Jamie Doe  ", "Jamie Doe")]
    public void Tidies_the_shapes_a_provider_actually_sends(string sent, string expected) =>
        Assert.Equal(expected, Culprit.Full(sent));

    [Fact]
    public void Takes_the_display_name_out_of_an_addressed_one()
    {
        Assert.Equal("Jamie Doe", Culprit.Full("Jamie Doe <jamie@example.com>"));
    }

    /// <summary>
    /// The one that would be genuinely rude to get wrong. A blanket title-case pass turns
    /// these into Mckenzie, O'brien and Van Der Berg on a screen the whole team is looking at.
    /// </summary>
    [Theory]
    [InlineData("Ewan McKenzie")]
    [InlineData("Anne O'Brien")]
    [InlineData("Joris van der Berg")]
    [InlineData("bell hooks")]
    public void Leaves_a_name_that_is_already_spelled_the_way_somebody_spells_it(string name) =>
        Assert.Equal(name, Culprit.Full(name));

    /// <summary>
    /// The exception to leaving display names alone: one with no lower case anywhere in it is
    /// a directory export shouting, not a person who capitalises their own name that way.
    /// </summary>
    [Fact]
    public void Quietens_a_display_name_that_arrived_in_capitals()
    {
        Assert.Equal("Jamie Doe", Culprit.Full("JAMIE DOE"));
    }

    /// <summary>
    /// A display name with punctuation in it keeps the punctuation. Only a bare handle gets
    /// its separators turned into spaces.
    /// </summary>
    [Fact]
    public void Does_not_take_a_hyphenated_name_apart()
    {
        Assert.Equal("Anna Krishnan-Weiss", Culprit.Full("Anna Krishnan-Weiss"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("@")]
    public void Says_somebody_when_the_provider_said_nothing_usable(string? sent) =>
        Assert.Equal("Somebody", Culprit.Full(sent));

    [Theory]
    [InlineData("Jamie Doe", "Jamie")]
    [InlineData("jamie.doe@example.com", "Jamie")]
    [InlineData("Jamie", "Jamie")]
    [InlineData(null, "Somebody")]
    public void The_headline_is_the_first_name(string? sent, string expected) =>
        Assert.Equal(expected, Culprit.First(sent));

    /// <summary>
    /// What the taunt book is keyed on: everything that is not a letter or a digit thrown
    /// away, so one entry finds every spelling of the same person.
    /// </summary>
    [Theory]
    [InlineData("Jamie", "jamie")]
    [InlineData("Jamie Doe", "jamiedoe")]
    [InlineData("jamie.doe@example.com", "jamiedoeexamplecom")]
    [InlineData(null, "")]
    public void Keys_flatten_a_name_for_comparison(string? sent, string expected) =>
        Assert.Equal(expected, Culprit.Key(sent));
}
