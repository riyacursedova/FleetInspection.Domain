namespace FleetInspection.Domain.Models.ValueObject;

public sealed class FuelLevel
{
    public decimal Percentage { get; }

    public FuelLevel(decimal percentage)
    {
        if (percentage < 0 || percentage > 100)
        {
            throw new ArgumentOutOfRangeException("Рівень пального має бути в межах від 0% до 100%.");
        }
        Percentage = percentage;
    }
}