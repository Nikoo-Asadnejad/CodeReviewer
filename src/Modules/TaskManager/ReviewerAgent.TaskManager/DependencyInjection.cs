using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReviewerAgent.TaskManager.Contracts;

namespace ReviewerAgent.TaskManager;

/// <summary>Registers the TaskManager module.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddTaskManagerModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TaskManagerOptions>(configuration.GetSection(TaskManagerOptions.SectionName));
        services.AddTransient<ITaskManager, TaskManager>();
        return services;
    }
}
