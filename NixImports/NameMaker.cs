using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Builder;
using AsmResolver.DotNet.Cloning;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;

namespace NixImports;

public class NameMaker
{
    private readonly ModuleDefinition _module;

    private TokenAllocator _allocator = null!;

    private ModuleDefinition _stub = null!;

    public NameMaker(ModuleDefinition module)
    {
        _module = module;
    }

    public void Run()
    {
        _stub = Utils.CreateStub(_module);

        _allocator = _stub.TokenAllocator;

        InjectLoader();

        InjectPayload();

        string path = Path.Combine(AppContext.BaseDirectory, "Loader.exe");

        var imageBuilder = new ManagedPEImageBuilder();

        var factory = new DotNetDirectoryFactory
        {
            MetadataBuilderFlags = MetadataBuilderFlags.PreserveBlobIndices
                                   | MetadataBuilderFlags.PreserveTypeReferenceIndices,
            MethodBodySerializer = new CilMethodBodySerializer()
            {
                VerifyLabelsOnBuildOverride = false
            }
        };

        imageBuilder.DotNetDirectoryFactory = factory;

        _stub.Write(path, imageBuilder);

        Console.WriteLine($"Successfully created loader image for {_stub.Name}");
    }

    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    private void PatchLoader(int payloadSize, int startToken, int finalToken)
    {
        var method = _stub.ManagedEntryPointMethod!;

        var instructions = method.CilMethodBody!.Instructions;

        var patch = instructions.First(i => i.IsLdcI4() && i.GetLdcI4Constant() == 0x1337);

        patch.Operand = payloadSize;

        patch = instructions.First(i => i.IsLdcI4() && i.GetLdcI4Constant() == 0x1338);

        patch.Operand = startToken;

        patch = instructions.First(i => i.IsLdcI4() && i.GetLdcI4Constant() == 0x1339);

        patch.Operand = finalToken;

        var target = _stub.AssemblyReferences.First(a => a.Name == "Loader");
        target.Name = "netstаndаrd";
    }

    private void InjectPayload()
    {
        byte[] payload = File.ReadAllBytes(_module.FilePath!);

        var chunks = payload.SplitByteArray(RandomNumberGenerator.GetInt32(128, 257));

        var storageClass =
            new TypeDefinition(string.Empty, Utils.GetRandomName(32), TypeAttributes.Class,
                _stub.CorLibTypeFactory.Object.ToTypeDefOrRef())
            {
                Attributes = _stub.ManagedEntryPointMethod!.DeclaringType!.Attributes
            };

        int startToken = _allocator.GetNextAvailableToken(TableIndex.Method).ToInt32();
        foreach (byte[] chunk in chunks)
        {
            string name = Utils.Rot65(Convert.ToBase64String(chunk));

            var method = new MethodDefinition(name, MethodAttributes.Public,
                MethodSignature.CreateInstance(_module.CorLibTypeFactory.UIntPtr,
                    _module.CorLibTypeFactory.UInt64));

            var body = new CilMethodBody(method);

            // checksum body
            body.Instructions.InsertRange(0, new CilInstruction[]
            {
                new(CilOpCodes.Ldarg_1),
                new(CilOpCodes.Ldc_I4, BitConverter.ToInt32(chunk, 0)),
                new(CilOpCodes.Ldelem_I4),
                new(CilOpCodes.Ldind_U4),
                new(CilOpCodes.Ret)
            });

            method.CilMethodBody = body;
            _allocator.AssignNextAvailableToken(method);
            storageClass.Methods.Add(method);
        }

        _stub.TopLevelTypes.Add(storageClass);

        int finalToken = _allocator.GetNextAvailableToken(TableIndex.Method).ToInt32();

        Console.WriteLine($"{nameof(startToken)}=0x{startToken:x8}");
        Console.WriteLine($"{nameof(finalToken)}=0x{finalToken:x8}");

        PatchLoader(payload.Length, startToken, finalToken);
    }

    private void InjectLoader()
    {
        var sourceModule = ModuleDefinition.FromFile(Path.Combine(AppContext.BaseDirectory, "Loader.dll"));
        var cloner = new MemberCloner(_stub);
        var loader = sourceModule.GetAllTypes().First(t => t.Name == "Loader");
        cloner.Include(loader, true);
        var result = cloner.Clone();

        foreach (var clonedType in result.ClonedTopLevelTypes)
            _stub.TopLevelTypes.Add(clonedType);

        foreach (var methodDefinition in result.GetClonedMember(loader).Methods)
        {
            _allocator.AssignNextAvailableToken(methodDefinition);
        }

        result.GetClonedMember(loader).Namespace = "";

        var entryPoint = (MethodDefinition)result.ClonedMembers.First(m => m.Name == "Main");

        entryPoint.Name = Utils.GetRandomName(32);
        entryPoint.DeclaringType!.Name = Utils.GetRandomName(32);

        entryPoint.DeclaringType.RenameMembers();

        _stub.ManagedEntryPointMethod = entryPoint;
    }
}
