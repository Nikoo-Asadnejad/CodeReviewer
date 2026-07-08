using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReviewerAgent.CodeScanner.Contracts;

namespace ReviewerAgent.CodeScanner;

/// <summary>Registers the CodeScanner module.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddCodeScannerModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CodeScannerOptions>(configuration.GetSection(CodeScannerOptions.SectionName));
        services.AddTransient<ICodeScanner, RoslynCodeScanner>();
        return services;
    }
}
