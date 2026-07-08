using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReviewerAgent.ContextBuilder.Contracts;

namespace ReviewerAgent.ContextBuilder;

/// <summary>Registers the ContextBuilder module.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddContextBuilderModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<IContextBuilder, ContextBuilder>();
        return services;
    }
}
