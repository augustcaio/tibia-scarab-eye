using TibiaScarabEye.Interop;
using Xunit;

namespace TibiaScarabEye.Tests;

public class TibiaLocatorTests
{
    [Theory]
    [InlineData("Tibia - Santizza", true, "Santizza")]
    [InlineData("Tibia - Nome com espaco", true, "Nome com espaco")]
    [InlineData("Tibia", true, "")]
    [InlineData("Tibia - ", true, "")]
    [InlineData("tibia - Santizza", false, "")]
    [InlineData("Tibia Scarab Eye", false, "")]
    [InlineData("Editor de áreas • Tibia Scarab Eye", false, "")]
    [InlineData("Wiki - Tibia - Google Chrome", false, "")]
    [InlineData("", false, "")]
    [InlineData(null, false, "")]
    public void ParseTitle_ReadsTheCharacterFromTheClientTitle(string title, bool expected, string character)
    {
        Assert.Equal(expected, TibiaLocator.ParseTitle(title, out string parsed));
        Assert.Equal(character, parsed);
    }

    [Theory]
    [InlineData("client", true)]
    [InlineData("Client", true)]
    [InlineData("chrome", false)]
    [InlineData("client_launcher", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsClientProcess_OnlyAcceptsTheTibiaClient(string process, bool expected)
    {
        Assert.Equal(expected, TibiaLocator.IsClientProcess(process));
    }
}
