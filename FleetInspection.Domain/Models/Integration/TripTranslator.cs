using FleetInspection.Domain.Models.ValueObject;

namespace FleetInspection.Integration;

public sealed record LocalCompletionData(Odometer EndOdometer, FuelLevel EndFuel);

// Переклад зовнішнього контракту у локальні об'єкти-значення
public static class TripTranslator
{
    public static InspectionId ToInspectionId(TripCompletedV1 m) => new(m.InspectionId);

    public static LocalCompletionData ToLocal(TripCompletedV1 m)
    {
        var odometer = new Odometer(m.EndOdometerKm);
        var fuelLevel = new FuelLevel(m.EndFuelPercent);
        return new LocalCompletionData(odometer, fuelLevel);
    }
}