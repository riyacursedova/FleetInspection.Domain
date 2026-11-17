using System;
using System.Text.Json;

namespace TripDispatch;

// Зовнішнє повідомлення (контракт для відправки)
public sealed record TripCompletedMessage(
    Guid MessageId,
    int Version,
    Guid CorrelationId,
    Guid InspectionId,
    Guid TripId,
    Guid VehicleId,
    long Revision,
    int EndOdometerKm,
    int EndFuelPercent,
    DateTimeOffset OccurredAtUtc,
    string StateCode);

// Внутрішня подія рейсу
public sealed record TripCompletedDomainEvent(
    Guid EventId,
    Guid TripId,
    Guid InspectionId,
    Guid VehicleId,
    int EndOdometerKm,
    int EndFuelPercent,
    DateTimeOffset OccurredAtUtc);

public sealed class Trip(Guid id, Guid inspectionId, Guid vehicleId, int startOdometerKm, DateTimeOffset startedAt)
{
    public string Status { get; private set; } = "InProgress";
    public TripCompletedMessage? Pending { get; private set; }
    public TripCompletedDomainEvent? CompletedEvent { get; private set; }
    
    public void CompleteTrip(int endOdometerKm, int endFuelPercent, DateTimeOffset now)
    {
        if (Status != "InProgress" || Pending is not null || now < startedAt ||
            id == Guid.Empty || inspectionId == Guid.Empty || vehicleId == Guid.Empty ||
            endOdometerKm < startOdometerKm || endFuelPercent is < 0 or > 100)
        {
            throw new InvalidOperationException("Некоректні параметри завершення рейсу.");
        }

        CompletedEvent = new TripCompletedDomainEvent(
            Guid.NewGuid(), id, inspectionId, vehicleId, endOdometerKm, endFuelPercent, now);

        Pending = new TripCompletedMessage(
            MessageId: CompletedEvent.EventId,
            Version: 1,
            CorrelationId: inspectionId,
            InspectionId: inspectionId,
            TripId: id,
            VehicleId: vehicleId,
            Revision: 1,
            EndOdometerKm: endOdometerKm,
            EndFuelPercent: endFuelPercent,
            OccurredAtUtc: now,
            StateCode: "TRIP_COMPLETED");

        Status = "Completed";
    }

    public string PendingJson() =>
        JsonSerializer.Serialize(Pending ?? throw new InvalidOperationException("Немає повідомлення для відправлення."));

    public void Acknowledge(Guid messageId)
    {
        if (Pending?.MessageId != messageId)
            throw new InvalidOperationException("Невідповідний ідентифікатор підтвердження.");

        Pending = null;
    }
}