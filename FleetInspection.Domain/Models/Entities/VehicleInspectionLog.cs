using System;
using System.Collections.Generic;
using FleetInspection.Domain.Models.ValueObject;

namespace FleetInspection.Domain.Models.Entities;

// DTO для збереження та читання з БД 
public sealed record InspectionState(
    InspectionId Id,
    VehicleId VehicleId,
    InspectorId InspectorId,
    Odometer StartOdometer,
    FuelLevel StartFuelLevel,
    Odometer? EndOdometer,
    FuelLevel? EndFuelLevel,
    InspectionStatus Status,
    List<Defect> Defects
);

public sealed class VehicleInspectionLog
{
    private readonly List<Defect> _defects = new();

    public InspectionId Id { get; }
    public VehicleId VehicleId { get; }
    public InspectorId InspectorId { get; }
    public Odometer StartOdometer { get; }
    public FuelLevel StartFuelLevel { get; }
    
    public Odometer? EndOdometer { get; private set; }
    public FuelLevel? EndFuelLevel { get; private set; }
    public InspectionStatus Status { get; private set; }

    public IReadOnlyCollection<Defect> Defects => _defects.AsReadOnly();
    
    public VehicleInspectionLog(
        InspectionId id, 
        VehicleId vehicleId, 
        InspectorId inspectorId, 
        Odometer startOdometer, 
        FuelLevel startFuelLevel)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        VehicleId = vehicleId ?? throw new ArgumentNullException(nameof(vehicleId));
        InspectorId = inspectorId ?? throw new ArgumentNullException(nameof(inspectorId));
        StartOdometer = startOdometer ?? throw new ArgumentNullException(nameof(startOdometer));
        StartFuelLevel = startFuelLevel ?? throw new ArgumentNullException(nameof(startFuelLevel));

        Status = InspectionStatus.Draft;
    }

    // Приватний конструктор, заповнює поля при читанні з бази без валідації
    private VehicleInspectionLog(InspectionState s)
    {
        Id = s.Id;
        VehicleId = s.VehicleId;
        InspectorId = s.InspectorId;
        StartOdometer = s.StartOdometer;
        StartFuelLevel = s.StartFuelLevel;
        EndOdometer = s.EndOdometer;
        EndFuelLevel = s.EndFuelLevel;
        Status = s.Status;
        _defects = new List<Defect>(s.Defects);
    }

    // Фабричний метод: збирає живий об'єкт із знімка бази (Rehydration) без нових подій
    public static VehicleInspectionLog Restore(InspectionState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        return new VehicleInspectionLog(state);
    }

    // Пакує поточний стан об'єкта у знімок для запису в MemorySession
    public InspectionState ExportState()
    {
        return new InspectionState(
            Id, VehicleId, InspectorId, StartOdometer, StartFuelLevel,
            EndOdometer, EndFuelLevel, Status, new List<Defect>(_defects)
        );
    }
    
    public void AddDefect(string description, DefectSeverity severity)
    {
        if (Status != InspectionStatus.Draft)
            throw new InvalidOperationException("Реєстрація дефектів можлива лише під час початкового стану ТО (Draft).");

        _defects.Add(new Defect(DefectId.New(), description, severity));
    }

    // Перевірка інваріантів, кінцевий стан
    public void Complete(Odometer endOdometer, FuelLevel endFuelLevel)
    {
        if (Status != InspectionStatus.Draft)
            throw new InvalidOperationException("Акт огляду вже було завершено раніше.");

        if (endOdometer == null)
            throw new ArgumentNullException(nameof(endOdometer));

        if (endOdometer.Kilometers < StartOdometer.Kilometers)
        {
            throw new InvalidOperationException(
                $"Кінцевий пробіг ({endOdometer.Kilometers} км) менший за початковий ({StartOdometer.Kilometers} км).");
        }

        EndOdometer = endOdometer;
        EndFuelLevel = endFuelLevel ?? throw new ArgumentNullException(nameof(endFuelLevel));
        
        bool hasCriticalDefects = false;

        foreach (var defect in _defects)
        {
            if (defect.Severity == DefectSeverity.Critical)
            {
                hasCriticalDefects = true;
                break;
            }
        }

        if (hasCriticalDefects)
        {
            Status = InspectionStatus.FailedBlockOutbound;
        }
        else
        {
            Status = InspectionStatus.ApprovedForDrive;
        }
    }
}