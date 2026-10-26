using StoryCups.Contracts.Profile;

namespace StoryCups.UnitTests.Contracts;

public sealed class ProfileDtoTests
{
    [Theory]
    [InlineData("Мария Иванова", "МИ")]
    [InlineData("мария", "М")]
    [InlineData("  Иван   Петров  Георгиев ", "ИГ")]
    [InlineData("   ", "?")]
    public void Initials_UseFirstAndLastWord(string displayName, string expected)
    {
        var profile = new ProfileDto(Guid.NewGuid(), "x@example.com", displayName, []);

        Assert.Equal(expected, profile.Initials);
    }
}
