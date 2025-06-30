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

## Add BSR to your global NuGet.config


TODO!!!!!
At the moment the NuGet.config contains a token to access the BSR.
I don't know why this is needed, but it is.

For now, I added the NuGet.config to the root of the project, so you can use it. But we have to find a way to remove the clear text token in it.

Because this file is stored in the Git repo, the token is visible to everyone who has access to the repo.

Which means potentially EVERYONE!!!

The NuGet.Config is created from https://buf.build/surimi/surimi-protocol/sdks/main:grpc/csharp

Part of this is configuring an auth token. This token will last a maximum of a year. So when you have forgotten about it, you will have to generate it again.


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



## Visual package manager doesnt't work
The visual package manager in Visual Studio does not work for the Buf packages.


## More info on Buf, BSR and NuGet

https://buf.build/blog/bsr-generated-sdks-for-csharp

## How to create a docker image, run it and push it

### Create and Push
Start Docker Desktop and make sure it is running. Then right click the Ecopath project and select "Publish".

In the Publish window click on "Publish"


### Run
To run it, you can select "Container (docker file)" in the Start menu of Visual Studio.

