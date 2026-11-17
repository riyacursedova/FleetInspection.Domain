using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FleetInspection.Integration;

public static class TripContract
{
    private static readonly string[] RequiredFields =
    [
        "MessageId", "Version", "CorrelationId", "InspectionId", "TripId",
        "VehicleId", "Revision", "EndOdometerKm", "EndFuelPercent", "OccurredAtUtc", "StateCode"
    ];

    private static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    // Перевірка структури JSON, версії, часових меж та нормалізація до UTC
    public static TripCompletedV1 Parse(string json, DateTimeOffset now)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new JsonException("Очікувався JSON-об'єкт.");

        var names = document.RootElement.EnumerateObject().Select(p => p.Name).ToArray();
        if (names.Length != RequiredFields.Length ||
            names.Distinct().Count() != names.Length ||
            RequiredFields.Any(f => !document.RootElement.TryGetProperty(f, out var val) ||
                                    val.ValueKind == JsonValueKind.Null))
        {
            throw new JsonException("Відсутні обов'язкові поля, значення null або дублікати ключів.");
        }

        var m = JsonSerializer.Deserialize<TripCompletedV1>(json, Options)
                ?? throw new JsonException("Помилка десеріалізації.");

        if (m.Version != 1 ||
            m.Revision <= 0 ||
            m.StateCode != "TRIP_COMPLETED" ||
            m.CorrelationId != m.InspectionId ||
            new[] { m.MessageId, m.InspectionId, m.TripId, m.VehicleId }.Any(id => id == Guid.Empty) ||
            m.EndOdometerKm < 0 ||
            m.EndFuelPercent is < 0 or > 100 ||
            m.OccurredAtUtc == default ||
            m.OccurredAtUtc > now)
        {
            throw new JsonException("Некоректні значення полів контракту.");
        }

        return m with
        {
            OccurredAtUtc = m.OccurredAtUtc.ToUniversalTime()
        };
    }

    public static string Canonical(TripCompletedV1 m) => JsonSerializer.Serialize(m);
}

// Відстеження дублікатів, конфліктів вмісту та порядку ревізій
public sealed class TripCache
{
    private readonly Dictionary<Guid, string> _messages = [];
    private readonly Dictionary<(Guid TripId, long Revision), string> _revisions = [];

    public int Count => _messages.Count;

    public Receipt Apply(string json, DateTimeOffset now)
    {
        var m = TripContract.Parse(json, now);
        var canonical = TripContract.Canonical(m);

        if (_messages.TryGetValue(m.MessageId, out var oldCanonical))
        {
            return oldCanonical == canonical
                ? Receipt.Duplicate
                : throw new InvalidDataException("Конфлікт вмісту для вже обробленого MessageId.");
        }

        if (_revisions.ContainsKey((m.TripId, m.Revision)))
            throw new InvalidDataException("Конфлікт ревізії.");

        var currentRev = _revisions.Keys
            .Where(k => k.TripId == m.TripId)
            .Select(k => k.Revision)
            .DefaultIfEmpty(0)
            .Max();

        if (m.Revision != currentRev + 1)
            throw new InvalidDataException("Порушено послідовність ревізій.");

        _messages.Add(m.MessageId, canonical);
        _revisions.Add((m.TripId, m.Revision), canonical);
        return Receipt.Applied;
    }
}