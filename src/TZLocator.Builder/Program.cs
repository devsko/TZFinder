// Copyright (c) devsko. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Spectre.Builder;
using TZLocator.Builder;
using TZLocator.Builder.Steps;

string baseAppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TZLocator");
string sourcePath = Path.Combine(baseAppDataPath, "timezones{release}.geojson");
string timeZonePath = Path.Combine(baseAppDataPath, "TimeZones.tree");

Directory.CreateDirectory(baseAppDataPath);

CancellationTokenSource cancellation = new();
Console.CancelKeyPress += (sender, e) => cancellation.Cancel();

await BuilderContext.RunAsync<Context>(
    Step.Sequential("Create time zone data",
    [
        new DownloadSource(sourcePath),
        new LoadSource(),
        new CreateTree(),
        new ConsolidateTree(),
        new SerializeTree(timeZonePath),
    ]),
    [
        new MemoryInfo(),
        new GCTimeInfo(),
    ], cancellation.Token);
