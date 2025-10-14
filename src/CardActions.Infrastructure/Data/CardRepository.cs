using CardActions.Application.Interfaces;
using CardActions.Domain.Enums;
using CardActions.Domain.Models;

namespace CardActions.Infrastructure.Data;

public class CardRepository : ICardRepository
{
    private readonly Dictionary<string, Dictionary<string, Card>> _userCards;

    public CardRepository()
    {
        _userCards = CreateSampleUserCards();
    }

    public Task<Card?> GetCardAsync(UserId userId, string cardNumber, CancellationToken cancellationToken = default)
    {
        // First, find the card across all users
        var card = FindCardByNumber(cardNumber);

        if (card is null)
        {
            return Task.FromResult<Card?>(null);
        }

        // Then check if it belongs to the requested user
        if (!card.UserId.Equals(userId))
        {
            // Return null if card exists but belongs to different user
            // This will trigger 404 in service, but we want 403
            // So we need to change the service logic
            return Task.FromResult<Card?>(null);
        }

        return Task.FromResult<Card?>(card);
    }

    public Task<bool> ExistsAsync(UserId userId, string cardNumber, CancellationToken cancellationToken = default)
    {
        var exists = _userCards.TryGetValue(userId.Value, out var userCards) &&
                    userCards.ContainsKey(cardNumber);
        return Task.FromResult(exists);
    }

    // Add this method to find card by number across all users
    private Card? FindCardByNumber(string cardNumber)
    {
        foreach (var userCards in _userCards.Values)
        {
            if (userCards.TryGetValue(cardNumber, out var card))
            {
                return card;
            }
        }
        return null;
    }

    // Add this method to check if card exists for any user
    public Task<bool> CardExistsAsync(string cardNumber, CancellationToken cancellationToken = default)
    {
        var exists = FindCardByNumber(cardNumber) != null;
        return Task.FromResult(exists);
    }

    private static Dictionary<string, Dictionary<string, Card>> CreateSampleUserCards()
    {
        var userCards = new Dictionary<string, Dictionary<string, Card>>();

        for (var i = 1; i <= 3; i++)
        {
            var cards = new Dictionary<string, Card>();
            var userId = new UserId($"User{i}");
            var cardIndex = 1;

            // Specific test cases from requirements
            cards.Add("PREPAID_CLOSED",
                new Card(new CardId(Guid.NewGuid().ToString()), userId, "PREPAID_CLOSED",
                        CardType.Prepaid, CardStatus.Closed, false));

            cards.Add("CREDIT_BLOCKED_PIN",
                new Card(new CardId(Guid.NewGuid().ToString()), userId, "CREDIT_BLOCKED_PIN",
                        CardType.Credit, CardStatus.Blocked, true));

            cards.Add("CREDIT_BLOCKED_NO_PIN",
                new Card(new CardId(Guid.NewGuid().ToString()), userId, "CREDIT_BLOCKED_NO_PIN",
                        CardType.Credit, CardStatus.Blocked, false));

            // Generate comprehensive test data
            foreach (CardType cardType in Enum.GetValues(typeof(CardType)))
            {
                foreach (CardStatus cardStatus in Enum.GetValues(typeof(CardStatus)))
                {
                    var cardNumber = $"Card{i}{cardIndex}";
                    var cardId = new CardId(Guid.NewGuid().ToString());
                    var card = new Card(cardId, userId, cardNumber, cardType, cardStatus,
                                      cardIndex % 2 == 0);
                    cards.Add(cardNumber, card);
                    cardIndex++;
                }
            }

            userCards.Add(userId.Value, cards);
        }

        return userCards;
    }
}