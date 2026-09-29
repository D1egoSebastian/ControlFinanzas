using ControlDeFinanzasIA.Application.UseCases.Notifications.DetectNotifications;
using ControlDeFinanzasIA.Application.UseCases.Notifications.DismissNotification;
using ControlDeFinanzasIA.Application.UseCases.Notifications.GetNotifications;
using ControlDeFinanzasIA.Application.UseCases.Notifications.GetNotificationSettings;
using ControlDeFinanzasIA.Application.UseCases.Notifications.MarkNotificationsRead;
using ControlDeFinanzasIA.Application.UseCases.Notifications.UpdateNotificationSettings;
using ControlDeFinanzasIA.UI.Composition;
using ControlDeFinanzasIA.UI.Services;
using ControlDeFinanzasIA.UI.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Verifies the Fase 17 DI block of <see cref="Bootstrap"/> (F-017, D-14, slice S5a): the
/// singletons resolve from the root without a captive scoped dependency, and the transient
/// use cases resolve inside a scope.
/// </summary>
public sealed class NotificationCompositionTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        Bootstrap.ConfigureServices(services, new ConfigurationBuilder().Build());
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = false });
    }

    [Fact]
    public void NotificationSingletons_ResolveFromRoot_AsTheSameInstance()
    {
        using ServiceProvider provider = BuildProvider();

        INotificationCenter center = provider.GetRequiredService<INotificationCenter>();
        NotificationListViewModel first = provider.GetRequiredService<NotificationListViewModel>();
        NotificationListViewModel second = provider.GetRequiredService<NotificationListViewModel>();

        Assert.IsType<ScopedNotificationCenter>(center);
        Assert.Same(center, provider.GetRequiredService<INotificationCenter>());
        Assert.Same(first, second);
    }

    [Fact]
    public void NotificationUseCases_ResolveInsideAScope()
    {
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        Assert.NotNull(services.GetRequiredService<DetectNotificationsUseCase>());
        Assert.NotNull(services.GetRequiredService<GetNotificationsQuery>());
        Assert.NotNull(services.GetRequiredService<MarkNotificationsReadUseCase>());
        Assert.NotNull(services.GetRequiredService<DismissNotificationUseCase>());
        Assert.NotNull(services.GetRequiredService<GetNotificationSettingsQuery>());
        Assert.NotNull(services.GetRequiredService<UpdateNotificationSettingsUseCase>());
    }
}
