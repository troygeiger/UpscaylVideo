# Instructions Overview

This project is a .NET 9.0 C# project using Avalonia for the UI, in MVVM file
layout. All source code should be in the src folder.

The purpose of the project is to orchestrate Cli applications Upscayl and ffmpeg
for extracting frames, upscaling the frames with upscayl and then pushing the
frames to another running instance outputting to an output video file.

## FFMpeg Wrapper

Project UpscaylVideo.FFMpegWrap is a wrapper class that mostly uses the CliWrap
package but where things need piped, it uses System.Diagnostics process and
returns the process so the ffmpeg process will stay running throughout
processing the video.

## Upscaling Jobs

Currently, upscaling jobs are ran in the JobPageViewModel. That class handles
the frame extraction in batch sizes provided by the user, starting a new process
of upscayl and then pipping the upscaled frames to the another ffmpeg process
that was started for output when starting the job.

## App Configuration

Configurations are stored as json, using System.Text.Json, with the
Models/AppConfiguration as the model that is serialized/deserialized.

## Views

The Avalonia views use a form of xaml and are in the Views folder with an
extension of .axaml.