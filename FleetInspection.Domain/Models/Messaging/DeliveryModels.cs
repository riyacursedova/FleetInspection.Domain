using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FleetInspection.Domain.Models.Entities;
using FleetInspection.Integration;

namespace FleetInspection.Messaging;

public sealed record DeliveryState(
    InspectionState Inspection,
    Dictionary<Guid, string> Inbox,
    Dictionary<Guid, long> LastRevisions,
    int AppliedCount);

public sealed class ReceiverModel(VehicleInspectionLog initial)
{
    private DeliveryState _state = new(initial.ExportState(), [], [], 0);

    public DeliveryState State => _state with
    {
        Inbox = new(_state.Inbox),
        LastRevisions = new(_state.LastRevisions)
    };

    public Receipt Handle(string json, DateTimeOffset now, bool failBeforeCommit = false)
    {
        var msg = TripContract.Parse(json, now);
        var canonical = TripContract.Canonical(msg);

        if (_state.Inbox.TryGetValue(msg.MessageId, out var prev))
            return prev == canonical ? Receipt.Duplicate : throw new InvalidDataException("message_conflict");

        var lastRev = _state.LastRevisions.GetValueOrDefault(msg.TripId, 0);
        if (msg.Revision != lastRev + 1)
            throw new InvalidDataException("revision_conflict_or_gap");

        if (_state.Inspection.Id.Value != msg.InspectionId)
            throw new InvalidDataException("wrong_inspection");

        var inspection = VehicleInspectionLog.Restore(_state.Inspection);
        var local = TripTranslator.ToLocal(msg);
        inspection.Complete(local.EndOdometer, local.EndFuel);

        var nextInbox = new Dictionary<Guid, string>(_state.Inbox) { [msg.MessageId] = canonical };
        var nextRevs = new Dictionary<Guid, long>(_state.LastRevisions) { [msg.TripId] = msg.Revision };
        var next = new DeliveryState(inspection.ExportState(), nextInbox, nextRevs, _state.AppliedCount + 1);

        if (failBeforeCommit) throw new IOException("simulated_failure");

        _state = next;
        return Receipt.Applied;
    }
}

public sealed record OutboxItem(Guid MessageId, string Json, bool Sent);
public sealed record SenderState(Guid TripId, string Status, OutboxItem? Outbox);

public sealed class SenderModel(Guid tripId)
{
    public SenderState State { get; private set; } = new(tripId, "InProgress", null);

    public void Report(TripCompletedV1 msg, DateTimeOffset now)
    {
        var valid = TripContract.Parse(JsonSerializer.Serialize(msg), now);
        if (State.Status != "InProgress" || valid.TripId != State.TripId)
            throw new InvalidOperationException("wrong_trip_or_state");

        State = new(State.TripId, "Completed", new OutboxItem(valid.MessageId, TripContract.Canonical(valid), false));
    }

    public void Deliver(ReceiverModel receiver, DateTimeOffset now, bool loseAck = false)
    {
        var item = State.Outbox ?? throw new InvalidOperationException("no_message");
        if (item.Sent) return;

        receiver.Handle(item.Json, now);
        if (loseAck) throw new IOException("acknowledgement_lost");

        State = State with { Outbox = item with { Sent = true } };
    }
}