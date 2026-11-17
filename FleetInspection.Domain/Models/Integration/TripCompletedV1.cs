using System;

namespace FleetInspection.Integration;

public sealed record TripCompletedV1
{
    public required Guid MessageId { get; init; }
    public required int Version { get; init; }
    public required Guid CorrelationId { get; init; }
    public required Guid InspectionId { get; init; }
    public required Guid TripId { get; init; }
    public required Guid VehicleId { get; init; }
    public required long Revision { get; init; }
    public required int EndOdometerKm { get; init; }
    public required int EndFuelPercent { get; init; }
    public required DateTimeOffset OccurredAtUtc { get; init; }
    public required string StateCode { get; init; }
}

public enum Receipt
{
    Applied,
    Duplicate
}