using Microsoft.Extensions.DependencyInjection;

using OpenTicket.Ddd.Infrastructure;
using OpenTicket.Infrastructure.Database.InMemory;

namespace OpenTicket.Infrastructure.Database;

public static class OpenTicketInfrastructureDatabaseModule
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, DatabaseOption option)
    {
        return option switch
        {
            DatabaseOption.InMemory => AddInMemoryPersistence(services),
            DatabaseOption.PostgreSql => AddPostgreSqlPersistence(services),
            _ => throw new ArgumentOutOfRangeException(nameof(option), option, "Unknown database option")
        };
    }

    private static IServiceCollection AddInMemoryPersistence(IServiceCollection services)
    {
        services.AddSingleton(typeof(IRepository<>), typeof(InMemoryRepository<>));
        services.AddSingleton<IUnitOfWork, InMemoryUnitOfWork>();
        return services;
    }

    private static IServiceCollection AddPostgreSqlPersistence(IServiceCollection services)
    {
        // TODO: Implement PostgreSQL persistence when ready
        throw new NotImplementedException("PostgreSQL persistence is not yet implemented");
    }
}
