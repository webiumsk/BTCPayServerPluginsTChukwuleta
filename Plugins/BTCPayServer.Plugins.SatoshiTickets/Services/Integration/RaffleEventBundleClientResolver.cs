#nullable enable
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace BTCPayServer.Plugins.SatoshiTickets.Services.Integration;

public sealed class RaffleEventBundleClientProvider
{
    private readonly IServiceProvider _serviceProvider;
    private IRaffleEventBundleClient? _client;
    private bool _resolved;

    public RaffleEventBundleClientProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>Resolved on first use so BTCPay Raffle can load after Satoshi Tickets.</summary>
    public IRaffleEventBundleClient? Client
    {
        get
        {
            if (!_resolved)
            {
                _client = RaffleEventBundleClientResolver.TryResolve(_serviceProvider);
                _resolved = true;
            }
            return _client;
        }
    }
}

internal static class RaffleEventBundleClientResolver
{
    private const string RaffleAssemblyName = "BTCPayServer.Plugins.BTCPayRaffle";
    private const string BundleServiceTypeName = "BTCPayServer.Plugins.BTCPayRaffle.Services.IRaffleEventBundleService";
    private const string BundleResultTypeName = "BTCPayServer.Plugins.BTCPayRaffle.Services.RaffleEventBundleResult";

    public static IRaffleEventBundleClient? TryResolve(IServiceProvider serviceProvider)
    {
        var raffleAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => string.Equals(a.GetName().Name, RaffleAssemblyName, StringComparison.Ordinal));
        if (raffleAssembly is null)
            return null;

        var serviceType = raffleAssembly.GetType(BundleServiceTypeName, throwOnError: false);
        if (serviceType is null)
            return null;

        var implementation = serviceProvider.GetService(serviceType);
        if (implementation is null)
            return null;

        var resultType = raffleAssembly.GetType(BundleResultTypeName, throwOnError: false);
        if (resultType is null)
            return null;

        return new ReflectionRaffleEventBundleClient(implementation, serviceType, resultType);
    }
}

internal sealed class ReflectionRaffleEventBundleClient : IRaffleEventBundleClient
{
    private readonly object _target;
    private readonly MethodInfo _validateMethod;
    private readonly MethodInfo _allocateMethod;
    private readonly Type _resultType;

    public ReflectionRaffleEventBundleClient(object target, Type serviceType, Type resultType)
    {
        _target = target;
        _resultType = resultType;
        _validateMethod = serviceType.GetMethod(
            nameof(ValidateBundledRaffleAsync),
            [typeof(string), typeof(Guid)])!;
        _allocateMethod = serviceType.GetMethod(
            nameof(AllocateForEventOrderAsync),
            [typeof(string), typeof(Guid), typeof(int), typeof(string), typeof(string), typeof(string), typeof(string)])!;
    }

    public async Task<(bool Ok, string? Error)> ValidateBundledRaffleAsync(string storeId, Guid raffleId)
    {
        var task = (Task)_validateMethod.Invoke(_target, [storeId, raffleId])!;
        await task.ConfigureAwait(false);
        var result = task.GetType().GetProperty("Result")!.GetValue(task);
        var ok = (bool)result!.GetType().GetProperty("Item1")!.GetValue(result)!;
        var error = (string?)result.GetType().GetProperty("Item2")!.GetValue(result);
        return (ok, error);
    }

    public async Task<RaffleBundleAllocationResult> AllocateForEventOrderAsync(
        string storeId,
        Guid raffleId,
        int count,
        string buyerEmail,
        string? buyerName,
        string eventOrderId,
        string baseUrl)
    {
        var task = (Task)_allocateMethod.Invoke(_target,
            [storeId, raffleId, count, buyerEmail, buyerName, eventOrderId, baseUrl])!;
        await task.ConfigureAwait(false);
        var result = task.GetType().GetProperty("Result")!.GetValue(task);
        return MapResult(result);
    }

    private RaffleBundleAllocationResult MapResult(object? result)
    {
        if (result is null)
            return new RaffleBundleAllocationResult { Success = false, Error = "Empty raffle bundle response" };

        var type = result.GetType();
        return new RaffleBundleAllocationResult
        {
            Success = (bool)type.GetProperty("Success")!.GetValue(result)!,
            IsNew = (bool)type.GetProperty("IsNew")!.GetValue(result)!,
            TicketsAllocated = (int)type.GetProperty("TicketsAllocated")!.GetValue(result)!,
            Error = (string?)type.GetProperty("Error")!.GetValue(result)
        };
    }
}
