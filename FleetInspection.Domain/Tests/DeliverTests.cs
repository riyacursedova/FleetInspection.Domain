using System.Text.Json;
using FleetInspection.Domain.Models.Entities;
using FleetInspection.Domain.Models.ValueObject;
using FleetInspection.Integration;
using FleetInspection.Messaging;
using Xunit;

namespace FleetInspection.Domain.Tests;

public class DeliveryTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static (SenderModel Sender, ReceiverModel Receiver, Guid TripId, Guid InspectionId, Guid VehicleId, Guid MsgId) Setup()
    {
        var vehicleId = Guid.NewGuid();
        var inspection = new VehicleInspectionLog(
            InspectionId.New(),
            new VehicleId(vehicleId),
            new InspectorId(Guid.NewGuid()),
            new Odometer(10000),
            new FuelLevel(80));

        var tripId = Guid.NewGuid();
        return (new SenderModel(tripId), new ReceiverModel(inspection), tripId, inspection.Id.Value, vehicleId, Guid.NewGuid());
    }

    private static TripCompletedV1 Msg(Guid tripId, Guid inspectionId, Guid vehicleId, Guid msgId, int endKm = 10250) => new()
    {
        MessageId = msgId,
        Version = 1,
        CorrelationId = inspectionId,
        InspectionId = inspectionId,
        TripId = tripId,
        VehicleId = vehicleId,
        Revision = 1,
        EndOdometerKm = endKm,
        EndFuelPercent = 60,
        OccurredAtUtc = Now,
        StateCode = "TRIP_COMPLETED"
    };

    [Fact]
    public void NormalDelivery_Success()
    {
        var (sender, receiver, tripId, inspectionId, vehicleId, msgId) = Setup();

        sender.Report(Msg(tripId, inspectionId, vehicleId, msgId), Now);
        sender.Deliver(receiver, Now);

        Assert.True(sender.State.Outbox!.Sent);
        Assert.Equal(InspectionStatus.ApprovedForDrive, receiver.State.Inspection.Status);
        Assert.Equal(1, receiver.State.AppliedCount);
    }

    [Fact]
    public void LostAck_RetryWorks()
    {
        var (sender, receiver, tripId, inspectionId, vehicleId, msgId) = Setup();
        sender.Report(Msg(tripId, inspectionId, vehicleId, msgId), Now);

        Assert.Throws<IOException>(() => sender.Deliver(receiver, Now, loseAck: true));
        Assert.False(sender.State.Outbox!.Sent);

        sender.Deliver(receiver, Now);

        Assert.True(sender.State.Outbox!.Sent);
        Assert.Equal(1, receiver.State.AppliedCount);
    }

    [Fact]
    public void FailBeforeCommit_Rollback()
    {
        var (sender, receiver, tripId, inspectionId, vehicleId, msgId) = Setup();
        sender.Report(Msg(tripId, inspectionId, vehicleId, msgId), Now);

        Assert.Throws<IOException>(() => receiver.Handle(sender.State.Outbox!.Json, Now, failBeforeCommit: true));

        Assert.Empty(receiver.State.Inbox);
        Assert.Equal(InspectionStatus.Draft, receiver.State.Inspection.Status);
        Assert.Equal(0, receiver.State.AppliedCount);
    }

    [Fact]
    public void SameIdConflict_Throws()
    {
        var (_, receiver, tripId, inspectionId, vehicleId, msgId) = Setup();

        receiver.Handle(JsonSerializer.Serialize(Msg(tripId, inspectionId, vehicleId, msgId, endKm: 10250)), Now);

        Assert.Throws<InvalidDataException>(() =>
            receiver.Handle(JsonSerializer.Serialize(Msg(tripId, inspectionId, vehicleId, msgId, endKm: 10999)), Now));

        Assert.Equal(1, receiver.State.AppliedCount);
        Assert.Equal(10250, receiver.State.Inspection.EndOdometer!.Kilometers);
    }
}