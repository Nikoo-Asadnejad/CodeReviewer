using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReviewerAgent.Rules.Contracts;

namespace ReviewerAgent.Rules;

/// <summary>Registers the Rules module.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddRulesModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RulesOptions>(configuration.GetSection(RulesOptions.SectionName));
        services.AddTransient<IRuleProvider, MarkdownRuleProvider>();
        return services;
    }
}
