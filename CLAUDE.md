# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

HealthTracker is an offline-first, single-user .NET MAUI app targeting **Android only** (`net9.0-android`). It tracks medicines/doses, health metrics, medical reports, doctor visits, and a timeline, with optional Gemini-powered OCR parsing and chat. All data is local SQLite plus files under the app data directory. There is no cloud sync.

The repo is not a git repository. `PROJECT_LOGIC_DOCUMENTATION.md` at the root is a detailed, accurate walkthrough of every service and ViewModel; read it before touching business logic. `health_tracker_pdf.html` is the original spec.

## Commands

The solution file lives in the app folder, not the repo root: `HealthTracker/HealthTracker.sln`. It includes both the app and the test project.

```powershell
# Build the Android app. Name the csproj explicitly: the folder also holds the .sln, and building
# the .sln with -f net9.0-android fails because the test project has no Android target.
dotnet build HealthTracker/HealthTracker.csproj -f net9.0-android "-p:AndroidSdkDirectory=C:/Users/BS842/AppData/Local/Android/Sdk"

# Run unit tests (from repo root)
dotnet test HealthTracker.Tests/HealthTracker.Tests.csproj

# Run a single test class / method
dotnet test HealthTracker.Tests/HealthTracker.Tests.csproj --filter "FullyQualifiedName~MedicineServiceTests"
dotnet test HealthTracker.Tests/HealthTracker.Tests.csproj --filter "FullyQualifiedName~MedicineServiceTests.AddMedicine_WithNoSchedules_ThrowsInvalidOperationException"
```

Toolchain notes:
- `HealthTracker/global.json` pins the SDK to 9.0.3xx. The `maui-android` workload for .NET 9 is installed.
- `HealthTracker/NuGet.Config` and `HealthTracker.Tests/nuget.config` both `<clear/>` sources and use only nuget.org (a corporate Telerik feed was blocking workload restore). Keep them.
- A clean app build produces 0 errors and 2 harmless SkiaSharp page-size warnings.
- Device deploy is done from Visual Studio 2022 by opening the `.sln`; there is no CLI deploy script.

## Architecture

- **`App.Services`** (static `IServiceProvider`) is how pages resolve other pages for navigation. Navigation is mostly `Navigation.PushAsync(App.Services.GetService<SomePage>())` with data passed by calling a method on the page/ViewModel first (e.g. `page.SetMedicineId(id)`, `await page.LoadMedicine(id)`), not Shell query properties. Shell routes are registered in `AppShell.xaml.cs` but only the PIN lock uses `GoToAsync`.
- **`BaseViewModel`** is a hand-rolled `INotifyPropertyChanged` with `IsBusy`/`HasError`/`ErrorMessage`. No CommunityToolkit.Mvvm; properties and commands are written out manually.
- **`BaseRepository<T>`** gives generic CRUD over `sqlite-net-pcl`; concrete repositories add typed queries. Models are plain classes with SQLite attributes. Dates and times are stored as strings (`"yyyy-MM-dd"`, `"HH:mm"`); see `Helpers/DateTimeHelper.cs`.
- **`DatabaseService.GetConnectionAsync`** lazily opens `healthapp.db`, calls `CreateTableAsync` for every model, then runs hand-written migrations in `MigrateAsync` (column removals via table rebuild). When you add a model, add it to both `GetConnectionAsync` and `DeleteAllDataAsync`. When you change a column in a way `CreateTableAsync` won't handle, add a migration step.
- **Reminders** (`Services/ReminderService.cs`, behind `IReminderService`) use Plugin.LocalNotification with deterministic notification IDs derived from medicine and schedule IDs, two daily notifications per schedule (15 minutes early and on time). `App.OnStart` calls `RescheduleAll()` every launch, so reminder state is rebuilt from the DB, not persisted.
- **Daily housekeeping** runs from `TodayViewModel.Load`: `MedicineService.AutoLogMissedDoses()` and `CheckAndCompleteCourses()` run before the Today list is rendered. Dose/stock/course invariants live in `MedicineService`; don't update those tables directly from ViewModels.
- **Android-specific code** lives in `Platforms/Android/` (home-screen widget that reads SQLite read-only, exact-alarm permission prompt in `App.xaml.cs` under `#if ANDROID`). The other platform folders are template leftovers; only `net9.0-android` is in `TargetFrameworks`.
- **AI features** call the Gemini REST API directly with a user-supplied key stored in MAUI `Preferences` under `gemini_api_key`; the PIN is in `SecureStorage` under `app_pin`. Both are optional and every AI path falls back to regex parsers.

## Tests

`HealthTracker.Tests` is a plain `net9.0` xUnit project that **does not reference the MAUI app**. It compiles app sources in via `<Compile Include="..\HealthTracker\...">` links, and only files with no MAUI dependency can be linked. Two test doubles live in the same namespaces as the real types and shadow them:

- `TestHelpers/TestDatabaseService.cs` defines `HealthTracker.Services.DatabaseService` backed by an in-memory SQLite connection.
- `TestHelpers/NullReminderService.cs` is a no-op `IReminderService`.

Consequences:
- If you add a dependency to a linked file (e.g. a new repository injected into `MedicineService`), you must also link the new model/repository in `HealthTracker.Tests.csproj` and add its `CreateTableAsync` to the test `DatabaseService`, or the test project stops compiling.
- Never reference `Microsoft.Maui.*`, `Preferences`, `FileSystem`, `Shell`, etc. from a linked file. Put MAUI-touching code in files that are not linked (e.g. `FileStorageHelper.cs`, `ReminderService.cs`).

- **PIN lock** is pushed as a modal page from `App.CheckPinAndLock`, and `PinLockPage` swallows the back button. Keep it modal; a pushed page can be popped by the Shell back arrow.
- **Notification IDs** are the schedule row id (on-time) and 1,000,000 + schedule id (early reminder). `RescheduleAll` runs `CancelAll` first, so the scheme can change between versions safely.
