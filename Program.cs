namespace EpicTransport.EOSSDK.Upgrader;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

//TODO: add logging
public class Program
{
    public static int Main(string[] args)
    {
        Console.WriteLine("Enter the path to the EOS Transport 'EOSSDK' folder (the folder that contains Core, Generated, and Plugins):");
        string? eosTransportPath = Console.ReadLine();

        Console.WriteLine("\nEnter the path to the EOS SDK 'SDK' folder (the folder that contains Source, Tools, and Bin):");
        string? eosSdkPath = Console.ReadLine();

        if (!Directory.Exists(eosTransportPath))
        {
            Console.WriteLine($"Directory not found: '{eosTransportPath}'.");
            return -1;
        }

        if (!Directory.Exists(eosSdkPath))
        {
            Console.WriteLine($"Directory not found: '{eosSdkPath}'.");
            return -2;
        }

        string sdkSrcPath = Path.Combine(eosSdkPath, "Source");
        string sdkAssembliesPath = Path.Combine(eosSdkPath, "Bin");

        Version oldVersion = GetVersion(eosTransportPath);

        string coreTarget = Path.Combine(eosTransportPath, "Core");
        string generatedTarget = Path.Combine(eosTransportPath, "Generated");

        string coreSource = Path.Combine(sdkSrcPath, "Core");
        string generatedSource = Path.Combine(sdkSrcPath, "Generated");

        SyncDirectory(coreSource, coreTarget);
        SyncDirectory(generatedSource, generatedTarget);

        //copying assemblies
        FileCopy(Path.Combine(sdkAssembliesPath, "EOSSDK-Win64-Shipping.dll"), Path.Combine(eosTransportPath, "Plugins", "Win64", "EOSSDK-Win64-Shipping.dll"), true);
        FileCopy(Path.Combine(sdkAssembliesPath, "libEOSSDK-Mac-Shipping.dylib"), Path.Combine(eosTransportPath, "Plugins", "macOS", "libEOSSDK-Mac-Shipping.dylib"), true);
        FileCopy(Path.Combine(sdkAssembliesPath, "libEOSSDK-Linux-Shipping.so"), Path.Combine(eosTransportPath, "Plugins", "Linux", "x86_64", "libEOSSDK-Linux-Shipping.so"), true);
        FileCopy(Path.Combine(sdkAssembliesPath, "libEOSSDK-LinuxArm64-Shipping.so"), Path.Combine(eosTransportPath, "Plugins", "Linux", "ARM64", "libEOSSDK-LinuxArm64-Shipping.so"), true);
        FileCopy(Path.Combine(sdkAssembliesPath, "Android", "static-stdc++", "aar", "eossdk-StaticSTDC-release.aar"), Path.Combine(eosTransportPath, "Plugins", "Android", "eos-sdk.aar"), true);

        string iOSTarget = Path.Combine(eosTransportPath, "Plugins", "iOS", "EOSSDK.framework");
        Directory.Delete(iOSTarget, true);
        Directory.CreateDirectory(iOSTarget);

        DirectoryInfo diSource = new DirectoryInfo(Path.Combine(sdkAssembliesPath, "IOS", "EOSSDK.framework"));
        DirectoryInfo diTarget = new DirectoryInfo(iOSTarget);
        CopyAll(diSource, diTarget);

        Version newVersion = GetVersion(eosTransportPath);

        string assemInfoPath = Path.Combine(eosTransportPath, "AssemblyInfo.cs");
        string assemInfo = File.ReadAllText(assemInfoPath);
        File.WriteAllText(assemInfoPath, assemInfo.Replace(oldVersion.ToString(), newVersion.ToString()));

        string versionPath = Path.Combine(eosTransportPath, "version.txt");
        File.WriteAllText(versionPath, newVersion.ToString());

        DirectoryInfo? parent = Directory.GetParent(eosTransportPath);
        if (parent == null) return -3;

        string eosTransportCSPath = Path.Combine(parent.FullName, "Scripts", "EOSTransport.cs");
        string eosTransportCS = File.ReadAllText(eosTransportCSPath);
        File.WriteAllText(eosTransportCSPath, eosTransportCS.Replace(oldVersion.ToString(), newVersion.ToString()));

        //TODO: delete x86_64 folder from eos-sdk.aar to save space
        return 0;
    }

