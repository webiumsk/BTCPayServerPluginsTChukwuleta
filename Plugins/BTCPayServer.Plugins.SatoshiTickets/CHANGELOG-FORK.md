# Changelog — Webium fork (Satoshi Tickets)

Upstream autor: [TChukwuleta/BTCPayServerPlugins](https://github.com/TChukwuleta/BTCPayServerPlugins).  
Údržba fork: [webiumsk/BTCPayServerPlugins](https://github.com/webiumsk/BTCPayServerPlugins).

Postup pri každom release: [FORK_MAINTENANCE.md](./FORK_MAINTENANCE.md).

---

## [Unreleased]

### Pending
- **Upstream merge** — autor `upstream/main` ~1.3.61; integračná vetva `integrate/upstream-2026-06` (plán 2026-06-20).

---

## [1.3.6.4] — 2026-05-21

### Fixed
- **Event raffle bundle** — validácia raffle cez reflection: `ValueTuple` má `Item1`/`Item2` ako **polia**, nie properties → vždy „Invalid raffle validation response“ (aj pri platnom raffle).

---

## [1.3.6.3] — 2026-05-21

### Fixed
- **Event raffle bundle** — `ReflectionRaffleEventBundleClient` čítal `Result` z netypovaného `Task` → `NullReferenceException` pri validácii bundlu; opravené cez `InvokeAsync`.

---

## [1.3.6.2] — 2026-05-21

### Fixed
- Pridaný chýbajúci EF migrácia `20260520120000_EventRaffleBundle.Designer.cs` (oprava `column BundledRaffleId does not exist`).
- `PluginMigrationRunner` loguje pending migrácie.
- Jasnejšia chybová správa pri bundle bez nainštalovaného BTCPay Raffle.

---

## [1.3.6.1] — 2026-05-21

### Fixed
- Plugin sa načíta aj **bez** nainštalovaného BTCPay Raffle (runtime resolver, žiadny compile-time ref na Raffle DLL).

---

## [1.3.6.0] — 2026-05-21

### Fork
- **Greenfield purchase API**, **`create-tickets-offline`**, všetky stavy eventov v API, **event raffle bundle**, fork runbook.

### Merged from upstream
- Base pred fork release: lokálny stav po rebase na `upstream/main` (net10).

---

## Template (po release doplniť)

```markdown
## [X.Y.Z-webium] — YYYY-MM-DD

### Fork
- …

### Merged from upstream
- Base upstream version: …
```
