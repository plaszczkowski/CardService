using CardActions.Domain.Models;

namespace CardActions.Application.Interfaces;

public interface ICardRepository
{
    Task<Card?> GetCardAsync(UserId userId, string cardNumber, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(UserId userId, string cardNumber, CancellationToken cancellationToken = default);
    Task<bool> CardExistsAsync(string cardNumber, CancellationToken cancellationToken = default); // Add this
}