using CardActions.Domain.Enums;
using CardActions.Domain.Models;
using CardActions.Domain.Services;
using FluentAssertions;
using Xunit;

namespace CardActions.UnitTests;

[Trait("Category", "Unit")]
[Trait("Feature", "CardActionPolicy")]
public class CardActionPolicyTests
{
    private readonly CardActionPolicy _policy = new();
    private static readonly string[] unexpected = ["ACTION6", "ACTION7"];
    private static readonly string[] expected = ["ACTION3", "ACTION4", "ACTION5", "ACTION8", "ACTION9"];
    private static readonly string[] expectedArray = ["ACTION3", "ACTION4", "ACTION5", "ACTION6", "ACTION7", "ACTION8", "ACTION9"];
    private static readonly string[] expectation = ["ACTION3", "ACTION4", "ACTION9"];

    [Fact]
    public void PrepaidClosed_ShouldReturnOnly_Action3_4_9()
    {
        var card = new Card(new CardId("1"), new UserId("U"), "PREPAID_CLOSED", CardType.Prepaid, CardStatus.Closed, false);
        var actions = _policy.GetAllowedActions(card);
        actions.Should().BeEquivalentTo(expectation);
    }

    [Fact]
    public void CreditBlocked_WithPin_ShouldReturn_AllActionsIncluding_6_7()
    {
        var card = new Card(new CardId("2"), new UserId("U"), "CREDIT_BLOCKED_PIN", CardType.Credit, CardStatus.Blocked, true);
        var actions = _policy.GetAllowedActions(card);
        actions.Should().Contain(expectedArray);
    }

    [Fact]
    public void CreditBlocked_WithoutPin_ShouldNotReturn_Action6_7()
    {
        var card = new Card(new CardId("3"), new UserId("U"), "CREDIT_BLOCKED_NO_PIN", CardType.Credit, CardStatus.Blocked, false);
        var actions = _policy.GetAllowedActions(card);
        actions.Should().Contain(expected);
        actions.Should().NotContain(unexpected);
    }

    [Theory]
    [InlineData(CardStatus.Ordered, true, true, false)]
    [InlineData(CardStatus.Ordered, false, false, true)]
    [InlineData(CardStatus.Inactive, true, true, false)]
    [InlineData(CardStatus.Inactive, false, false, true)]
    [InlineData(CardStatus.Active, true, true, false)]
    [InlineData(CardStatus.Active, false, false, true)]
    [InlineData(CardStatus.Blocked, true, true, true)]
    [InlineData(CardStatus.Blocked, false, false, false)]
    [InlineData(CardStatus.Restricted, true, false, false)]
    [InlineData(CardStatus.Restricted, false, false, false)]
    [InlineData(CardStatus.Expired, true, false, false)]
    [InlineData(CardStatus.Expired, false, false, false)]
    [InlineData(CardStatus.Closed, true, false, false)]
    [InlineData(CardStatus.Closed, false, false, false)]
    [Trait("Enhancement", "MatrixValidation")]
    public void Action6_7_MatrixValidation(CardStatus status, bool isPinSet, bool expectA6, bool expectA7)
    {
        var card = new Card(new CardId("X"), new UserId("U"), $"CARD_{status}_{isPinSet}", CardType.Debit, status, isPinSet);
        var actions = _policy.GetAllowedActions(card);
        actions.Contains("ACTION6").Should().Be(expectA6);
        actions.Contains("ACTION7").Should().Be(expectA7);
    }

    [Theory]
    [InlineData(CardStatus.Ordered, true)]
    [InlineData(CardStatus.Inactive, true)]
    [InlineData(CardStatus.Active, true)]
    [InlineData(CardStatus.Blocked, true)]
    [InlineData(CardStatus.Restricted, false)]
    [InlineData(CardStatus.Expired, false)]
    [InlineData(CardStatus.Closed, false)]
    public void Action8_StatusExclusion(CardStatus status, bool shouldBeAllowed)
    {
        var card = new Card(new CardId("X"), new UserId("U"), "CARD_TEST", CardType.Prepaid, status, false);
        var actions = _policy.GetAllowedActions(card);
        actions.Contains("ACTION8").Should().Be(shouldBeAllowed);
    }

    [Theory]
    [InlineData(CardStatus.Ordered)]
    [InlineData(CardStatus.Inactive)]
    [InlineData(CardStatus.Active)]
    [InlineData(CardStatus.Restricted)]
    [InlineData(CardStatus.Blocked)]
    [InlineData(CardStatus.Expired)]
    [InlineData(CardStatus.Closed)]
    public void Action9_ShouldAlwaysBeAllowed(CardStatus status)
    {
        var card = new Card(new CardId("X"), new UserId("U"), $"CARD_{status}", CardType.Debit, status, false);
        var actions = _policy.GetAllowedActions(card);
        actions.Should().Contain("ACTION9");
    }
}
