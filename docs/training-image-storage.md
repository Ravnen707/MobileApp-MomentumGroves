# Supabase Storage for exercise images

Supabase image storage is optional. Standalone web uploads still go to `wwwroot/TrainingImages` and standalone phone downloads still use PC sync. Supabase mode uses a private bucket within your existing project. It needs no paid feature or additional dependency. Files count toward your project's normal Storage and egress limits.

## Supabase Dashboard

1. In **Storage → New bucket**, create `momentum-training-images`. Leave **Public bucket** off. Set the file size limit to **8 MB** and allow only `image/jpeg`, `image/png` and `image/webp`.
2. In **SQL Editor**, paste [the Storage policy script](sql/momentum-groves-training-storage.sql). Replace the first zero UUID in the pasted copy with the same confirmed Auth user UUID as the mobile Data API setup. Run it. The script checks the bucket settings before replacing its named policies.
3. In **SQL Editor**, run [the read-only Storage verification script](sql/verify-momentum-groves-training-storage.sql). Expect one private bucket, one permissive authenticated SELECT policy and four restrictive bucket guards. The signed-in owner can read the bucket. Other accounts and anonymous clients cannot read it. Client sessions cannot upload, replace, or delete its objects.
4. In project **API Keys**, create or copy a server secret key (`sb_secret_…`). Use a key dedicated to the web app if possible. A legacy `service_role` JWT key also works if your project still uses legacy keys. This is an elevated project credential. Keep it in the web host's secret store.

## Web project

From the **MomentumGroves web repository**, use these PowerShell commands in Development. For a deployed web app, set `TrainingImages__SupabaseUrl` and `TrainingImages__ServerKey` in the deployment's secret store.

```powershell
$projectUrl = Read-Host 'Supabase project HTTPS URL'
dotnet user-secrets set 'TrainingImages:SupabaseUrl' $projectUrl --project .\MomentumGroves.csproj
$secret = Read-Host 'Web-only Supabase secret key' -AsSecureString
$webSettings = @{ TrainingImages = @{ ServerKey = [Net.NetworkCredential]::new('', $secret).Password } }
try {
    $webSettings | ConvertTo-Json -Depth 4 | dotnet user-secrets set --project .\MomentumGroves.csproj
}
finally {
    Remove-Variable webSettings, secret
}
```

Restart the updated web app. Keep its existing PostgreSQL settings and mobile Data API flag. Use the training editor on localhost to upload and preview cloud images. The app allows paired mobile clients to fetch private images from the PC over authenticated HTTPS in standalone mode. A publicly forwarded route should never expose this personal web app.

### Copy existing files explicitly

Run these commands from the **MomentumGroves web repository** on the PC that holds `wwwroot/TrainingImages`. The dry run checks references and compares local and remote bytes without uploading. Review any missing, invalid, or conflicting entries before applying.

```powershell
$env:DOTNET_ENVIRONMENT = 'Development' # Use this when settings are in .NET User Secrets.
dotnet run --project .\MomentumGroves.csproj -- migrate-training-images --dry-run
dotnet run --project .\MomentumGroves.csproj -- migrate-training-images --apply
dotnet run --project .\MomentumGroves.csproj -- migrate-training-images --dry-run
```

Apply copies files using the existing names and leaves all local files and programme references in place. The command skips identical objects and never overwrites a different object. If interrupted, run it again. A nonzero exit code means some entries need review. The apply run can retain successful uploads when other entries fail.

### Replacement, removal and rollback

Replace an image through the editor so the new upload gets its own key. Wait for the programme save to complete. **Remove image** clears that exercise's reference. It leaves the underlying object available for the gallery and other exercises. Before manually deleting an object through **Storage**, remove or replace every programme reference to it, including any other use you want to keep. An unchanged reference can continue to show a cached image. Avoid overwriting objects in the Dashboard because clients cache by immutable key.

To suspend cloud uploads, remove the web-only Storage key and restart the web app. Existing cloud images remain in Storage and can still load on the phone while its read policy is enabled. To suspend phone access too, remove only the `momentum_training_images_owner` policy in **Storage → Policies** and leave the restrictive guards and private bucket in place. Standalone local files stay available. Cloud-only objects can be exported from Storage before removing the bucket if you need a local copy.

## Mobile project

Install the updated APK. If **Active data provider** already says **Supabase**, keep your existing URL, publishable key and sign-in. There is no new key or bucket setting on the phone.

With the web app off, open **Training** or tap **Refresh programmes**. The app downloads new references and missing files, then reuses existing cached files. Verify a replacement appears after refresh and a removed reference disappears. In standalone mode, use the existing **Sync to PC** workflow.
