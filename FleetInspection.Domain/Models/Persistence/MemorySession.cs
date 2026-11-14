using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FleetInspection.Application.Ports;
using FleetInspection.Domain.Models.Entities;
using FleetInspection.Domain.Models.ValueObject;

namespace FleetInspection.Infrastructure.Persistence;

public sealed class ConcurrencyException : Exception
{
    public ConcurrencyException(string message) : base(message) { }
}

// Сховище в RAM
public sealed class MemorySession : IInspectionRepository, IUnitOfWork
{
    // Таблиця БД/словник у RAM
    private readonly Dictionary<Guid, (InspectionState State, long Revision)> _storage = new();
    
    // Список змін, чекають на фіксацію при SaveChangesAsync
    private readonly List<(VehicleInspectionLog Aggregate, long ExpectedRevision)> _staged = new();

    // Заповнення бд, ревізія v1
    public MemorySession(params VehicleInspectionLog[] initialAggregates)
    {
        foreach (var agg in initialAggregates)
        {
            _storage[agg.Id.Value] = (agg.ExportState(), 1);
        }
    }

    // Пошук акта за ID: restore збирає живий обєкт
    public Task<LoadedInspection?> FindAsync(InspectionId id)
    {
        if (!_storage.TryGetValue(id.Value, out var entry))
            return Task.FromResult<LoadedInspection?>(null);

        var restored = VehicleInspectionLog.Restore(entry.State);
        return Task.FromResult<LoadedInspection?>(new LoadedInspection(restored, entry.Revision));
    }

    // Змінений агрегат до списку з ревізією
    public void Stage(VehicleInspectionLog aggregate, long expectedRevision)
    {
        if (aggregate == null) throw new ArgumentNullException(nameof(aggregate));
        _staged.Add((aggregate, expectedRevision));
    }

    // Unit of Work: атомарно зберігає зміни з перевіркою версій
    public Task SaveChangesAsync()
    {
        foreach (var (aggregate, expectedRevision) in _staged)
        {
            var id = aggregate.Id.Value;

            if (_storage.TryGetValue(id, out var current))
            {
                if (current.Revision != expectedRevision)
                {
                    throw new ConcurrencyException(
                        $"Конфлікт паралельного редагування для акта {id}. Очікувана: {expectedRevision}, у базі: {current.Revision}.");
                }

                // Якщо був +1
                _storage[id] = (aggregate.ExportState(), current.Revision + 1);
            }
            else
            {
                // Якщо змінку ще не було +0
                _storage[id] = (aggregate.ExportState(), 1);
            }
        }

        // Очистка списку
        _staged.Clear();
        return Task.CompletedTask;
    }
}