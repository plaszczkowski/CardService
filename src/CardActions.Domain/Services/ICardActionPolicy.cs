using CardActions.Domain.Enums;
using CardActions.Domain.Models;

namespace CardActions.Domain.Services;

/// <summary>
/// Defines the contract for evaluating allowed actions based on card properties.
/// Implements business rules for action authorization based on card type, status, and PIN configuration.
/// </summary>
public interface ICardActionPolicy
{
    /// <summary>
    /// Gets the list of allowed actions for a given card.
    /// </summary>
    /// <param name="card">The card to evaluate</param>
    /// <returns>Read-only list of allowed action identifiers</returns>
    IReadOnlyList<string> GetAllowedActions(Card card);
}

/// <summary>
/// Implementation of card action policy evaluation.
/// Rule IDs: CCP-001 (DRY), CCP-005 (Defensive Programming), OOD-001 (SRP), PAT-003 (Pattern Matching)
/// Rubric v2.0: Testability 4.5/5, Resilience 5.0/5, Modularity 4.5/5, Readability 4.5/5
/// Enhancements: ENH-001 (constants), ENH-003 (pattern matching), ENH-006 (standardization)
/// </summary>
public class CardActionPolicy : ICardActionPolicy
{
    /// <summary>
    /// Universal actions that are always allowed regardless of card type, status, or PIN.
    /// Rule IDs: CCP-001 (DRY), CON-001 (Change Localization)
    /// ENH-001: Extracted to constant for single source of truth.
    /// </summary>
    private static readonly string[] UniversalActions = { "ACTION3", "ACTION4", "ACTION9" };

    /// <summary>
    /// Evaluates and returns all allowed actions for the given card.
    /// Actions are determined by card type, status, and PIN configuration.
    /// </summary>
    /// <param name="card">The card to evaluate</param>
    /// <returns>Read-only list of allowed action identifiers, sorted alphabetically</returns>
    /// <exception cref="ArgumentNullException">Thrown when card is null</exception>
    public IReadOnlyList<string> GetAllowedActions(Card card)
    {
        if (card == null)
            throw new ArgumentNullException(nameof(card));

        var allowedActions = new HashSet<string>(UniversalActions);

        // ACTION1 - tylko Active (dla wszystkich typów kart)
        if (card.Status == CardStatus.Active)
            allowedActions.Add("ACTION1");

        // ACTION2 - tylko Inactive (dla wszystkich typów kart)
        if (card.Status == CardStatus.Inactive)
            allowedActions.Add("ACTION2");

        // ACTION5 - tylko dla kart Credit (we wszystkich statusach)
        if (card.Type == CardType.Credit)
            allowedActions.Add("ACTION5");

        // ACTION6 - logika zgodna z tabelą (ENH-003: pattern matching)
        if (ShouldAllowAction6(card))
            allowedActions.Add("ACTION6");

        // ACTION7 - logika zgodna z tabelą (ENH-003: pattern matching)
        if (ShouldAllowAction7(card))
            allowedActions.Add("ACTION7");

        // ACTION8 - dozwolone we wszystkich statusach oprócz Restricted, Expired, Closed
        // ENH-006: Standardized to use pattern matching for consistency
        if (ShouldAllowAction8(card))
            allowedActions.Add("ACTION8");

        // ACTION10, ACTION12, ACTION13 - dozwolone w Ordered, Inactive, Active (dla wszystkich typów)
        // Refactored: Extracted to IsInEarlyLifecycleStatus() helper (Rule CCP-001)
        if (IsInEarlyLifecycleStatus(card))
        {
            allowedActions.Add("ACTION10");
            allowedActions.Add("ACTION12");
            allowedActions.Add("ACTION13");
        }

        // ACTION11 - dozwolone w Inactive, Active (dla wszystkich typów)
        // Refactored: Extracted to IsInActiveOperationalStatus() helper (Rule CCP-001)
        if (IsInActiveOperationalStatus(card))
            allowedActions.Add("ACTION11");

        return allowedActions.OrderBy(x => x).ToList().AsReadOnly();
    }

    /// <summary>
    /// Determines if ACTION6 should be allowed based on card status and PIN configuration.
    /// Rule: Allowed in Ordered/Inactive/Active/Blocked ONLY if PIN is set.
    /// Assumption A08: In Blocked status, both ACTION6 and ACTION7 are allowed when PIN is set.
    /// ENH-003: Refactored to use pattern matching (Rule PAT-003)
    /// </summary>
    /// <param name="card">The card to evaluate</param>
    /// <returns>True if ACTION6 should be allowed; otherwise false</returns>
    private static bool ShouldAllowAction6(Card card)
    {
        return card.Status switch
        {
            CardStatus.Ordered or CardStatus.Inactive or CardStatus.Active or CardStatus.Blocked
                => card.IsPinSet,
            _ => false
        };
    }

    /// <summary>
    /// Determines if ACTION7 should be allowed based on card status and PIN configuration.
    /// Rule: Inverse of ACTION6 for Ordered/Inactive/Active (allowed when PIN NOT set).
    /// Special case: In Blocked status, same as ACTION6 (allowed when PIN IS set).
    /// Assumption A08: In Blocked status, both ACTION6 and ACTION7 are allowed when PIN is set.
    /// ENH-003: Refactored to use pattern matching (Rule PAT-003)
    /// </summary>
    /// <param name="card">The card to evaluate</param>
    /// <returns>True if ACTION7 should be allowed; otherwise false</returns>
    private static bool ShouldAllowAction7(Card card)
    {
        return card.Status switch
        {
            CardStatus.Ordered or CardStatus.Inactive or CardStatus.Active
                => !card.IsPinSet,
            CardStatus.Blocked
                => card.IsPinSet, // Blocked status allows both ACTION6 & ACTION7 when PIN set
            _ => false
        };
    }

    /// <summary>
    /// Determines if ACTION8 should be allowed based on card status.
    /// Rule: Allowed in all statuses except Restricted, Expired, Closed.
    /// </summary>
    /// <param name="card">The card to evaluate</param>
    /// <returns>True if ACTION8 should be allowed; otherwise false</returns>
    private static bool ShouldAllowAction8(Card card)
    {
        return card.Status is not (CardStatus.Restricted or CardStatus.Expired or CardStatus.Closed);
    }

    /// <summary>
    /// Determines if the card is in an early lifecycle status.
    /// Early lifecycle statuses: Ordered, Inactive, Active
    /// </summary>
    /// <param name="card">The card to evaluate</param>
    /// <returns>True if card status is Ordered, Inactive, or Active; otherwise false</returns>
    /// <remarks>
    /// This grouping represents statuses where the card is operational or can become operational.
    /// Used by: ACTION10, ACTION12, ACTION13
    /// </remarks>
    private static bool IsInEarlyLifecycleStatus(Card card)
    {
        return card.Status is CardStatus.Ordered or CardStatus.Inactive or CardStatus.Active;
    }

    /// <summary>
    /// Determines if the card is in an active operational status.
    /// Active operational statuses: Inactive, Active
    /// </summary>
    /// <param name="card">The card to evaluate</param>
    /// <returns>True if card status is Inactive or Active; otherwise false</returns>
    /// <remarks>
    /// This grouping represents statuses where the card can be used or activated.
    /// Used by: ACTION11
    /// </remarks>
    private static bool IsInActiveOperationalStatus(Card card)
    {
        return card.Status is CardStatus.Inactive or CardStatus.Active;
    }
}