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


## Visual package manager doesnt't work
The visual package manager in Visual Studio does not work for the Buf packages.


## More info on Buf, BSR and NuGet

https://buf.build/blog/bsr-generated-sdks-for-csharp

## How to create a docker image, run it and push it

### Create and Push
Start Docker Desktop and make sure it is running. 
To create a new docker image you have to pass the GITHUB_TOKEN and the BSR_TOKEN to restore the solution.

- Open the `Developer Powershell` window and confirm you are in the `SURIMI-Ecopath` directory.
- `docker build -f .\SURIMI-Ecopath\Dockerfile --build-arg GITHUB_TOKEN=<github token> --build-arg BSR_TOKEN=<BSR token> -t rikkert242/ecopath:latest .`
- `docker push rikkert242/ecopath:latest`
### Run
To run it, you can select "Container (docker file)" in the Start menu of Visual Studio.
 