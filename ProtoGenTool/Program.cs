using Microsoft.Extensions.Configuration;
using Scriban;
using Scriban.Runtime;

namespace ProtoGenTool;

class Program
{
    static string CurDir = Directory.GetCurrentDirectory();
    
    static void Main(string[] args)
    {
        var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json",optional:true,reloadOnChange:true).Build();
        var options = new Options();
        configuration.GetSection("ProtoGeneratorOptions").Bind(options);
        
        var parseInfo = new ParseInfo
        {
            TypeFileName = options.TypeFileName,
            PbNamespace = options.PbNamespace,
            GenNamespace = options.GenNamespace,
            GenerateTime = DateTime.Now.ToString(),
            Infos = new List<ProtocolInfo>(),
            PbNames = new List<string>()
        };
        var parseEnumInfo = new ParseEnumInfo
        {
            EnumNamespace = options.EnumNamespace,
            GenerateTime = DateTime.Now.ToString(),
            Infos = new List<EnumInfo>()
        };

        var protoFiles = Directory.EnumerateFiles(Path.Combine(CurDir,options.ProtoDir), "*.proto", SearchOption.AllDirectories)
            .ToList();

        if (protoFiles.Count == 0)
        {
            Console.WriteLine("No proto files found.");
            return;
        }
        
        // 删除目标文件夹下所有文件
        var outputDir1 = Path.Combine(CurDir,options.TmpOutput);
        var outputDir2 = Path.Combine(CurDir,options.CustomMsgOutput);
        if (Directory.Exists(outputDir1)) Directory.Delete(outputDir1,true);
        if (Directory.Exists(outputDir2)) Directory.Delete(outputDir2,true);

        Console.WriteLine("Proto files found:");
        protoFiles.ForEach(Console.WriteLine);

        foreach (var filePath in protoFiles)
        {
            var lines = File.ReadAllLines(filePath);
            if (lines.Length == 0) continue;

            var firstLine = lines[0].Trim();
            if (firstLine.Contains("@proto_id"))
            {
                var startId = ParseHead(firstLine);
                if (startId == -1)
                {
                    Console.WriteLine($"File: {filePath} proto id define error，skipping...");
                    continue;
                }

                var pInfos = ParseProto(lines, startId);
                if (pInfos.Any())
                {
                    parseInfo.Infos.AddRange(pInfos);
                    parseInfo.PbNames.Add(Path.GetFileNameWithoutExtension(filePath));
                }
            }
            else if (firstLine.Contains("@proto_enum"))
            {
                var eInfos = ParseEnum(lines);
                if (eInfos.Any())
                {
                    parseEnumInfo.Infos.AddRange(eInfos);
                }
            }
        }

        GenerateProtoCode(options, parseInfo);
        GenerateEnumCode(options, parseEnumInfo);
        GenerateCustomCode(options, parseInfo);
    }

    public static int ParseHead(string line)
    {
        var index = line.IndexOf("@proto_id", StringComparison.Ordinal);
        if (index == -1) return -1;

        var equalsIndex = line.IndexOf('=', index);
        if (equalsIndex == -1) return -1;

        var value = line.Substring(equalsIndex + 1).Trim();
        return int.TryParse(value, out var result) ? result : -1;
    }

