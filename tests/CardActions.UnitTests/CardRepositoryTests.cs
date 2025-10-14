using CardActions.Domain.Enums;
using CardActions.Domain.Models;
using CardActions.Infrastructure.Data;
using FluentAssertions;
using Xunit;

namespace CardActions.UnitTests;

[Trait("Category", "Unit")]
[Trait("Feature", "CardRepository")]
public class CardRepositoryTests
{
    private readonly CardRepository _repository = new();

    [Fact]
    public async Task GetCardAsync_WithValidUserAndCard_ReturnsCard()
    {
        var card = await _repository.GetCardAsync(new UserId("User1"), "PREPAID_CLOSED");
        card.Should().NotBeNull();
        card!.CardNumber.Should().Be("PREPAID_CLOSED");
        card.UserId.Value.Should().Be("User1");
    }

    [Fact]
    public async Task GetCardAsync_WithNonExistentCard_ReturnsNull()
    {
        var card = await _repository.GetCardAsync(new UserId("User1"), "NON_EXISTENT_CARD");
        card.Should().BeNull();
    }

    [Fact]
    public async Task GetCardAsync_WithWrongUser_ReturnsNull()
    {
        var card = await _repository.GetCardAsync(new UserId("User1"), "Card21");
        card.Should().BeNull("because the card belongs to a different user");
    }

    [Fact]
    public async Task CardExistsAsync_WithExistingCard_ReturnsTrue()
    {
        var exists = await _repository.CardExistsAsync("PREPAID_CLOSED");
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task CardExistsAsync_WithNonExistentCard_ReturnsFalse()
    {
        var exists = await _repository.CardExistsAsync("NON_EXISTENT_CARD");
        exists.Should().BeFalse();
    }

    [Theory]
    [InlineData("User1", "PREPAID_CLOSED", CardType.Prepaid, CardStatus.Closed)]
    [InlineData("User1", "CREDIT_BLOCKED_PIN", CardType.Credit, CardStatus.Blocked)]
    [InlineData("User1", "CREDIT_BLOCKED_NO_PIN", CardType.Credit, CardStatus.Blocked)]
    public async Task GetCardAsync_ValidatesCardProperties(string userId, string cardNumber, CardType expectedType, CardStatus expectedStatus)
    {
        var card = await _repository.GetCardAsync(new UserId(userId), cardNumber);
        card.Should().NotBeNull();
        card!.Type.Should().Be(expectedType);
        card.Status.Should().Be(expectedStatus);
    }

    [Fact]
    [Trait("Enhancement", "TwoStageAuthorization")]
    public async Task TwoStageAuthorization_CardExistsGlobally_ButNotForRequestingUser()
    {
        var exists = await _repository.CardExistsAsync("Card21");
        var card = await _repository.GetCardAsync(new UserId("User1"), "Card21");

        exists.Should().BeTrue();
        card.Should().BeNull("because card does not belong to the requesting user");
    }

    [Theory]
    [Trait("Enhancement", "AuthorizationMatrix")]
    [InlineData("User1", "PREPAID_CLOSED", true)]
    [InlineData("User1", "Card21", false)]
    [InlineData("User2", "Card21", true)]
    [InlineData("User2", "PREPAID_CLOSED", false)]
    [InlineData("User3", "Card31", true)]
    [InlineData("User3", "Card21", false)]
    public async Task GetCardAsync_AuthorizationMatrix_ValidatesOwnership(string userId, string cardNumber, bool shouldReturnCard)
    {
        var card = await _repository.GetCardAsync(new UserId(userId), cardNumber);

        if (shouldReturnCard)
        {
            card.Should().NotBeNull($"because {cardNumber} belongs to {userId}");
            card!.UserId.Value.Should().Be(userId);
        }
        else
        {
            card.Should().BeNull($"because {cardNumber} does not belong to {userId}");
        }
    }

    [Fact]
    [Trait("Enhancement", "TwoStageAuthorization")]
    public async Task GetCardAsync_TwoStageAuthorization_ValidatesExistenceThenOwnership()
    {
        var exists = await _repository.CardExistsAsync("Card21");
        var card = await _repository.GetCardAsync(new UserId("User1"), "Card21");

        exists.Should().BeTrue();
        card.Should().BeNull("because card does not belong to the requesting user");
    }
}
