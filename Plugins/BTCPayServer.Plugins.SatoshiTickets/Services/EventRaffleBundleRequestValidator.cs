#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BTCPayServer.Plugins.BTCPayRaffle.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace BTCPayServer.Plugins.SatoshiTickets.Services;

public static class EventRaffleBundleRequestValidator
{
    public const int MaxTicketsPerAdmission = 20;

    public static async Task ApplyBundleFieldsAsync(
        ModelStateDictionary modelState,
        string storeId,
        int bundledTicketsPerAdmission,
        Guid? bundledRaffleId,
        IRaffleEventBundleService? raffleBundle)
    {
        if (bundledTicketsPerAdmission < 0)
        {
            modelState.AddModelError(nameof(bundledTicketsPerAdmission),
                "Bundled raffle tickets per admission cannot be negative");
            return;
        }

        if (bundledTicketsPerAdmission > MaxTicketsPerAdmission)
        {
            modelState.AddModelError(nameof(bundledTicketsPerAdmission),
                $"Bundled raffle tickets per admission cannot exceed {MaxTicketsPerAdmission}");
            return;
        }

        if (bundledTicketsPerAdmission == 0)
            return;

        if (!bundledRaffleId.HasValue || bundledRaffleId.Value == Guid.Empty)
        {
            modelState.AddModelError(nameof(bundledRaffleId),
                "Select an open raffle when including raffle tickets per admission");
            return;
        }

        if (raffleBundle is null)
        {
            modelState.AddModelError(nameof(bundledRaffleId),
                "BTCPay Raffle plugin is required for raffle ticket bundles");
            return;
        }

        var (ok, error) = await raffleBundle.ValidateBundledRaffleAsync(storeId, bundledRaffleId.Value);
        if (!ok)
            modelState.AddModelError(nameof(bundledRaffleId), error ?? "Invalid raffle");
    }
}
