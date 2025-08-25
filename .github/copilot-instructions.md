# Instructions Overview

This project is a .NET 9.0 C# project using Avalonia for the UI, in MVVM file layout. All source code should be in the `src` folder.

## Purpose

The application orchestrates CLI applications Upscayl and ffmpeg for extracting frames from video, upscaling the frames with Upscayl, and then reassembling the video using ffmpeg. It supports batch processing, queueing, and progress/cancellation controls.

## Architecture

- **MVVM Pattern**: ViewModels in `src/UpscaylVideo/ViewModels`, Views in `src/UpscaylVideo/Views` (`.axaml`), Models in `src/UpscaylVideo/Models`.
- **Service Layer**: All job execution, queue management, and progress/cancellation logic is handled in `src/UpscaylVideo/Services/JobProcessingService.cs`. UI/ViewModels interact with this service for all job-related actions. For XAML binding compatibility, the service is exposed to the UI as an alias property named `JobQueueService` via `PageManager` (it returns `JobProcessingService.Instance`).
- **FFMpeg Wrapper**: `UpscaylVideo.FFMpegWrap` project provides a wrapper for ffmpeg/ffprobe, using CliWrap for most operations, and System.Diagnostics.Process for persistent or piped operations.
- **App Configuration**: Stored as JSON using System.Text.Json, with the model in `Models/AppConfiguration.cs`. It includes persisted Upscayl options like `LastImageFormat` (for `-f`), `LastTileSize` (UI value 31 maps to `-t 0` meaning auto), and `UpscaylThreadConfig` (for `-j load:proc:save`, regex-validated with UI error state).

## Job Queue & Progress

- Users can enqueue multiple upscaling jobs, which are processed in order.
- The queue is managed by `JobProcessingService` (exposed to bindings as `JobQueueService`), which exposes observable properties for queue state, current job, progress, ETA, and cancellation.
- Progress and status reporting (frames, percent, ETA, etc.) is handled in the service, not in the ViewModel. ETA uses a rolling average per-frame duration and is reset/seeded appropriately between jobs.
- Cancellation is supported for the current job.
- The UI (queue page, main page, toolbar) binds to these observable properties for real-time feedback.

## UX/UI

- The queue and job progress are displayed in the UI using DataGrids and progress bars, bound to the service properties.
- Start/Cancel buttons are context-sensitive and reflect the queue state.
- Output file paths are set at enqueue time and shown in the queue.
- The UI is being refactored for a new UX that relies on the service for all job/progress state.

## Video Pipeline Notes

- Frames are extracted from the source video via an ffmpeg image pipe.
- Frames are upscaled in chunks by invoking Upscayl with args built from the job and configuration:
  - `-f` output image format (e.g., png/jpg/webp) from job/config.
  - `-t` tile size where UI value `<=31` maps to `0` (Upscayl auto), otherwise the numeric value.
  - `-j` global threads config from `AppConfiguration.UpscaylThreadConfig`.
- Reassembly uses `PngVideoHelper`, which streams upscaled frames into ffmpeg; optional frame interpolation to a target FPS can be selected per job.

## Localization

- All displayed strings must be added to the main `Localization.resx` file in `src/UpscaylVideo/`.
- For additional languages, add translations to the corresponding `.resx` files (e.g., `Localization.de-de.resx`).
- When creating new displayed strings, add them to the main resource file and provide translations as needed.
- Keys that are specific to a view should be prefixed with the window or page name (e.g., `MainWindow_TheStringName`).
- Bind localizations in the views using Avalonia's binding mechanisms.
- When binding to localized strings, ensure an xmlns is pointing to `clr-namespace:UpscaylVideo`.

## Development Notes

- All orchestration logic (frame extraction, upscaling, merging, progress, cancellation) should be implemented in the service layer, not in ViewModels.
- ViewModels should only handle UI state and delegate all job actions to the service.
- When adding new features, prefer to extend the service and expose new observable properties for UI binding.
- Use the existing MVVM and observable patterns for all new UI features.
- Build Upscayl invocation consistently with `-f`, `-t` (31->0 mapping), and `-j` from configuration.
- All new code should be placed in the appropriate `src` subfolder.

## File Structure

- `src/UpscaylVideo/Services/JobProcessingService.cs`: Main job queue and orchestration logic (exposed to the UI via `PageManager.JobQueueService`).
- `src/UpscaylVideo/Services/PageManager.cs`: Hosts the current page and exposes the job service for bindings.
- `src/UpscaylVideo/ViewModels/QueuePageViewModel.cs`, `MainPageViewModel.cs`, etc.: UI logic, delegates to service.
- `src/UpscaylVideo/Views/QueuePageView.axaml`, etc.: Avalonia XAML views.
- `src/UpscaylVideo.FFMpegWrap/`: ffmpeg/ffprobe wrappers and helpers.
- `src/UpscaylVideo/Models/`: Data models, including `UpscaleJob`, `AppConfiguration`, etc.

## Build & Release

- GitHub Actions workflow builds and releases Linux and Windows x64 binaries as zips on GitHub Releases.

## Tips for Copilot

- Always prefer service-based logic for anything related to job execution, progress, or queue state.
- Use observable properties for anything the UI needs to bind to.
- Avoid putting orchestration or process logic in ViewModels.
- When in doubt, check if a property or method should be in the service or the ViewModel.
- Keep all new code in the `src` folder, following the existing structure.

## Avalonia Views

- All UI views are implemented as `.axaml` files in `src/UpscaylVideo/Views`.
- Each view should have a corresponding `.axaml.cs` code-behind file (e.g., `MyPageView.axaml` and `MyPageView.axaml.cs`).
- The code-behind file is required for event handlers, control logic, and proper Avalonia designer support.
- When adding a new view, always create both the `.axaml` and `.axaml.cs` files, and ensure the class in the code-behind matches the XAML root element's `x:Class` attribute.
- Bindings and UI logic should be handled via ViewModels, but any code-behind needed for Avalonia-specific features or interop should be placed in the `.axaml.cs` file.