    private static void SyncDirectory(string sourceRoot, string targetRoot)
    {
        foreach (string dir in Directory.GetDirectories(sourceRoot, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(sourceRoot, dir);
            string targetDir = Path.Combine(targetRoot, rel);

            if (!Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);
        }

        foreach (string sourceFile in Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(sourceRoot, sourceFile);
            string targetFile = Path.Combine(targetRoot, rel);

            if (!File.Exists(targetFile) || !FilesEqual(sourceFile, targetFile))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
                FileCopy(sourceFile, targetFile, true);
            }
            else
                File.SetLastWriteTimeUtc(targetFile, File.GetLastWriteTimeUtc(sourceFile));

            string metaFile = targetFile + ".meta";
            if (File.Exists(metaFile))
                File.SetLastWriteTimeUtc(metaFile, File.GetLastWriteTimeUtc(sourceFile));
        }

        foreach (string targetFile in Directory.GetFiles(targetRoot, "*", SearchOption.AllDirectories).Where(f => !f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)))
        {
            string rel = Path.GetRelativePath(targetRoot, targetFile);
            string sourceFile = Path.Combine(sourceRoot, rel);

            if (!File.Exists(sourceFile)) File.Delete(targetFile);
        }

        foreach (string targetFile in Directory.GetFiles(targetRoot, "*.meta", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(targetRoot, targetFile);
            string sourceFileOrDir = Path.Combine(sourceRoot, rel.Replace(".meta", ""));

            if (!File.Exists(sourceFileOrDir) && !Directory.Exists(sourceFileOrDir)) File.Delete(targetFile);
        }
    }

    private static bool FilesEqual(string path1, string path2)
    {
        FileInfo f1 = new FileInfo(path1);
        FileInfo f2 = new FileInfo(path2);

        if (path1.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
            path1.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
            path1.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
        {
            //format to LF
            string content1 = File.ReadAllText(path1).Replace("\r\n", "\n");
            string content2 = File.ReadAllText(path2).Replace("\r\n", "\n");
            return content1 == content2;
        }

        if (f1.Length != f2.Length) return false;

        const int bufferSize = 1024 * 8;
        using var fs1 = File.OpenRead(path1);
        using var fs2 = File.OpenRead(path2);

        byte[] b1 = new byte[bufferSize];
        byte[] b2 = new byte[bufferSize];

        int read;
        while ((read = fs1.Read(b1, 0, bufferSize)) > 0)
        {
            int read2 = fs2.Read(b2, 0, read);
            if (read2 != read) return false;

            for (int i = 0; i < read; i++)
                if (b1[i] != b2[i]) return false;
        }

        return true;
    }

    private static void CopyAll(DirectoryInfo source, DirectoryInfo target)
    {
        Directory.CreateDirectory(target.FullName);

        foreach (FileInfo file in source.GetFiles())
        {
            string targetPath = Path.Combine(target.FullName, file.Name);
            FileCopy(file.FullName, targetPath, true);
        }

        foreach (DirectoryInfo subDir in source.GetDirectories())
        {
            DirectoryInfo nextTargetSubDir = target.CreateSubdirectory(subDir.Name);
            CopyAll(subDir, nextTargetSubDir);
        }
    }

    private static void FileCopy(string sourceFile, string targetFile, bool overwrite = true)
    {
        if (sourceFile.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
            sourceFile.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
            sourceFile.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
        {
            string content = File.ReadAllText(sourceFile);
            
            //format to LF
            content = content.Replace("\r\n", "\n");
            File.WriteAllText(targetFile, content, new System.Text.UTF8Encoding(false));
            File.SetLastWriteTimeUtc(targetFile, File.GetLastWriteTimeUtc(sourceFile));
        }
        else
        {
            File.Copy(sourceFile, targetFile, overwrite);
            File.SetLastWriteTimeUtc(targetFile, File.GetLastWriteTimeUtc(sourceFile));
        }
    }

    private static Version GetVersion(string path)
    {
        string versionInterfacePath = Path.Combine(path, "Generated", "Version", "VersionInterface.cs");
        string versionInterface = File.ReadAllText(versionInterfacePath);

        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(versionInterface);
        var root = syntaxTree.GetRoot();

        var constFields = root.DescendantNodes().OfType<FieldDeclarationSyntax>().Where(f => f.Modifiers.Any(SyntaxKind.ConstKeyword));

        int major = 0, minor = 0, patch = 0, hotfix = 0;

        foreach (var field in constFields)
        {
            var variable = field.Declaration.Variables.First();
            int value = int.Parse(variable.Initializer!.Value.ToString());

            switch (variable.Identifier.Text)
            {
                case "MAJOR": major = value; break;
                case "MINOR": minor = value; break;
                case "PATCH": patch = value; break;
                case "HOTFIX": hotfix = value; break;
            }
        }

        return new Version(major, minor, patch, hotfix);
    }
}
