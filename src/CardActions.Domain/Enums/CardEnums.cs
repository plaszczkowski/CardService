namespace CardActions.Domain.Enums;

public enum CardType
{
    Prepaid = 1,
    Debit = 2,
    Credit = 3
}

public enum CardStatus
{
    Ordered = 1,
    Inactive = 2,
    Active = 3,
    Restricted = 4,
    Blocked = 5,
    Expired = 6,
    Closed = 7
}