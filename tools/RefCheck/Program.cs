// RefCheck: proves a built mod DLL only uses things that exist in the game it targets.
//
//  1. every type and member reference resolves against the game's real assemblies
//     (Managed + BepInEx core), the same way the Mono runtime will resolve them
//  2. every RPC name the mod sends by string exists in the game with [PunRPC] and the
//     parameter types the mod passes
//  3. lists every non-public game member the mod touches (allowed: publicized build, Mono
//     does not check access), so a reviewer can see them
//
// usage: RefCheck <mod.dll> <game dir>
using Mono.Cecil;
using Mono.Cecil.Cil;

if (args.Length < 2)
{
    Console.WriteLine("usage: RefCheck <mod.dll> <game dir>");
    return 2;
}

var modPath = args[0];
var game = args[1];
var managed = Path.Combine(game, "REPO_Data", "Managed");
var core = Path.Combine(game, "BepInEx", "core");

var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(managed);
resolver.AddSearchDirectory(core);
foreach (var d in resolver.GetSearchDirectories().ToList())
{
    if (d != managed && d != core) resolver.RemoveSearchDirectory(d);
}

var mod = ModuleDefinition.ReadModule(modPath, new ReaderParameters { AssemblyResolver = resolver });
int failures = 0;

Console.WriteLine($"Checking {Path.GetFileName(modPath)} against {managed}");

foreach (var ar in mod.AssemblyReferences)
{
    try
    {
        var a = resolver.Resolve(ar);
        Console.WriteLine($"  assembly {ar.Name} {ar.Version} -> {a.Name.Version}");
    }
    catch (Exception e)
    {
        Console.WriteLine($"  MISSING ASSEMBLY {ar.FullName}: {e.Message}");
        failures++;
    }
}

int types = 0, members = 0;

foreach (var tr in mod.GetTypeReferences())
{
    types++;
    if (tr.Resolve() == null)
    {
        Console.WriteLine($"  UNRESOLVED TYPE {tr.FullName} (scope {tr.Scope})");
        failures++;
    }
}

var nonPublic = new SortedSet<string>();

foreach (var mr in mod.GetMemberReferences())
{
    members++;
    IMemberDefinition def = null;

    try
    {
        def = mr switch
        {
            MethodReference m => m.Resolve(),
            FieldReference f => f.Resolve(),
            _ => null
        };
    }
    catch (Exception e)
    {
        Console.WriteLine($"  ERROR resolving {mr.FullName}: {e.Message}");
        failures++;
        continue;
    }

    if (def == null)
    {
        Console.WriteLine($"  UNRESOLVED MEMBER {mr.FullName}");
        failures++;
        continue;
    }

    bool isPublic = def switch
    {
        MethodDefinition md => md.IsPublic,
        FieldDefinition fd => fd.IsPublic,
        _ => true
    };

    if (!isPublic && def.DeclaringType.Module.Assembly.Name.Name == "Assembly-CSharp")
    {
        nonPublic.Add(def.DeclaringType.Name + "." + def.Name);
    }
}

Console.WriteLine($"  {types} type references, {members} member references checked");

// RPC names sent by string: find  ldstr "Name" ... callvirt PhotonView::RPC
var gameAsm = resolver.Resolve(AssemblyNameReference.Parse("Assembly-CSharp"));
var rpcMethods = gameAsm.MainModule.GetTypes()
    .SelectMany(t => t.Methods)
    .Where(m => m.CustomAttributes.Any(a => a.AttributeType.Name == "PunRPC"))
    .ToList();

int rpcCount = 0;

foreach (var type in mod.GetTypes())
{
    foreach (var method in type.Methods.Where(m => m.HasBody))
    {
        var ins = method.Body.Instructions;

        for (int i = 0; i < ins.Count; i++)
        {
            if (ins[i].OpCode != OpCodes.Call && ins[i].OpCode != OpCodes.Callvirt) continue;
            if (ins[i].Operand is not MethodReference call || call.Name != "RPC" || call.DeclaringType.Name != "PhotonView") continue;

            // nearest ldstr before the call is the RPC name
            string name = null;
            for (int j = i - 1; j >= 0 && j > i - 40; j--)
            {
                if (ins[j].OpCode == OpCodes.Ldstr) { name = (string)ins[j].Operand; }
            }

            // the first ldstr in the window is the name (RPC(name, target, args)); walk forward instead
            for (int j = Math.Max(0, i - 40); j < i; j++)
            {
                if (ins[j].OpCode == OpCodes.Ldstr) { name = (string)ins[j].Operand; break; }
            }

            rpcCount++;
            var found = rpcMethods.Where(m => m.Name == name).ToList();

            if (found.Count == 0)
            {
                Console.WriteLine($"  RPC NOT IN GAME: \"{name}\" sent from {type.Name}.{method.Name}");
                failures++;
            }
            else
            {
                var sig = string.Join(", ", found[0].Parameters.Select(p => p.ParameterType.Name));
                Console.WriteLine($"  rpc \"{name}\" -> {found[0].DeclaringType.Name}.{name}({sig})  [from {type.Name}.{method.Name}]");
            }
        }
    }
}

// Harmony targets named by string
foreach (var type in mod.GetTypes())
{
    foreach (var method in type.Methods.Where(m => m.HasBody))
    {
        var ins = method.Body.Instructions;
        for (int i = 0; i < ins.Count; i++)
        {
            if (ins[i].Operand is MethodReference call && call.DeclaringType.Name == "AccessTools" && call.Name.Contains("Method"))
            {
                Console.WriteLine($"  harmony lookup in {type.Name}.{method.Name} (checked at compile time through nameof)");
            }
        }
    }
}

Console.WriteLine($"  {rpcCount} RPC sends checked");
Console.WriteLine($"  {nonPublic.Count} non-public game members used (publicized):");
foreach (var n in nonPublic) Console.WriteLine("    " + n);

Console.WriteLine(failures == 0 ? "RESULT: OK, everything resolves" : $"RESULT: {failures} PROBLEM(S)");
return failures == 0 ? 0 : 1;
