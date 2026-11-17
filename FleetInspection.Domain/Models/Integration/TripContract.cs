using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FleetInspection.Integration;

public static class TripContract
{
    private static readonly string[] RequiredFields =
    {
        "MessageId", "Version", "CorrelationId", "InspectionId", "TripId",
        "VehicleId", "Revision", "EndOdometerKm", "EndFuelPercent", "OccurredAtUtc", "StateCode"
    };

    private static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    // Перевірка структури JSON, версії, часових меж
    public static TripCompletedV1 Parse(string json, DateTimeOffset now)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Очікувався JSON-об'єкт.");
        }
        
        var seenNames = new HashSet<string>();
        foreach (var prop in document.RootElement.EnumerateObject())
        {
            if (!seenNames.Add(prop.Name))
            {
                throw new JsonException("У JSON є дублікати ключів.");
            }
        }

        if (seenNames.Count != RequiredFields.Length)
        {
            throw new JsonException("Неправильна кількість полів у JSON.");
        }
        
        foreach (var field in RequiredFields)
        {
            if (!document.RootElement.TryGetProperty(field, out var val) ||
                val.ValueKind == JsonValueKind.Null)
            {
                throw new JsonException("Відсутні обов'язкові поля або значення null.");
            }
        }

        var m = JsonSerializer.Deserialize<TripCompletedV1>(json, Options);
        if (m == null)
        {
            throw new JsonException("Помилка десеріалізації.");
        }
        
        if (m.Version != 1 ||
            m.Revision <= 0 ||
            m.StateCode != "TRIP_COMPLETED" ||
            m.CorrelationId != m.InspectionId ||
            m.MessageId == Guid.Empty ||
            m.InspectionId == Guid.Empty ||
            m.TripId == Guid.Empty ||
            m.VehicleId == Guid.Empty ||
            m.EndOdometerKm < 0 ||
            m.EndFuelPercent < 0 ||
            m.EndFuelPercent > 100 ||
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

    public static string Canonical(TripCompletedV1 m)
    {
        return JsonSerializer.Serialize(m);
    }
}

//  Дублікати, конфлікти вмісту та порядок ревізій
public sealed class TripCache
{
    private readonly Dictionary<Guid, string> _messages = new();
    private readonly Dictionary<(Guid TripId, long Revision), string> _revisions = new();

    public int Count => _messages.Count;

    public Receipt Apply(string json, DateTimeOffset now)
    {
        var m = TripContract.Parse(json, now);
        var canonical = TripContract.Canonical(m);

        if (_messages.TryGetValue(m.MessageId, out var oldCanonical))
        {
            if (oldCanonical == canonical)
            {
                return Receipt.Duplicate;
            }
            throw new InvalidDataException("Конфлікт вмісту для вже обробленого MessageId.");
        }

        if (_revisions.ContainsKey((m.TripId, m.Revision)))
        {
            throw new InvalidDataException("Конфлікт ревізії.");
        }
        
        long currentRev = 0;
        foreach (var key in _revisions.Keys)
        {
            if (key.TripId == m.TripId && key.Revision > currentRev)
            {
                currentRev = key.Revision;
            }
        }

        if (m.Revision != currentRev + 1)
        {
            throw new InvalidDataException("Порушено послідовність ревізій.");
        }

        _messages.Add(m.MessageId, canonical);
        _revisions.Add((m.TripId, m.Revision), canonical);
        return Receipt.Applied;
    }
}