using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Application;

namespace TaskFlow.Infrastructure;

public static class DependencyInjection
{
    // The one call the host makes to plug the outer layer in. Everything EF
    // Core and SQLite needs to know is behind this method.
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, string connectionString, bool logSensitiveData)
    {
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
