using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReviewerAgent.PromptManager.Contracts;

namespace ReviewerAgent.PromptManager;

/// <summary>Registers the PromptManager module.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddPromptManagerModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<IPromptManager, PromptManager>();
        return services;
    }
}
