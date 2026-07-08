using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReviewerAgent.Reviewer.Contracts;

namespace ReviewerAgent.Reviewer;

/// <summary>Registers the Reviewer module.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddReviewerModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<IReviewer, AiReviewer>();
        return services;
    }
}
