# Changelog — Webium fork (Satoshi Tickets)

Upstream autor: [TChukwuleta/BTCPayServerPlugins](https://github.com/TChukwuleta/BTCPayServerPlugins).  
Údržba fork: [webiumsk/BTCPayServerPlugins](https://github.com/webiumsk/BTCPayServerPlugins).

Postup pri každom release: [FORK_MAINTENANCE.md](./FORK_MAINTENANCE.md).

---

## [Unreleased]

### Added
- **Event raffle bundle** — polia `bundledRaffleId`, `bundledRaffleTicketsPerAdmission` na evente; po `InvoiceSettled` alokácia cez BTCPay Raffle plugin (per e-mail pri `sendIndividually`). Vetva `feature/event-raffle-bundle`, cieľ verzie **1.3.6.0+**.

### Pending merge to `main`
- **Greenfield purchase API** + **`create-tickets-offline`** (WordPress / manuálne priradenie platby) — vetva `feature/greenfield-purchase-api`.
- **Return disabled events** v Greenfield liste — vetva `feature/return-disabled-events`.
- **Upstream merge** — autor `upstream/main` ~1.3.61 (stav k máju 2026); integračná vetva ešte neurobená.

---

## Template (po release doplniť)

```markdown
## [X.Y.Z-webium] — YYYY-MM-DD

### Fork
- …

### Merged from upstream
- Base upstream version: …
```
