using System.Threading.Tasks;

namespace FleetInspection.Application.Ports;

public interface IUnitOfWork
{
    Task SaveChangesAsync();
}