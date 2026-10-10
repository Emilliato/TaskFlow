using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Application;

namespace TaskFlow.Infrastructure;

public static class DependencyInjection
{
    // The one call the host makes to plug the outer layer in. Everything EF
    // Core and SQLite needs to know is behind this method - and so is the
    // choice of store: the same two interfaces, two interchangeable
    // implementations, picked by one configuration value.
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, string connectionString, bool logSensitiveData,
        string storage = "Sqlite")
    {
        if (storage.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
        {
            // Singletons: the "database" is the object itself, so it must outlive a request.
            services.AddSingleton<ITaskRepository, InMemoryTaskRepository>();
            services.AddSingleton<ICommentRepository, InMemoryCommentRepository>();
            return services;
        }

        services.AddDbContext<TaskFlowDbContext>(options =>
        {
            options.UseSqlite(connectionString);
            if (logSensitiveData) options.EnableSensitiveDataLogging();
        });

        services.AddScoped<ITaskRepository, EfTaskRepository>();
        services.AddScoped<ICommentRepository, EfCommentRepository>();
        return services;
    }
}
