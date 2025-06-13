# TZFinder

*Get IANA time zone id from geo location.*

[![NuGet package](https://img.shields.io/nuget/v/TZFinder.svg)](https://nuget.org/packages/TZFinder)
[API documentation](https://devsko.github.io/TZFinder/api/TZFinder.html)

## Features
+ Based on data from the [Timezone Boundary Builder](https://github.com/evansiroky/timezone-boundary-builder).
+ Includes three compressed data files with different resolutions, selectable in the `.csproj`
+ Custom data files with any resolution can be created easily with a dotnet tool. `Etc/GMT` time zones can also be included (otherwise they are calculated at runtime).
+ Supports disputed areas with two time zones.
+ The entire area of a time zone can be traversed, e.g. for graphical display.
+ Trim safe and AOT compatible.

## Getting started

Install the TZFinder package.
```batch
dotnet package add TZFinder
```

Simple retrieval of the IANA id at a position
```c#
string id = TZFinder.TZLookup.GetTimeZoneId(2.255419f, 47.479083f); // Europe/Paris
```

Optionally, you can select a different data file in the `.csproj` (default: `Medium`)
```xml
  <PropertyGroup>
    <TimeZoneData>Large</TimeZoneData>
  </PropertyGroup>
```

### MSBuild properties
If the TZFinder NuGet package is referenced by an application (directly or transitively), the desired data file is automatically included as `Content` in the project so that it can be used at runtime. The file can also be included as an `EmbeddedResource`, which is the simplest method for Blazor WASM applications, for example.

|Property|Values||
|---|---|---|
|`TimeZoneData`|`Small` **`Medium`** `Large` `None`|Select one of the included data files.|
|`TimeZoneItem`|**`Content`** `EmbeddedResource` `MauiAsset` (automatically used in MAUI apps) `AndroidAsset` (automatically used in Android apps)|The item with which the data file is integrated into the application.|
|`TimeZoneAndroidAssetPack`|asset pack name|Sets the name of the Android asset pack in which the data file should be published.|

At runtime, the file is searched for by default as a file or assembly resource with the name `TZFinder.TimeZoneData.bin`. In all other cases, a path to the data file can be set with `TZLookup.TimeZoneDataPath` or an open readable stream can be set with `TZLookup.TimeZoneDataStream`.
### Data files
TZFinder is based on the [GeoHash](https://en.wikipedia.org/wiki/Geohash) algorithm, in which the Earth's surface is alternately halved longitudinally and laterally. Each halving is counted as 1 level. Every 5 levels correspond to one GeoHash character.
In this way, bounding boxes are generated up to a maximum level that lie within the boundaries of a time zone (maxLevel).

The raw data for the boundaries comes from the [Timezone Boundary Builder](https://github.com/evansiroky/timezone-boundary-builder). To simplify the calculation, all points from the boundaries that are too close to each other are removed (minRingDistance).
#### Included data files
|`TimeZoneData`|`maxLevel`|`minRingDistance`|Size [KB]|GeoHash length|Error [km]|
|---|---|---|---|---|---|
|Small|20|5.000|63|4|20|
|Medium|25|600|310|5|2.4|
|Large|30|152|1.579|6|0.61|
#### Creating your own data file
Install the tool in your project or as global tool and run it with `dotnet tz-build`.
```batch
dotnet new tool-manifest
dotnet tool install TZFinder.Builder
dotnet tz-build 
```
|Parameter|Default||
|---|---|---|
|`--output`, `-o`||Path to the directory where the generated data file will be copied.|
|`--maxLevel`, `-l`|25|Maximum level up to which the bounding boxes are halved.|
|`--minRingDistance`, `-d`|600|Minimum distance in meters between two points of the boundary.|
|`--release`, `-r`|latest|Name of the [Time Zone Boundary Builder release](https://github.com/evansiroky/timezone-boundary-builder/releases).|
|`--includeEtc`, `-e`||Should the Etc/GMT time zones be included? This is only necessary if you want to traverse these time zones later.|
|`--force`||Should an existing data file in the target directory be overwritten?|

The calculation of high-resolution data files is very computationally intensive and can take a long time. Please note that the calculation will utilize the CPU at 100%.

All downloaded and generated files are cached in the `%LocalAppData%\TZFinder` directory. If you encounter any issues, you can delete the entire directory.

To use a custom data file, set `TimeZoneData` to `None` and include the created file in your project.
```xml
  <PropertyGroup>
    <TimeZoneData>None</TimeZoneData>
  </PropertyGroup>

  <ItemGroup>
    <Content Include="TZFinder.TimeZoneData.bin" 
             CopyToOutputDirectory="Always" 
             SkipUnchangedFilesOnCopyAlways="true" />

    <!-- OR -->

    <EmbeddedResource Include="TZFinder.TimeZoneData.bin"
                      LogicalName="TZFinder.TimeZoneData.bin" />
  </ItemGroup>
```
### Platforms

TZFinder is compatible with .NET (Core) 2.0 or higher and .NET Framework 4.7.2 or higher and has been tested with all project types that generate .exe assemblies (Console, ASP.NET Core, WinForms, WPF, WinUI 3, UWP Native, Modern UWP).

#### Blazor WASM
Add this to use TZFinder from a WebAssembly
```xml
  <PropertyGroup>
    <TimeZoneItem>EmbeddedResource</TimeZoneItem>
  </PropertyGroup>
```
#### MAUI
The data file is automatically included as `MauiAsset`. Run this code before using `TZLookup`.
```c#
  TZFinder.TZLookup.TimeZoneDataStream = await FileSystem.OpenAppPackageFileAsync(TZFinder.TZLookup.DataFileName);
```
#### Android
The data file is automatically included as `AndroidAsset`. Run this code before using `TZLookup`.
```c#
  TZFinder.TZLookup.TimeZoneDataStream = Assets!.Open(TZFinder.TZLookup.DataFileName, Access.Streaming);
```
#### Other Platforms
Please let me know, if you have issues using TZFinder on other platforms.
