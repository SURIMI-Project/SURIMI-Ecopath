# SURIMI


## gRPC interface in Buf Schema Registry
The gRPC interface is descibed by  protobuf files that are stored in https://github.com/Official-EwE/SURIMI-protocol
So there are no proto files in the project!

The proto files are also stored in  https://buf.build/surimi/surimi-protocol

## Add BSR to your global NuGet.config


Edit your %AppData%\NuGet\NuGet.config

And past the corresponding parts from the model NuGet.Config from https://buf.build/surimi/surimi-protocol/sdks/main:grpc/csharp

Part of this is configuring an auth token. This token will last a maximum of a year. So when you have forgotten about it, you will have to generate it again.

Don't install the NuGet.config in the solution because the token is funerable data. That you don't want in your Git repo.

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
