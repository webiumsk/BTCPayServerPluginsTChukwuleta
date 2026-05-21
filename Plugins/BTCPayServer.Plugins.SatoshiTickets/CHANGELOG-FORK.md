# Changelog — Webium fork (Satoshi Tickets)

Upstream autor: [TChukwuleta/BTCPayServerPlugins](https://github.com/TChukwuleta/BTCPayServerPlugins).  
Údržba fork: [webiumsk/BTCPayServerPlugins](https://github.com/webiumsk/BTCPayServerPlugins).

Postup pri každom release: [FORK_MAINTENANCE.md](./FORK_MAINTENANCE.md).

---

## [Unreleased]

### Pending
- **Upstream merge** — autor `upstream/main` ~1.3.61; integračná vetva `integrate/upstream-2026-06` (plán 2026-06-20).

---

## [1.3.6.3] — 2026-05-21

### Fixed
- **Event raffle bundle** — `ReflectionRaffleEventBundleClient` čítal `Result` z netypovaného `Task` → `NullReferenceException` pri validácii bundlu pri vytváraní/úprave eventu; opravené cez `InvokeAsync` a lookup metód na implementácii Raffle služby.

---

## [1.3.6.2] — 2026-05-21

### Fixed
- Pridaný chýbajúci EF migrácia `20260520120000_EventRaffleBundle.Designer.cs` — `MigrateAsync` aplikuje stĺpce `BundledRaffleId` / `BundledRaffleTicketsPerAdmission` (oprava `column BundledRaffleId does not exist`).
- `PluginMigrationRunner` loguje pending migrácie pred/po `MigrateAsync`.
- Jasnejšia chybová správa pri bundle bez nainštalovaného BTCPay Raffle.

---

## [1.3.6.1] — 2026-05-21

### Fixed
- Plugin sa načíta aj **bez** nainštalovaného BTCPay Raffle — odstránený compile-time `ProjectReference` na `BTCPayServer.Plugins.BTCPayRaffle`; integrácia cez lazy runtime resolver (`Services/Integration/`).

---

## [1.3.6.0] — 2026-05-21

### Fork
- **Greenfield purchase API** — `POST .../events/{eventId}/purchase` (checkout URL pre integrácie / WordPress).
- **`create-tickets-offline`** — manuálne / offline priradenie vstupeniek bez BTCPay invoice.
- **Všetky stavy eventov v API** — `GET /events` a `GET /events/{id}` vracajú Active aj Disabled (bez filtra `EventState == Active`).
- **Event raffle bundle** — `bundledRaffleId`, `bundledRaffleTicketsPerAdmission`; po `InvoiceSettled` alokácia cez BTCPay Raffle (`SimpleTicketSalesHostedService`).
- Fork runbook: `AGENTS.md`, `FORK_MAINTENANCE.md`.

### Merged from upstream
- Base pred fork release: lokálny stav po rebase na `upstream/main` (net10); bez nového upstream merge v tomto release.

---

## Template (po release doplniť)

```markdown
## [X.Y.Z-webium] — YYYY-MM-DD

### Fork
- …

### Merged from upstream
- Base upstream version: …
```
