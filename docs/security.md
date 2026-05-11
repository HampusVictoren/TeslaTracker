# Säkerhet — Threat Model

**Senast uppdaterad:** 2026-05-11 (Sprint 1)
**Scope för denna version:** Hantering av Tesla refresh tokens. Övriga ytor uppdateras per sprint.

## Tillgångar (assets) vi skyddar

| Tillgång | Klassning | Varför |
|---|---|---|
| **Tesla refresh token** (klartext) | **Kritisk** | Ger full kontroll över användarens Tesla — lås, klimat, härd, mobilappens funktioner |
| Tesla access token (cache) | Hög | Kortare livslängd (~8h) men ger samma åtkomst tills den löper ut |
| Order-status (VIN, leveransfönster) | Låg | PII-light, redan synligt för Tesla själv |
| Push-prenumerationer | Medel | Endpoint-URL kan användas för spam mot specifik enhet |

## Aktörer (threat actors)

1. **Extern angripare** — försöker ta över andras spårning eller exfiltrera refresh tokens.
2. **Bot/scraper** — försöker brute-forcea Order-ID:n eller spam-registrera.
3. **Insider (oss)** — accidentell exponering via loggar/dumpar.
4. **Komprometterad infrastruktur** — databas eller backup som läcker.

## Hotmatris för refresh token-lagring (Sprint 2)

| # | Hot | Aktör | Mitigation | Status |
|---|---|---|---|---|
| T1 | Databas-läcka (Table Storage export, backup) | Extern, Infra-komp. | **Envelope encryption** via Key Vault wrapping key — datakey per token | Sprint 2 |
| T2 | Tokens loggas till Application Insights | Insider | Strukturerad logging med `SensitiveDataFilter` som scrubbar `RefreshToken`, `AccessToken`, `TrackingSecret`-fält | Sprint 2 |
| T3 | Tokens exponeras via felsvar | Extern | `Result<T>.Error.Message` får inte innehålla token-värden; HTTP-lagret returnerar generiska felkoder | Sprint 3 |
| T4 | Tokens hamnar i git (local.settings.json) | Insider | `.gitignore` täcker `local.settings.json`, `appsettings.Development.json`, `.env*`; GitHub secret scanning aktiv | Sprint 1 ✓ |
| T5 | KMS/Key Vault-key kompromiss | Infra-komp. | Key rotation: `TrackingSecretKeyId` lagras per token → kan migrera utan att ändra cipher | Sprint 2 |
| T6 | Memory dumps innehåller plaintext | Extern | Plaintext lever bara i `Application.Tokens.ITokenProtector.UnprotectAsync(...)`-scope under sync; aldrig persistent fält på `Order` | Sprint 2 |

## Hotmatris för publik tjänst-yta (Sprint 3)

| # | Hot | Mitigation | Status |
|---|---|---|---|
| T7 | Bot-registreringar | Cloudflare Turnstile på `POST /api/orders` | Sprint 3 |
| T8 | Brute force på Order-ID | Kombination: (a) rate limit per IP (Table-baserad), (b) registrering kräver giltig token → token verifieras mot Tesla innan vi lagrar = ägarskapsbevis | Sprint 3 |
| T9 | XSS exfiltrerar status från SPA | Strict CSP, ingen `dangerouslySetInnerHTML`, sanera Tesla-svar innan render | Sprint 4 |
| T10 | CSRF mot mutation-endpoints | Custom header (`X-Order-Id`) som klienten måste skicka — preflight blockar enkla forms; SameSite=Strict på ev. cookies | Sprint 3 |
| T11 | Ofrivillig DDoS mot Tesla från timer | Polly circuit breaker + exponential backoff + jitter i sync-tid (slumpa minut inom timme) | Sprint 2-3 |
| T12 | Push-spam mot annans endpoint | Endpoint partitioneras på OrderId → går inte att registrera kanal utan att kunna autentisera mot ordern; FailureCount-tröskel rensar döda endpoints | Sprint 3 |

## DDD-implementerade kontroller (Sprint 1, klart)

- **`TrackingSecret` är ett value object** — kapsar krypterad blob + KV-key-id. Får aldrig hålla plaintext. Plaintext finns bara i `ITokenProtector.UnprotectAsync`-returns och i `RegisterOrderTrackingCommand.RefreshToken`/`TeslaCredential.RefreshToken` (Application-lagret).
- **`Order`-aggregatet exponerar inte `TrackingSecret`-värden** — bara `Secret`-property av typ `TrackingSecret`. Cipher är `ReadOnlyMemory<byte>` (immutable).
- **Domain layer har inga IO-beroenden** — ingen Tesla, ingen Azure, ingen logging. Kan inte oavsiktligt logga eller persistera tokens.
- **`OrderArchived`-event har en `ArchiveReason.TokenRevoked`-variant** — explicit livscykel när token blivit ogiltig.

## Uppföljning

- [ ] Sprint 2: implementera `KeyVaultTokenProtector` + verifiera att plaintext aldrig persisteras
- [ ] Sprint 2: lägg `SensitiveDataFilter` i Application Insights TelemetryInitializer
- [ ] Sprint 3: penetrationstest av rate-limit och Turnstile bypass
- [ ] Sprint 6: Bicep-deploy ska sätta Key Vault soft-delete + purge protection
