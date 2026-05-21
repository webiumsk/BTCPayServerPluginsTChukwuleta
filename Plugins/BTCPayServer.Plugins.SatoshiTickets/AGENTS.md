# Agent: Satoshi Tickets (Webium fork)

Si agent pre **BTCPay Server plugin Satoshi Tickets** v repozitári **webiumsk/BTCPayServerPlugins** (lokálne často `BTCPayServerPluginsTChukwuleta`). Toto **nie je** čistý upstream od autora TChukwuleta — ide o **udržiavaný produkčný fork**.

Pred každou väčšou zmenou si prečítaj:
- `FORK_MAINTENANCE.md` — vetvy, merge upstream, mesačný checklist (ďalšia kontrola: **2026-06-20**)
- `CHANGELOG-FORK.md` — čo je fork-only vs merged z upstream

---

## Repozitáre

| Remote | Účel |
|--------|------|
| `origin` | webiumsk — deploy, tagy, `.btcpay` |
| `upstream` | TChukwuleta — len `git merge`, nie produkčný build |

**Deployateľná vetva:** `main`. Nové featury: `feature/*` → review → merge do `main`.

**Nikdy:** nasadiť upstream `.btcpay` na inštancie so Satflux alebo WordPress — chýbajú fork endpointy.

---

## Fork-only (chrániť pri merge upstream)

| Funkcia | Kľúčové súbory |
|---------|----------------|
| Offline / manuálne vstupenky | `Controllers/GreenfieldSatoshiTicketsController.cs` — `create-tickets-offline` |
| Greenfield purchase | ten istý controller — `CreatePurchase` |
| Disabled events v liste | `Controllers/GreenfieldSatoshiTicketsEventsController.cs`, query `includeInactive` / `include_inactive` |
| Event raffle bundle | `Data/Entities/Event.cs`, `Services/SimpleTicketSalesHostedService.cs`, `Services/EventRaffleBundleRequestValidator.cs`, migrácia bundle; **ProjectReference** na `BTCPayServer.Plugins.BTCPayRaffle` |

Po zmene fork featury aktualizuj `CHANGELOG-FORK.md` a bump `<Version>` v `BTCPayServer.Plugins.SatoshiTickets.csproj`.

---

## Architektúra pluginu

- **Greenfield API:** `Controllers/GreenfieldSatoshiTickets*.cs` — route prefix `~/api/v1/stores/{storeId}/satoshi-tickets/`
- **UI (Razor):** `Controllers/UITicketSales*.cs`, `Views/`
- **Platby / lístky:** `Services/SimpleTicketSalesHostedService.cs` — reaguje na `InvoiceEvent`, tag `Ticket_Sales_{txnId}`
- **DB:** schema `BTCPayServer.Plugins.SatoshiTickets`, `SimpleTicketSalesDbContextFactory`, migrácie v `Data/Migrations/`
- **Swagger:** `Resources/swagger.json` — pri API zmenách aktualizuj

Build (z koreňa pluginu alebo monorepa):

```bash
dotnet build -c Release Plugins/BTCPayServer.Plugins.SatoshiTickets/BTCPayServer.Plugins.SatoshiTickets.csproj
```

---

## Spotrebitelia mimo pluginu

| Projekt | Úloha |
|---------|--------|
| **Satflux** | Proxy + UI — `satflux/app/Http/Controllers/TicketController.php`, `app/Services/BtcPay/TicketService.php`, `resources/js/pages/stores/TicketsShow.vue`. Dok: `satflux/docs/SATOSHI_TICKETS.md` |
| **WordPress** | `create-tickets-offline`, `CreatePurchase` |
| **BTCPay Raffle** | `IRaffleEventBundleService` — bundle po `InvoiceSettled`, idempotencia `eventbundle:{orderId}:{email}` |

**Raffle bundle:** fulfillment **iba** v `SimpleTicketSalesHostedService` po úspešnom settle — **nie** cez Satflux webhook.

**Bundle pravidlá:** `počet_vstupeniek_s_rovnakým_emailom × bundledRaffleTicketsPerAdmission`; rôzne e-maily na objednávke = samostatná alokácia + e-mail.

---

## Pravidlá implementácie

1. **Minimálny diff** — nerefaktoruj upstream kód bez dôvodu.
2. **Konvencie BTCPay** — Greenfield validation errors, `CreateAPIError`, `EnsureStoreOwnership` cez store context.
3. **Migrácie** — nové stĺpce cez EF migráciu + `SimpleTicketSalesDbContextModelSnapshot.cs`.
4. **Voliteľná závislosť na Raffle** — `IRaffleEventBundleService` môže byť null ak Raffle plugin nie je nainštalovaný; validácia pri uložení eventu musí zlyhať zrozumiteľne ak bundle zapnutý bez Raffle.
5. **Commity** — len na explicitnú žiadosť používateľa.
6. Pri úprave API, ktoré volá Satflux, skontroluj camelCase payload (`bundledRaffleId`, `startDate`, …) a prípadne test v `satflux/tests/Feature/Ticket*.php`.

---

## Čo nerobiť

- Presúvať ticket/raffle fulfillment do Satflux Laravel webhookov.
- Mazat alebo prepisovať fork endpointy pri merge upstream bez kontroly owned regions.
- Pridávať `btcpay_store_id` do frontendu Satflux (satflux používa lokálne UUID store).

---

## Otvorené úlohy (stav máj 2026 — over v gite)

- [ ] Commit + merge `feature/event-raffle-bundle` do `main`
- [ ] Merge `feature/greenfield-purchase-api`, `feature/return-disabled-events` do `main` ak ešte nie sú
- [ ] `integrate/upstream-2026-05` → merge TChukwuleta ~1.3.61
- [ ] Release `.btcpay`, doplniť min. verzie v `satflux/docs/SATOSHI_TICKETS.md`

Keď používateľ pýta „mesačný merge“ alebo „čo robiť s Tickets pluginom“, otvor `FORK_MAINTENANCE.md` a postupuj podľa checklistu.
