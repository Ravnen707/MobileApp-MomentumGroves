# Mobile test examples

Run from the **MomentumGroves.Mobile repository** with the .NET 10 SDK:

```powershell
dotnet test .\tests\MomentumGroves.Mobile.Tests -c Debug
```

The NUnit project is included in `MomentumGroves.Mobile.slnx`. It links the production sources so the tests can run on a desktop without Android. Provider and storage tests live in `Data/`. The `Infrastructure/` folder provides a fake Secure Storage and a scripted HTTP handler. The examples cover an existing local JSON file, an authenticated RPC request with a 64-bit food ID, and a signed-out API read. All HTTP in the examples goes through the fake handler and uses an `.invalid` hostname.

Ideas for additional tests:

- Restore and refresh a previously saved session. Verify that sign-out clears it and that a network outage preserves it for retry.
- Check provider selection and retention of PC-address-specific snapshots when switching modes.
- Exercise malformed responses, timeouts, rejected accounts, and permission errors through the fake transport.
- Verify food pagination, stale updates and a lost response followed by an idempotent retry.
- Use disposable PostgreSQL or a separate Supabase project with synthetic fixtures for tests of grants, simultaneous writes, legacy training documents and recipe relationships. Keep real project configuration out of test runners.
