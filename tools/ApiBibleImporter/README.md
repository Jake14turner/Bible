# API.Bible licensed translation importer

This tool downloads a licensed Bible into the private data directory used by Bible Study Studio. It never stores the API key.

Default output:

`%LOCALAPPDATA%\Bible Study Studio\LicensedData\NASB1995\nasb1995.json`

For this private personal-use repository, refresh the tracked licensed-data file with:

```powershell
dotnet run --project tools\ApiBibleImporter\ApiBibleImporter.csproj -- --output LicensedData\NASB1995\nasb1995.json
```

To repair missing spaces at structured poetic-line boundaries in an existing import without making any API calls:

```powershell
dotnet run --project tools\ApiBibleImporter\ApiBibleImporter.csproj -- --repair-existing LicensedData\NASB1995\nasb1995.json
```

Run it from the repository root:

```powershell
dotnet run --project tools\ApiBibleImporter\ApiBibleImporter.csproj
```

If `BIBLE_API_KEY` is not set, the tool securely prompts for it. To inspect the Bible editions authorized for the key without downloading content:

```powershell
dotnet run --project tools\ApiBibleImporter\ApiBibleImporter.csproj -- --list
```

If NASB 1995 cannot be selected unambiguously, rerun with the exact ID shown by `--list`:

```powershell
dotnet run --project tools\ApiBibleImporter\ApiBibleImporter.csproj -- --bible-id YOUR_BIBLE_ID
```

The importer:

- uses passage batches capped at 190 verses;
- saves each completed batch for safe resume;
- validates the final verse set against `asv.json` references;
- preserves the API.Bible copyright notice and source metadata;
- skips network access while a local import is still within its 30-day review window;
- performs a one-call metadata review after 30 days and only redownloads when the source changed.

Use `--force` to redownload regardless of the local review date. Use `--output PATH` to select another private location.

The NASB 1995 content is authorized for this project's personal use only. The final file may remain in this private repository under that permission, but the repository must never be made public or shared with unauthorized collaborators.