    public static List<ProtocolInfo> ParseProto(string[] lines, int startId)
    {
        var infos = new List<ProtocolInfo>();
        var currentId = startId;

        foreach (var line in lines.Skip(1))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("message"))
            {
                var name = trimmed.Substring(7)
                    .Split(['{', ' '], StringSplitOptions.RemoveEmptyEntries)[0];
                if (!string.IsNullOrEmpty(name))
                {
                    infos.Add(new ProtocolInfo { ProtocolID = currentId++, ProtocolName = name });
                }
            }
        }

        return infos;
    }

    public static List<EnumInfo> ParseEnum(string[] lines)
    {
        var infos = new List<EnumInfo>();
        EnumInfo currentEnum = null;
        var enums = new List<EnumStruct>();

        foreach (var line in lines.Skip(1))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("enum"))
            {
                currentEnum = new EnumInfo();
                var name = trimmed.Substring(4)
                    .Split(new[] { '{', ' ' }, StringSplitOptions.RemoveEmptyEntries)[0];
                currentEnum.EnumType = name;
                enums = new List<EnumStruct>();
            }
            else if (trimmed.Contains("=") && currentEnum != null)
            {
                var parts = trimmed.Split('=');
                enums.Add(new EnumStruct
                {
                    EnumName = parts[0].Trim(),
                    EnumId = parts[1].Trim().Replace(";", "")
                });
            }
            else if (trimmed.StartsWith("}") && currentEnum != null)
            {
                currentEnum.Enums = enums;
                infos.Add(currentEnum);
                currentEnum = null;
            }
        }

        return infos;
    }

    static void GenerateProtoCode(Options options, ParseInfo info)
    {
        var templateFile = Path.Combine(CurDir,options.MsgTmp);
        if (!File.Exists(templateFile))
        {
            Console.WriteLine($"Template file not exist: {templateFile}");
            return;
        }
        var templateContent = File.ReadAllText(templateFile);
        var output = Template.Parse(templateContent).Render(info);
        var templateExtension = Path.GetExtension(templateFile);
        SaveFile(Path.Combine(CurDir,options.TmpOutput), $"{options.TypeFileName}{templateExtension}", output);
    }

    static void GenerateEnumCode(Options options, ParseEnumInfo info)
    {
        var templateFile = Path.Combine(CurDir,options.EnumTmp);
        if (!File.Exists(templateFile))
        {
            Console.WriteLine($"Template file not exist: {templateFile}");
            return;
        }

        var templateContent = File.ReadAllText(templateFile);
        var output = Template.Parse(templateContent).Render(info);
        var templateExtension = Path.GetExtension(templateFile);
        SaveFile(Path.Combine(CurDir,options.TmpOutput), $"{options.EnumFileName}{templateExtension}", output);
    }

    static void GenerateCustomCode(Options options, ParseInfo info)
    {
        var templatePath = Path.Combine(CurDir,options.CustomTmp);
        var templateExtension = Path.GetExtension(templatePath);
        var templateContent = File.ReadAllText(templatePath);
        var template = Template.Parse(templateContent);

        foreach (var protocol in info.Infos)
        {
            var context = new ScriptObject();
            context.Import(new
            {
                GenNamespace = options.GenNamespace,
                TypeFileName = options.TypeFileName,
                PbNamespace = options.PbNamespace,
                Info = protocol,
            });
            var output = template.Render(context);
            var fileName = $"{protocol.ProtocolName}{options.CustomMsgSubFix}{templateExtension}";
            SaveFile(Path.Combine(CurDir,options.CustomMsgOutput), fileName, output);
        }
    }

    static void SaveFile(string outputPath, string fileName, string content)
    {
        if (string.IsNullOrEmpty(outputPath))
        {
            Console.WriteLine("Output path is null，skip file generation");
            return;
        }

        Directory.CreateDirectory(outputPath);
        var filePath = Path.Combine(outputPath, fileName);
        File.WriteAllText(filePath, content);
        Console.WriteLine($"File generate success: {filePath}");
    }
}

public class Options
{
    public string ProtoDir { get; set; } = "./Proto";
    public string PbNamespace { get; set; } = "Pb";
    public string GenNamespace { get; set; } = "Generate.Pb";
    public string EnumNamespace { get; set; } = "Generate.Enum";
    public string TmpOutput { get; set; } = "./CSharp/";
    public string CustomMsgOutput { get; set; } = "./CSharp/Msg/";
    public string TypeFileName { get; set; } = "ProtoType.cs";
    public string EnumFileName { get; set; } = "ProtoEnum.cs";
    public string CustomMsgSubFix { get; set; } = "Msg.cs";
    public string MsgTmp { get; set; } = "./Template/CSharp.cs";
    public string EnumTmp { get; set; } = "./Template/CSharpEnum.cs";
    public string CustomTmp { get; set; } = "./Template/CSharpMsg.cs";
}

public class ProtocolInfo
{
    public int ProtocolID { get; set; }
    public string ProtocolName { get; set; }
}

public class EnumStruct
{
    public string EnumId { get; set; }
    public string EnumName { get; set; }
}

public class EnumInfo
{
    public string EnumType { get; set; }
    public List<EnumStruct> Enums { get; set; }
}

public class ParseInfo
{
    public string TypeFileName { get; set; }
    public string PbNamespace { get; set; }
    public string GenNamespace { get; set; }
    public string GenerateTime { get; set; }
    public List<ProtocolInfo> Infos { get; set; }
    public List<string> PbNames { get; set; }
}

public class ParseEnumInfo
{
    public string EnumNamespace { get; set; }
    public string GenerateTime { get; set; }
    public List<EnumInfo> Infos { get; set; }
}