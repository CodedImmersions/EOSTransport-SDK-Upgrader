# EOSTransport SDK Upgrader
Tool to easily upgrade EOSTransport to the latest EOS version.

## Usage
Before running, make sure you have the .NET 10 runtime installed. If you don't have it, you can download it from [here](https://dotnet.microsoft.com/download/dotnet/10.0).

1. Download the latest release from the [releases page](https://github.com/CodedImmersions/EOSTransport-SDK-Upgrader/releases).
2. Run the exe file.
3. For the first prompt, put in `<Your Project Path>/Assets/Mirror/Transports/EOSTransport/EOSSDK`.
4. Go to the [EOS Dashboard](https://dev.epicgames.com/portal). Sign in and select your organization. 
5. Next to the "Your Products" Header, there is a set of buttons. Click on the one that says "Downloads & Release notes".
6. Choose C# for the SDK type, and select the latest version for the version, and then hit download.
7. Once downloaded, extract the zip file and find the folder named "SDK". Put this path into the second prompt, and hit enter.
8. It should then sync the folders automatically, keeping all of your meta files intact.

## Exit Codes
- `0` (`0x0`): Success
- `-1` (`0xFFFFFFFF`): `EOSSDK` folder not found.
- `-2` (`0xFFFFFFFE`): `SDK` folder not found.