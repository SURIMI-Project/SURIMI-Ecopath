# Copilot instructions for SURIMI-Ecopath

## General Guidelines
- Always place `using` directives at the top of the file, outside the namespace block. Never nest `using` statements inside a namespace declaration.
- In .NET SDK-style projects, do not explicitly add `using` directives for namespaces that are already covered by implicit global usings (e.g., System, System.Linq, System.Collections.Generic, System.Threading.Tasks, etc.). Always treat these as redundant and remove them if they are not used.
- Always remove unused `using` directives and sort the remaining ones (System namespaces first, then others alphabetically) in every file that is created or modified.

## Build and test

Before restoring, building, or testing, make sure the private NuGet feeds from `NuGet.config` are authenticated in the **global** NuGet config. This repository depends on GitHub Packages (`SURIMI.*`, `Eii.*`) and Buf Schema Registry (`BSR.*`), and the README documents the `dotnet nuget add source ...` commands to configure those credentials.

- Full build: `dotnet build .\SURIMI-Ecopath.sln --no-restore --no-logo`
- Full test suite: `dotnet test .\SURIMI-Ecopath.sln --no-logo`
- Single test: `dotnet test .\SURIMI-Ecopath.Tests\SURIMI-Ecopath.Tests.csproj --no-logo --filter "FullyQualifiedName~Ecopath.Tests.EwE.EwEControllerTests.GetBiomassAsync_ReturnsBiomass"`
- Docker image build: `docker build -f .\SURIMI-Ecopath\Dockerfile --build-arg GITHUB_TOKEN=<token> --build-arg BSR_TOKEN=<token> -t ghcr.io/official-ewe/surimiecopath:latest .`

There is no repository-defined lint command or `dotnet format` workflow; use `.editorconfig` as the formatting/style source of truth.

## High-level architecture

This solution is a .NET 8 gRPC host around the EwE Ecopath/Ecosim/Ecospace engine:

- `SURIMI-Ecopath\Program.cs` wires the ASP.NET Core gRPC host, registers all EwE services as singletons, sets up metadata interceptors, and chooses the blob store implementation. If `AWS_ACCESS_KEY_ID` is present it uses `S3BlobStore`; otherwise it uses a local `LocalBlobStore` rooted at `Includes` and `Output`. It also optionally loads extra environment variables from Vault before serving requests.
- The gRPC contract is **not** stored in this repository. The service uses the generated package `BSR.Surimi.Surimi-Protocol.Grpc.Csharp`, so schema changes come from the external `SURIMI-protocol`/Buf pipeline instead of local `.proto` files.
- `Services\EcologyService.cs` is the transport layer. It converts between gRPC request/response types and `SURIMI.Datamodel` objects, calls into the controller, and delegates simulation reservation checks to `CheckSimulationService`.
- `Services\CheckSimulationService.cs` enforces a single active simulation reservation for the whole process. Every simulation RPC should reserve/check/release through this service; it writes the chosen host back as gRPC response metadata.
- `EwE\EwEController.cs` owns the actual EwE runtime lifecycle. It loads the model from blob storage, runs Ecopath/Ecosim/Ecospace, starts Ecospace on a dedicated thread, and uses the bridge plugin callback to synchronize external SURIMI data with EwE time steps.
- The callback flow in `EwEController` is important: prices are integrated at `BeginTimeStep`, externally computed catch dispositions are injected at `EffortDistrPost`, and biomass/catch/sales snapshots are cached at `EndTimeStep` right before the controller pauses and waits for the next SURIMI step.
- `EwE\EwEConfiguration.cs` builds the mapping layer between SURIMI concepts and EwE groups/fleets/markets. Species mappings come from the incoming simulation configuration; fleet and market mappings are still hard-coded there.
- `EwE\GroupSpeciesProportions.cs` and `GroupSpeciesProportionsFactory.cs` maintain per-cell species proportions inside an EwE functional group so the service can accept species-level external fishing and still return species-level biomass from function-group-based EwE state.
- `EwE\Wrapper\IEwECore` / `EwECore` and `IPluginManager` / `PluginManager` wrap the underlying EwE classes so the controller can be dependency-injected and unit-tested without binding tests to concrete EwE runtime objects.

## Key conventions

- Keep transport mapping in `EcologyService` and EwE business logic in `EwEController`. New gRPC methods should follow the same split instead of putting EwE state changes directly in the service class.
- The EwE data structures use **one-based indexing**. Loops over groups, fleets, rows, and columns deliberately start at `1` and use `<=` bounds.
- Spatial outputs use **cell centroids**, not raw row/column corners. Existing biomass and catch responses convert coordinates with `RowToLat(ir + 0.5)` and `ColToLon(ic + 0.5)`.
- Biomass/catch values cross a unit boundary when moving between SURIMI DTOs and EwE arrays. Reuse `DensityToKg` and `KgToDensity` instead of duplicating conversions.
- Species/fleet/market matching relies on `MultiLevelKey`, the controlled-vocabulary registries, and `EwEMapping`. Reuse that machinery for new mapping work instead of introducing ad-hoc string matching.
- `EwEController.RunState` (`idle`, `starting`, `waiting`, `running`, `stopping`) is the coordination mechanism for stepping Ecospace. Changes to simulation flow should preserve the pause/resume handshake rather than bypassing it.
- Tests in `SURIMI-Ecopath.Tests` mock `IEwECore`, `IEwEConfiguration`, `IPluginManager`, `IKeyFieldDescriptorRegistry`, and `IBlobStore`. Follow that seam when adding tests instead of constructing a real EwE engine.
- `UpdateEnvironmentVariablesAsync`, `UpdateRegulationsAsync`, and `GetFishingActivityAsync` in `EwEController` are currently placeholders/stubs. If work touches those gRPC endpoints, inspect the controller implementation first instead of assuming they are fully wired.
