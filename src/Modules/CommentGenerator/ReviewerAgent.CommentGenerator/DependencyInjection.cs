using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReviewerAgent.CommentGenerator.Contracts;

namespace ReviewerAgent.CommentGenerator;

/// <summary>Registers the CommentGenerator module.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddCommentGeneratorModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<ICommentGenerator, CommentGenerator>();
        return services;
    }
}
