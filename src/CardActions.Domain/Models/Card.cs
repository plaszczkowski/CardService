using CardActions.Domain.Enums;
using Microsoft.VisualBasic;

namespace CardActions.Domain.Models;

public record CardId(string Value);
public record UserId(string Value);

public class Card
{
    public CardId Id { get; private set; }
    public UserId UserId { get; private set; }
    public CardType Type { get; private set; }
    public CardStatus Status { get; private set; }
    public bool IsPinSet { get; private set; }
    public string CardNumber { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public Card(CardId id, UserId userId, string cardNumber, CardType type, CardStatus status, bool isPinSet)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        UserId = userId ?? throw new ArgumentNullException(nameof(userId));
        CardNumber = cardNumber ?? throw new ArgumentNullException(nameof(cardNumber));
        Type = type;
        Status = status;
        IsPinSet = isPinSet;
        CreatedAt = DateTime.UtcNow;
    }

    public static Card CreatePrepaid(UserId userId, string cardNumber)
        => new(new CardId(Guid.NewGuid().ToString()), userId, cardNumber, CardType.Prepaid, CardStatus.Ordered, false);

    public void ChangeStatus(CardStatus newStatus)
    {
        Status = newStatus;
    }

    public void SetPin()
    {
        IsPinSet = true;
    }
}