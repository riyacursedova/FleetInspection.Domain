using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FleetInspection.Application.Ports;
using FleetInspection.Domain.Models.Entities;
using FleetInspection.Domain.Models.ValueObject;

namespace FleetInspection.Application.Services;

public sealed class InspectionAppService
{
    private readonly IInspectionRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public InspectionAppService(IInspectionRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }
    
    public async Task CompleteInspectionAsync(InspectionId id, Odometer endOdometer, FuelLevel endFuel)
    {
        var loaded = await _repository.FindAsync(id);
        if (loaded == null)
        {
            throw new KeyNotFoundException($"Акт огляду з ID {id.Value} не знайдено.");
        }
        loaded.Aggregate.Complete(endOdometer, endFuel);
        
        _repository.Stage(loaded.Aggregate, loaded.Revision);
        await _unitOfWork.SaveChangesAsync();
    }

    // Координація  зовнішнього факту
    public async Task ExecuteIntegrationAsync<TMessage, TLocal>(
        string json,
        DateTimeOffset now,
        Func<string, DateTimeOffset, TMessage> parser,
        Func<TMessage, TLocal> mapper,
        Func<TMessage, InspectionId> idSelector,
        Action<VehicleInspectionLog, TLocal> domainAction)
    {
        // 1. Парсимо та валідуємо вхідний JSON через парсер
        var message = parser(json, now);
        var id = idSelector(message);

        // 2. Завантаження акту ТО 
        var loaded = await _repository.FindAsync(id);
        if (loaded == null)
        {
            throw new KeyNotFoundException($"Акт огляду з ID {id.Value} не знайдено.");
        }

        // 3. Перекладаємо дані у локальні об'єкти-значення та викликаємо метод агрегату
        var localData = mapper(message);
        domainAction(loaded.Aggregate, localData);

        // 4. Готуємо зміни до збереження, атомарно фіксуємо через паттерн UnitOfWork
        _repository.Stage(loaded.Aggregate, loaded.Revision);
        await _unitOfWork.SaveChangesAsync();
    }
}