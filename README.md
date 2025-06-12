# TZFinder

*Get IANA time zone id from geo location.*

<!--
[![NuGet package](https://img.shields.io/nuget/v/TZFinder.svg)](https://nuget.org/packages/TZFinder)
-->

# TZFinder

*Get IANA time zone id from geo location.*

<!--
[![NuGet package](https://img.shields.io/nuget/v/TZFinder.svg)](https://nuget.org/packages/TZFinder)
-->

## Features
+ Based on data from the [Timezone Boundary Builder](https://github.com/evansiroky/timezone-boundary-builder).
+ Includes three compressed data files with different resolutions, selectable in the `.csproj`
+ Custom data files can be created easily with a dotnet tool. `Etc` time zones can also be included.
+ Supports areas with 2 time zones.
+ The entire area of a time zone can be traversed, e.g. for graphical display.

## Getting started

Install the TZFinder package.
```batchfile
dotnet package add TZFinder
```

Simple retrieval of the IANA id at a position
```c#
string id = TZFinder.TZLookup.GetTimeZoneId(2.255419f, 47.479083f); // Europe/Paris
```

Optionally, the data file can be selected in the `.csproj` (default: `Medium`)
```xml
  <PropertyGroup>
    <TimeZoneData>Large</TimeZoneData>
  </PropertyGroup>
```

