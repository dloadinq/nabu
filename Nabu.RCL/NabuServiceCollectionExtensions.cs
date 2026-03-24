using Microsoft.Extensions.DependencyInjection;

namespace Nabu.RCL;

public static class NabuServiceCollectionExtensions
{
    public static NabuBuilder AddNabu(this IServiceCollection services)
    {
        services.AddScoped<INabuSettings, WhisperSettingsService>();
        return new NabuBuilder(services);
    }
}

public class NabuBuilder(IServiceCollection services)
{
    public NabuBuilder AddHandler<THandler>() where THandler : class, INabuHandler
    {
        services.AddScoped<THandler>();
        services.AddScoped<INabuHandler>(sp => sp.GetRequiredService<THandler>());
        return this;
    }
}
