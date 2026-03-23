using Microsoft.Extensions.DependencyInjection;

namespace Nabu.RCL;

public static class NabuServiceCollectionExtensions
{
    public static NabuBuilder AddNabu(this IServiceCollection services)
    {
        services.AddScoped<IWhisperSettings, WhisperSettingsService>();
        return new NabuBuilder(services);
    }
}

public class NabuBuilder(IServiceCollection services)
{
    public NabuBuilder AddHandler<THandler>() where THandler : class, IWhisperHandler
    {
        services.AddScoped<THandler>();
        services.AddScoped<IWhisperHandler>(sp => sp.GetRequiredService<THandler>());
        return this;
    }
}
