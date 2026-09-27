using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Upms.Application.Common;

namespace Upms.Web.Security;

/// <summary>Runs every call to a UI-facing application service in its own DI scope, so each user action
/// gets a fresh database context and unit of work (an interactive circuit lives for hours; research R2).
/// The caller is captured from the circuit or request that makes the call.</summary>
public static class OperationScope
{
    /// <summary>Replaces the registration of <typeparamref name="TService"/> with a per-call scoped proxy.</summary>
    public static IServiceCollection AddOperationScoped<TService>(this IServiceCollection services)
        where TService : class
    {
        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(TService))
            ?? throw new InvalidOperationException($"{typeof(TService).Name} must be registered first.");
        var implementationType = descriptor.ImplementationType
            ?? throw new InvalidOperationException($"{typeof(TService).Name} must be registered with an implementation type.");

        services.TryAdd(ServiceDescriptor.Scoped(implementationType, implementationType));
        services.Replace(ServiceDescriptor.Scoped(typeof(TService),
            sp => OperationScopeProxy<TService>.Create(sp, implementationType)));
        return services;
    }
}

/// <summary>The proxy behind <see cref="OperationScope.AddOperationScoped{TService}"/>.</summary>
#pragma warning disable CA1852 // DispatchProxy requires a non-sealed class.
public class OperationScopeProxy<TService> : DispatchProxy
#pragma warning restore CA1852
    where TService : class
{
    private static readonly MethodInfo InvokeTypedMethod =
        typeof(OperationScopeProxy<TService>).GetMethod(nameof(InvokeTypedAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private IServiceScopeFactory _scopeFactory = null!;
    private ICurrentUser _caller = null!;
    private Type _implementationType = null!;

    internal static TService Create(IServiceProvider services, Type implementationType)
    {
        var proxy = Create<TService, OperationScopeProxy<TService>>();
        var self = (OperationScopeProxy<TService>)(object)proxy;
        self._scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
        self._caller = services.GetRequiredService<ICurrentUser>();
        self._implementationType = implementationType;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        var userId = _caller.UserId;
        var returnType = targetMethod.ReturnType;
        if (returnType == typeof(Task))
        {
            return InvokeAsync(targetMethod, args, userId);
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            return InvokeTypedMethod.MakeGenericMethod(returnType.GetGenericArguments()[0])
                .Invoke(this, [targetMethod, args, userId]);
        }

        throw new NotSupportedException($"{typeof(TService).Name}.{targetMethod.Name} must return a Task.");
    }

    private async Task InvokeAsync(MethodInfo method, object?[]? args, Guid? userId)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        await (Task)Call(scope, method, args, userId)!;
    }

    private async Task<T> InvokeTypedAsync<T>(MethodInfo method, object?[]? args, Guid? userId)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        return await (Task<T>)Call(scope, method, args, userId)!;
    }

    private object? Call(AsyncServiceScope scope, MethodInfo method, object?[]? args, Guid? userId)
    {
        scope.ServiceProvider.GetRequiredService<CurrentUser>().UseOperationUser(userId);
        var target = scope.ServiceProvider.GetRequiredService(_implementationType);
        try
        {
            return method.Invoke(target, args);
        }
        catch (TargetInvocationException e) when (e.InnerException is not null)
        {
            ExceptionDispatchInfo.Throw(e.InnerException);
            throw;
        }
    }
}
