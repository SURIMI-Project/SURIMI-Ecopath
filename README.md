# SURIMI

## Contents:
- [SURIMI](#surimi)
  - [Contents:](#contents)
  - [gRPC interface in Buf Schema Registry](#grpc-interface-in-buf-schema-registry)
  - [Add BSR to your global NuGet.config](#add-bsr-to-your-global-nugetconfig)
  - [Update the BSR packages and the SURIMI protobuf interface](#update-the-bsr-packages-and-the-surimi-protobuf-interface)
  - [Visual package manager doesnt't work](#visual-package-manager-doesntt-work)
  - [More info on Buf, BSR and NuGet](#more-info-on-buf-bsr-and-nuget)
  - [How to create a docker image, run it and push it](#how-to-create-a-docker-image-run-it-and-push-it)
	- [Create and Push](#create-and-push)
	- [Run](#run)

## gRPC interface in Buf Schema Registry
The gRPC interface is descibed by  protobuf files that are stored in https://github.com/Official-EwE/SURIMI-protocol
So there are no proto files in the project!

The proto files are also stored in  https://buf.build/surimi/surimi-protocol

## NuGet.config
To authenticate to BSR you need to add the BSR source to your global NuGet.config file.


The NuGet.config file in the solution describes what package sources to use. It does not hold the secrets! So this file can be added to the Git repo.

To install the (PERSONAL) passwords encrypted on Windows you can use the following CLI command:

- github-Official-EwE: `dotnet nuget add source "https://nuget.pkg.github.com/Official-EwE/index.json" -n "github-Official-EwE" -u "<your-github-username>" -p "<your-PAT>"`
- github-Surimi-project: `dotnet nuget add source "https://nuget.pkg.github.com/surimi-project/index.json" -n "github-surimi-project" -u "<your-github-username>" -p "<your-PAT>"`
- BSR: `dotnet nuget add source "https://buf.build/gen/nuget/index.json" -n "BSR" -u "<your-github-username>" -p "<your-BSR-Password>"`

This changes the mother of all NuGet.config files which is stored in `C:\Users\<user>\AppData\Roaming\NuGet`. The secrets in this file are encrypted.

## Update the BSR packages and the SURIMI protobuf interface

Go to https://buf.build/surimi/surimi-protocol/sdks/main:grpc/csharp

When you want to install the latest version you copy the Package Manager line:

Example: 
```bash
dotnet add package BSR.Surimi.Surimi-Protocol.Grpc.Csharp -v 1.73.10101.7+d9c7c394022e
```

You can also install previous versions by changing the version number.

But be aware that the version at the top right corner is not the version of the protofiles!!!!

It's the version of the SDK that is generated from the proto files. In the example above it's v1.73.1.

More info on https://buf.build/docs/bsr/generated-sdks/nuget/#versions

### Build Check
When you merge a PR to master, the build check Github Action will check if the solution builds.

### Bridge plugin loading
The BridgePlugin NuGet package places `Eii.Ecopath.BridgePlugin.dll` in `Plugins\BridgePlugin` relative to the application output directory. Keep this directory in build and publish deployments. The EwE runtime wrapper loads the assembly into the default load context when constructed, before `StartAsync` is JIT-compiled; plugin discovery inside `StartAsync` is too late to resolve its direct bridge type references. The EwE engine and plugin manager are still created lazily when a simulation starts.

### Shared scenario inputs
Ecopath expects `<scenario>/<scenario>.eiixml` under the input root, for example `northwestern_med/northwestern_med.eiixml`. The S3 directory copy also downloads other scenario files; it does not apply the EwE filename convention.

### Process memory CSV
Memory monitoring measures the SURIMI-Ecopath process as a whole, independent of any simulation, so you can relate its memory use to the simulations logged by `SimulationScopeManager` in the same period (it logs the number of active simulations whenever one is created or removed). `ProcessMemoryMonitor` is a hosted service: it opens one CSV when the host starts, samples the memory every second, and writes a final `ProcessStopped` row when the host is disposed, for example on Ctrl+C or SIGTERM. A forced kill cannot write that final row. Each run creates a unique `process-memory-<UTC timestamp>-<unique ID>.csv` in `Output\memory` beneath the application directory (for example, `SURIMI-Ecopath\bin\Debug\net10.0\Output\memory` when debugging). The full path is logged when the CSV starts and finishes. Files are local to the host/container; they are not uploaded to blob storage. Preserve or mount this output directory when running in a container.

The CSV contains a header, a `ProcessStarted` baseline row, one `Running` row per second, and a `ProcessStopped` row. Each row is flushed to disk so measurements can be read while the process runs. `TimestampUtc`, `ElapsedSeconds` and `Stage` identify when the measurement was taken. Values are written directly to CSV instead of logging each sample.

Memory values are in MiB (1,048,576 bytes). `WorkingSetMiB` is resident process memory, `PrivateMemoryMiB` is private process memory (not necessarily resident), and `ManagedHeapMiB` estimates live/uncollected managed allocations without forcing garbage collection. `PrivateMemoryDeltaMiB` is the change from the baseline taken when the host started. `SampledPeakWorkingSetMiB`, `SampledPeakPrivateMemoryMiB`, and `SampledPeakPrivateMemoryDeltaMiB` cover the whole run; short spikes between samples can be missed.

In Excel, use **Data > From Text/CSV**, select the file, and choose **UTF-8** encoding and **Comma** delimiter. Numbers use a decimal point regardless of the host's locale; for Dutch Excel, import numeric columns using the **English (United States)** locale if necessary. Plot `ElapsedSeconds` against `WorkingSetMiB` or `PrivateMemoryMiB` to view usage over time.

For sizing separate worker processes, use the sampled peak **total** process memory, not just the baseline delta, and leave additional headroom. These measurements include the entire host, runtime, plugins, and request handling, not exclusively EwE allocations. Finalization does not force garbage collection, so its ending value may still include retained/uncollected memory. For the clearest baseline, restart the host before measuring a representative simulation through initialization, steps, and finalization.

### Initial biomass and fishing mortality
`GetInitialBiomasses` and `GetInitialFishingMortalities` read input blobs from the request's `ScenarioName` subdirectory. Locally these are `Includes\<ScenarioName>\initial_biomasses.csv` and `Includes\<ScenarioName>\initial_catch.csv`; S3 uses the same scenario directory under the configured input prefix. These RPCs use the shared blob store and should be called before `InitialiseSimulation`, which changes its active scenario subdirectory.

`IInitialStateFileService` documents the initial-state file-reading contract and returns values keyed by the complete species identity (`SpeciesCode`, `LengthClass`, `Age`, `LifeStage`).

Both CSV files use `SpeciesCode,LengthClass,Age,LifeStage` as the species key, with invariant-culture numeric columns named `Biomass` and `Catch`, respectively. Biomass is returned unchanged. Fishing mortality is annual initial catch divided by initial biomass, matched by the complete species key rather than row order. Both files must contain the same species keys. Missing files, malformed or duplicate rows, negative/non-finite values, and zero biomass in the mortality calculation produce errors rather than incomplete results.

## Visual package manager doesnt't work
The visual package manager in Visual Studio does not work for the Buf packages.


## More info on Buf, BSR and NuGet

https://buf.build/blog/bsr-generated-sdks-for-csharp

## How to create a docker image, run it and push it

## Build Docker image
```bash
dotnet build /t:BuildPushDockerImage -v:detailed
```

### Create and Push
Start Docker Desktop and make sure it is running. 
To create a new docker image you have to pass the GITHUB_TOKEN and the BSR_TOKEN to restore the solution. When they are stored as env vars, you can do that like this:

- Open the `Developer Powershell` window and confirm you are in the `SURIMI-Ecopath` base directory.
- `docker build --no-cache -f .\SURIMI-Ecopath\Dockerfile --secret id=GITHUB_TOKEN,env=SURIMI_DOCKER_BUILD_GITHUB_TOKEN --secret id=BSR_TOKEN,env=SURIMI_DOCKER_BUILD_BSR_TOKEN -t ghcr.io/surimi-project/surimiecopath:multicore .`
- `docker push ghcr.io/surimi-project/surimiecopath:multicore`
