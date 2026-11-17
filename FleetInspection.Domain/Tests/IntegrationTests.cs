using System;
using System.Text.Json;
using System.Threading.Tasks;
using FleetInspection.Application.Services;
using FleetInspection.Domain.Models.Entities;
using FleetInspection.Domain.Models.ValueObject;
using FleetInspection.Infrastructure.Persistence;
using FleetInspection.Integration;
using TripDispatch;
using Xunit;

namespace FleetInspection.Tests.Integration;

public class IntegrationTests
{
    private readonly DateTimeOffset _now = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);

    private VehicleInspectionLog CreateDraftLog()
    {
        return new VehicleInspectionLog(
            InspectionId.New(),
            new VehicleId(Guid.NewGuid()),
            new InspectorId(Guid.NewGuid()),
            new Odometer(100000),
            new FuelLevel(80m)
        );
    }

    // 1. Успішне приймання факту завершення рейсу
    [Fact]
    public async Task ExecuteIntegration_Success()
    {
        var log = CreateDraftLog();
        var session = new MemorySession(log);
        var service = new InspectionAppService(session, session);
        var cache = new TripCache();

        var trip = new Trip(Guid.NewGuid(), log.Id.Value, log.VehicleId.Value, 100000, _now.AddHours(-2));
        trip.CompleteTrip(100250, 65, _now.AddMinutes(-5));
        var json = trip.PendingJson();

        var receipt = cache.Apply(json, _now);
        Assert.Equal(Receipt.Applied, receipt);

        await service.ExecuteIntegrationAsync(
            json,
            _now,
            TripContract.Parse,
            TripTranslator.ToLocal,
            TripTranslator.ToInspectionId,
            (aggregate, data) => aggregate.Complete(data.EndOdometer, data.EndFuel)
        );

        trip.Acknowledge(trip.CompletedEvent!.EventId);
        Assert.Null(trip.Pending);

        var reloaded = await session.FindAsync(log.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(InspectionStatus.ApprovedForDrive, reloaded!.Aggregate.Status);
        Assert.Equal(100250, reloaded.Aggregate.EndOdometer!.Kilometers);
        Assert.Equal(2, reloaded.Revision);
    }

    // 2. Точний повтор повідомлення (ідемпотентність)
    [Fact]
    public async Task TripCache_ExactDuplicate()
    {
        var log = CreateDraftLog();
        var session = new MemorySession(log);
        var service = new InspectionAppService(session, session);
        var cache = new TripCache();

        var trip = new Trip(Guid.NewGuid(), log.Id.Value, log.VehicleId.Value, 100000, _now.AddHours(-2));
        trip.CompleteTrip(100250, 65, _now.AddMinutes(-5));
        var json = trip.PendingJson();

        Assert.Equal(Receipt.Applied, cache.Apply(json, _now));
        await service.ExecuteIntegrationAsync(
            json, _now, TripContract.Parse, TripTranslator.ToLocal, TripTranslator.ToInspectionId,
            (agg, data) => agg.Complete(data.EndOdometer, data.EndFuel));

        Assert.Equal(Receipt.Duplicate, cache.Apply(json, _now));
        Assert.Equal(1, cache.Count);

        var reloaded = await session.FindAsync(log.Id);
        Assert.Equal(2, reloaded!.Revision);
    }

    // 3. Подія з майбутнього відхиляється на межі контракту
    [Fact]
    public void TripContract_FutureTimestamp()
    {
        var inspId = Guid.NewGuid();
        var futureJson = JsonSerializer.Serialize(new TripCompletedMessage(
            Guid.NewGuid(), 1, inspId, inspId, Guid.NewGuid(), Guid.NewGuid(), 1, 100200, 70, _now.AddMinutes(10), "TRIP_COMPLETED"));

        Assert.Throws<JsonException>(() => TripContract.Parse(futureJson, _now));
    }

    // 4. Порушення доменного інваріанта не змінює стан у БД
    [Fact]
    public async Task ExecuteIntegration_InvalidOdometer()
    {
        var log = CreateDraftLog();
        var session = new MemorySession(log);
        var service = new InspectionAppService(session, session);

        var trip = new Trip(Guid.NewGuid(), log.Id.Value, log.VehicleId.Value, 90000, _now.AddHours(-2));
        trip.CompleteTrip(99500, 60, _now.AddMinutes(-5));
        var json = trip.PendingJson();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ExecuteIntegrationAsync(
                json, _now, TripContract.Parse, TripTranslator.ToLocal, TripTranslator.ToInspectionId,
                (agg, data) => agg.Complete(data.EndOdometer, data.EndFuel)));

        var reloaded = await session.FindAsync(log.Id);
        Assert.Equal(InspectionStatus.Draft, reloaded!.Aggregate.Status);
        Assert.Equal(1, reloaded.Revision);
    }
}