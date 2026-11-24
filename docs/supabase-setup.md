# Optional Supabase setup

Momentum Groves Mobile starts in standalone mode with private JSON files and manual PC synchronisation. Cloud mode uses the same personal PostgreSQL database as the web app through Supabase Auth and the Data API. Complete the web setup and the Supabase steps before switching the phone to cloud mode.

## 1. Configure the web project

Run these commands from the **MomentumGroves web repository**. Keep the existing `ConnectionStrings:Supabase` secret and its `SSL Mode=VerifyFull` and `Root Certificate` settings. Web SQLite remains the default when that connection string is absent.

For a locally hosted web app, put this in its ignored `.env`, or export it in the shell that starts the web app:

```powershell
$env:MobileDataApi__Enabled = 'true'
dotnet run --project .\MomentumGroves.csproj
```

For a deployed web app, set `MobileDataApi__Enabled=true` in the host environment and restart it. Deploy this version to every web instance connected to the database before exposing mobile access. An older web instance, or a new instance with the setting unset, revokes the API schema grant on PostgreSQL startup. The setting does not create policies or expose the schema. The separate SQLite migration command is outside this setup and reapplies the private schema initialization. Frida continues to use its local SQLite database.

## 2. Configure Supabase Auth and apply the SQL

Use an isolated Supabase project with synthetic data for a dry run. Back up your production database before applying SQL there.

1. In the **Supabase Dashboard → Authentication → Users**, create or locate your confirmed email/password user and copy its UUID. Enable the email provider if needed. For a dedicated personal project, disable public sign-up and anonymous sign-ins in Auth settings. The RPCs enforce the user UUID independently of those switches. [Supabase password authentication](https://supabase.com/docs/guides/auth/passwords)
2. Review [momentum-groves-mobile.sql](sql/momentum-groves-mobile.sql). The transaction adds RLS policies for a non-login RPC role. The five public-facing operations require the configured user's JWT. API roles receive no table or sequence grants. The training RPCs can read only the two programme keys and write only the current key. Food RPCs access custom foods and the minimum import and recipe-link data needed to preserve integrity. The scripts assume the five existing tables and that `momentum_groves` is dedicated to Momentum Groves. The script aborts if there is unrelated direct read access or an unrelated routine executable by the mobile API role. The database administrator keeps management membership in the non-login RPC role for updates and rollback.
3. Paste the script into the Supabase SQL Editor, replace the first zero UUID with your user UUID, and run it. Alternatively, from the **MomentumGroves.Mobile repository**, use the following PowerShell commands with PostgreSQL's `psql` installed. Take the host, username and port from your project's database connection settings. Use the downloaded Supabase CA certificate. The password prompt keeps the password out of the command line.

```powershell
$accountId = [guid](Read-Host 'Supabase Auth user UUID')
$dbHost = Read-Host 'Database host'
$dbPort = Read-Host 'Database port'
$dbUser = Read-Host 'Database user'
$caPath = (Resolve-Path (Read-Host 'Supabase CA certificate path')).Path.Replace('\', '/')
$connection = "host=$dbHost port=$dbPort dbname=postgres user=$dbUser sslmode=verify-full sslrootcert='$caPath'"
$sqlFile = [IO.Path]::GetTempFileName()
try {
    $sql = Get-Content -Raw .\docs\sql\momentum-groves-mobile.sql
    $sql = $sql.Replace("'00000000-0000-0000-0000-000000000000', true", "'$accountId', true")
    [IO.File]::WriteAllText($sqlFile, $sql)
    psql -X -W -v ON_ERROR_STOP=1 -d $connection -f $sqlFile
    if ($LASTEXITCODE -ne 0) { throw 'SQL setup failed. Review the psql error before continuing.' }
}
finally {
    Remove-Item -LiteralPath $sqlFile
}
```

4. In **Integrations → Data API**, enable the Data API and add `momentum_groves` to **Exposed schemas**. Keep automatic grants off. Copy the project HTTPS URL and `sb_publishable_…` key from the project settings. Use only those values on the phone. [Custom schemas](https://supabase.com/docs/guides/api/using-custom-schemas) · [Supabase keys](https://supabase.com/docs/guides/getting-started/api-keys)

## 3. Configure the mobile app

Run these commands from the **MomentumGroves.Mobile repository**:

```powershell
dotnet workload restore .\MomentumGroves.Mobile.csproj
dotnet build .\MomentumGroves.Mobile.csproj -c Debug
```

Install or update the app with your usual deploy method. Open **Settings**, enter the project URL and publishable key, and tap **Use Supabase after restart**. Force stop the app from Android's **App info** screen, then reopen it. Confirm **Active data provider: Supabase** in Settings, then sign in with the email and password of the account from step 2. The phone saves the session in MAUI Secure Storage.

- **Food** reads and updates the shared custom food library. Barcode lookup still reaches Open Food Facts.
- **Training** reads shared programmes and saves weights. Explicit refresh and opening the screen fetch the latest cloud data. A conflict means another client changed the weight. Refresh to review the stored value before saving again.
- The full training editor and standalone PC synchronisation require the PC. In cloud mode, use **PC settings** to configure the editor address. For images that load with the PC off, follow the [private exercise image Storage setup](training-image-storage.md). Standalone PC sync continues downloading images to the mobile cache.
- If Supabase is unavailable, the app reports the error. To return to your saved local foods, pending training edits and PC sync, select **Use standalone after restart**, force stop the app from Android's **App info** screen, then reopen it. Switching modes does not upload existing local data.

## 4. Verify

From **MomentumGroves.Mobile**, using the `$connection` defined in step 2, run the read-only checks or paste the script into the SQL Editor:

```powershell
psql -X -W -v ON_ERROR_STOP=1 -d $connection -f .\docs\sql\verify-momentum-groves-mobile.sql
```

Expect RLS on all five tables, zero direct table and column access for API users, and only the five mobile RPCs executable by `authenticated`. `anon` should have neither schema usage nor RPC execution. The RPC owner should have neither LOGIN nor BYPASSRLS.

On the isolated project, verify that the configured user can open Food and Training with the PC off. Try the same reads signed out and with a second authenticated account. Both must fail. Use synthetic data to verify a food and a weight update in each direction, then test stale edits in two clients. Existing duplicate names can be audited with the following read-only query. The installation script does not consolidate any matches.

```sql
SELECT lower(trim(name)) AS normalized_name, count(*)
FROM momentum_groves.customfoods
GROUP BY lower(trim(name))
HAVING count(*) > 1;
```

## Disable mobile access

Remove `momentum_groves` from **Exposed schemas**. From **MomentumGroves.Mobile**, run:

```powershell
psql -X -W -v ON_ERROR_STOP=1 -d $connection -f .\docs\sql\disable-momentum-groves-mobile.sql
```

The rollback revokes the RPC grants and removes the RPCs and their policies. It retains the shared application rows and the unprivileged non-login role. Set `MobileDataApi__Enabled=false` on the web host and restart it. On the phone, select standalone mode in Settings, force stop the app, then reopen it.
