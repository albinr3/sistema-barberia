namespace Barberia.Data.Models;

public static class Money
{
    public static long ToCents(decimal amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount cannot be negative.");
        }

        return decimal.ToInt64(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
    }

    public static decimal FromCents(long cents)
    {
        return cents / 100m;
    }
}
