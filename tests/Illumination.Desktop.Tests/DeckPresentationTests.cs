using Illumination.Application.ContentManagement;
using Illumination.Desktop;
using Xunit;

namespace Illumination.Desktop.Tests;

public sealed class DeckPresentationTests
{
    [Fact]
    public void Duplicate_decks_get_stable_human_labels_without_changing_identity()
    {
        var first = new DeckView(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Indo", []);
        var second = new DeckView(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "Indo", []);
        var third = new DeckView(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), "Other", []);

        var labels = DeckPresentationLabeler.Label([first, second, third]);

        Assert.Equal(["Indo", "Indo (2)", "Other"], labels.Select(x => x.DisplayName));
        Assert.Equal(second.Id, labels[1].Id);
        Assert.Same(second, labels[1].Deck);
    }
}
