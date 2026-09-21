namespace Greenlight.BlameClient.UnitTests;

/// <summary>
/// Who gets ridiculed, and with what. The matching is the interesting part: an entry filed
/// under one spelling of a name has to find every other spelling, or somebody carefully adds
/// five lines for a colleague and never sees any of them.
/// </summary>
public class TauntBookTests
{
    private static TauntBook Book(params string[] jamie) =>
        new(new Dictionary<string, List<string>> { ["Jamie"] = [.. jamie] }, ["Bad luck."]);

    [Fact]
    public void Gives_somebody_their_own_lines()
    {
        Assert.Equal("Again, Jamie.", Book("Again, Jamie.").Next("Jamie"));
    }

    [Theory]
    [InlineData("Jamie")]
    [InlineData("jamie")]
    [InlineData("JAMIE")]
    [InlineData("Jamie Doe")]
    [InlineData("jamie.doe@example.com")]
    public void Finds_them_however_the_provider_spelled_it(string who)
    {
        Assert.Equal("Again, Jamie.", Book("Again, Jamie.").Next(who));
    }

    [Fact]
    public void Everybody_else_gets_the_general_lines()
    {
        Assert.Equal("Bad luck.", Book("Again, Jamie.").Next("Priya Raman"));
    }

    /// <summary>
    /// A full-name entry beats a first-name one, so a team with two Jamies can single one of
    /// them out without the other inheriting it.
    /// </summary>
    [Fact]
    public void The_more_specific_entry_wins()
    {
        var book = new TauntBook(
            new Dictionary<string, List<string>>
            {
                ["Jamie"] = ["Some Jamie."],
                ["Jamie Doe"] = ["That Jamie."],
            },
            ["Bad luck."]);

        Assert.Equal("That Jamie.", book.Next("Jamie Doe"));
        Assert.Equal("Some Jamie.", book.Next("Jamie Roe"));
    }

    [Fact]
    public void Rotates_rather_than_repeating()
    {
        var book = Book("One.", "Two.", "Three.");

        Assert.Equal("One.", book.Next("Jamie"));
        Assert.Equal("Two.", book.Next("Jamie"));
        Assert.Equal("Three.", book.Next("Jamie"));
        Assert.Equal("One.", book.Next("Jamie"));
    }

    /// <summary>
    /// Each person keeps their own place in their own list. Sharing a cursor would mean
    /// somebody else breaking the build in between changed which line you got.
    /// </summary>
    [Fact]
    public void Keeps_a_separate_place_for_each_person()
    {
        var book = new TauntBook(
            new Dictionary<string, List<string>>
            {
                ["Jamie"] = ["Jamie one.", "Jamie two."],
                ["Priya"] = ["Priya one.", "Priya two."],
            },
            ["Bad luck."]);

        Assert.Equal("Jamie one.", book.Next("Jamie"));
        Assert.Equal("Priya one.", book.Next("Priya"));
        Assert.Equal("Jamie two.", book.Next("Jamie"));
        Assert.Equal("Priya two.", book.Next("Priya"));
    }

    [Fact]
    public void Says_something_when_the_file_has_been_emptied()
    {
        var book = new TauntBook(null, null);

        Assert.Contains(book.Next("Jamie"), TauntBook.DefaultGeneral);
    }

    /// <summary>
    /// A name with nothing but blank strings against it is somebody halfway through editing
    /// the file, not an instruction to draw an empty line under the headline.
    /// </summary>
    [Fact]
    public void Ignores_an_entry_with_nothing_in_it()
    {
        var book = new TauntBook(
            new Dictionary<string, List<string>> { ["Jamie"] = ["", "   "] },
            ["Bad luck."]);

        Assert.Equal("Bad luck.", book.Next("Jamie"));
        Assert.False(book.KnowsAbout("Jamie"));
    }

    [Fact]
    public void Says_whether_anybody_has_written_something_for_a_person()
    {
        var book = Book("Again, Jamie.");

        Assert.True(book.KnowsAbout("jamie.doe@example.com"));
        Assert.False(book.KnowsAbout("Priya Raman"));
    }

    [Fact]
    public void Never_returns_null_for_a_name_that_is_not_one()
    {
        var book = Book("Again, Jamie.");

        Assert.False(string.IsNullOrWhiteSpace(book.Next(null)));
        Assert.False(string.IsNullOrWhiteSpace(book.Next("")));
    }

    /// <summary>
    /// Nobody is singled out by default. A name baked into the shipped lines would be a real
    /// person's name in a public repository; the personal block is the team's to fill in.
    /// </summary>
    [Fact]
    public void Picks_on_nobody_out_of_the_box()
    {
        var book = new BlameConfig().Book();

        Assert.Empty(TauntBook.DefaultPersonal);
        Assert.False(book.KnowsAbout("Jamie Doe"));
        Assert.Contains(book.Next("Jamie Doe").Replace("Jamie", TauntBook.NamePlaceholder),
            TauntBook.DefaultGeneral);
    }

    /// <summary>
    /// The general lines are personal anyway: <c>{name}</c> becomes the first name off the
    /// snapshot, the same word the headline shouts.
    /// </summary>
    [Theory]
    [InlineData("Jamie Doe")]
    [InlineData("jamie.doe@example.com")]
    [InlineData("JAMIE.DOE")]
    public void Fills_in_the_first_name(string who)
    {
        var book = new TauntBook(null, ["Red is your colour, {name}."]);

        Assert.Equal("Red is your colour, Jamie.", book.Next(who));
    }

    [Fact]
    public void Fills_in_the_name_however_the_placeholder_was_typed()
    {
        var book = new TauntBook(null, ["{NAME}, {name} and {Name}."]);

        Assert.Equal("Jamie, Jamie and Jamie.", book.Next("Jamie"));
    }

    /// <summary>
    /// A pipeline triggered by another pipeline arrives with no name at all. The line still has
    /// to read as a sentence rather than as "Red is your colour, ."
    /// </summary>
    [Fact]
    public void Fills_in_somebody_when_there_is_no_name()
    {
        var book = new TauntBook(null, ["Red is your colour, {name}."]);

        Assert.Equal($"Red is your colour, {Culprit.Nobody}.", book.Next(null));
    }

    [Fact]
    public void Fills_in_the_name_in_personal_lines_too()
    {
        var book = new TauntBook(
            new Dictionary<string, List<string>> { ["Jamie"] = ["Again, {name}."] },
            ["Bad luck."]);

        Assert.Equal("Again, Jamie.", book.Next("jamie.doe@example.com"));
    }
}
