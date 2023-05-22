using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures.Types;

namespace NixImports;

public static class Utils
{
    public static IEnumerable<byte[]> SplitByteArray(this byte[] inputArray, int size)
    {
        int numArrays = (int)Math.Ceiling((double)inputArray.Length / size);
        var arrays = new List<byte[]>();

        for (int i = 0; i < numArrays; i++)
        {
            int length = Math.Min(size, inputArray.Length - i * size);
            byte[] array = new byte[length];

            Array.Copy(inputArray, i * size, array, 0, length);
            arrays.Add(array);
        }

        return arrays;
    }

    private static void ImportAssemblyTypeReferences(this ModuleDefinition target, ModuleDefinition origin)
    {
        var assembly = origin.Assembly;
        var importer = new ReferenceImporter(target);
        foreach (var ca in assembly!.CustomAttributes.Where(ca => ca.Constructor!.Module == origin))
            ca.Constructor = (ICustomAttributeType)importer.ImportMethod(ca.Constructor!);
    }

    public static string Rot65(string input)
    {
        char[] output = new char[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            output[i] = (char)(input[i] + 'A');
        }

        return new string(output);
    }

    public static ModuleDefinition CreateStub(ModuleDefinition originModule)
    {
        var stubModule =
            new ModuleDefinition(originModule.Name,
                (originModule.CorLibTypeFactory.CorLibScope.GetAssembly() as AssemblyReference)!);

        originModule.Assembly!.Modules.Insert(0, stubModule);

        stubModule.FileCharacteristics = originModule.FileCharacteristics;
        stubModule.DllCharacteristics = originModule.DllCharacteristics;
        stubModule.EncBaseId = originModule.EncBaseId;
        stubModule.EncId = originModule.EncId;
        stubModule.Generation = originModule.Generation;
        stubModule.PEKind = originModule.PEKind;
        stubModule.MachineType = originModule.MachineType;
        stubModule.RuntimeVersion = originModule.RuntimeVersion;
        stubModule.IsBit32Required = originModule.IsBit32Required;
        stubModule.IsBit32Preferred = originModule.IsBit32Preferred;
        stubModule.SubSystem = originModule.SubSystem;

        stubModule.ImportAssemblyTypeReferences(originModule);

        return stubModule;
    }

    public static void RenameMembers(this TypeDefinition type)
    {
        foreach (var method in type.Methods)
        {
            if (method.IsConstructor)
                continue;

            method.Name = GetRandomName(32);

            if (method.ParameterDefinitions.Count == 0)
                continue;

            foreach (var param in method.ParameterDefinitions)
                param.Name = GetRandomName(32);
        }

        foreach (var field in type.Fields)
        {
            field.Name = GetRandomName(32);
        }

        foreach (var field in type.Properties)
        {
            field.Name = GetRandomName(32);
        }

        foreach (var nestedType in type.NestedTypes)
        {
            if (nestedType.IsDelegate)
                nestedType.Name = GetRandomName(32);
        }
    }

    public static string GetRandomName(int length)
    {
        var random = new Random();
        string randomString = new(Enumerable.Repeat(UnicodeCharset, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
        return randomString + '\u2029';
    }

    private static readonly char[] UnicodeCharset = Array.Empty<char>()
        .Concat(Enumerable.Range(0x200b, 5).Select(ord => (char)ord))
        .Concat(Enumerable.Range(0x2029, 6).Select(ord => (char)ord))
        .Concat(Enumerable.Range(0x206a, 6).Select(ord => (char)ord))
        .Except(new[] { '\u2029' })
        .ToArray();
}
