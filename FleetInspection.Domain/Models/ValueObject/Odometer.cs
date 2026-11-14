namespace FleetInspection.Domain.Models.ValueObject;

public sealed class Odometer
{
    public int Kilometers { get; }

    public Odometer(int kilometers)
    {
        if (kilometers < 0)
        {
            throw new ArgumentException("Показання одометра не можуть бути від'ємними.");
        }
        Kilometers = kilometers;
    }
}