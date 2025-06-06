// Copyright (c) devsko. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Spectre.Builder;
using TZLocator.Builder;
using TZLocator.Builder.Steps;

CancellationTokenSource cancellation = new();
Console.CancelKeyPress += (sender, e) => cancellation.Cancel();

await BuilderContext.RunAsync<Context>(
    Step.Sequential("Create time zone data",
    [
        new DownloadSource(),
        new LoadSource(),
        new CreateTree(),
        new ConsolidateTree(),
        new SerializeTree(),
    ]),
    [
        new MemoryInfo(),
        new GCTimeInfo(),
    ], cancellation.Token);
