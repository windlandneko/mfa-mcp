// Statically wires AlienBridge.dll into the game's Assembly-CSharp.dll (see Hooks in Bridge.cs).
// usage: Patcher <path/to/MessageFromAliens_Data/Managed> <path/to/AlienBridge.dll>
// Idempotent: the pristine assembly is kept as Assembly-CSharp.dll.orig and always used as input.
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

if (args.Length < 2) { Console.Error.WriteLine("usage: Patcher <ManagedDir> <AlienBridge.dll>"); return 2; }
string managed = args[0], bridgeSrc = args[1];
string target = Path.Combine(managed, "Assembly-CSharp.dll"), orig = target + ".orig";
if (!File.Exists(orig)) File.Copy(target, orig);
File.Copy(bridgeSrc, Path.Combine(managed, "AlienBridge.dll"), true);

var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(managed);
var asm = AssemblyDefinition.ReadAssembly(orig, new ReaderParameters { AssemblyResolver = resolver, ReadWrite = false, InMemory = true });
var bridge = AssemblyDefinition.ReadAssembly(Path.Combine(managed, "AlienBridge.dll"), new ReaderParameters { AssemblyResolver = resolver });
var mod = asm.MainModule;
var hooksT = bridge.MainModule.GetType("AlienBridge.Hooks") ?? throw new Exception("AlienBridge.Hooks not found");
MethodReference H(string name) => mod.ImportReference(hooksT.Methods.Single(m => m.Name == name));
TypeDefinition T(string n) => mod.GetType(n) ?? throw new Exception("type not found: " + n);
MethodDefinition M(TypeDefinition t, string n) => t.Methods.Single(m => m.Name == n && m.HasBody);

int n = 0;
void Redirect(MethodDefinition m, string declType, string name, MethodReference to) {
  foreach (var i in m.Body.Instructions)
    if ((i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt) && i.Operand is MethodReference r
        && r.DeclaringType.FullName == declType && r.Name == name) {
      i.OpCode = OpCodes.Call;
      i.Operand = to;
      n++;
    }
}

var draw = T("Draw");
var global = T("Global");

// Run the game offline: skip its Steam initialization, then continue loading game data.
{
  var init = M(global, "Init");
  var il = init.Body.GetILProcessor();
  var data = init.Body.Instructions.Single(i => i.OpCode == OpCodes.Stsfld
    && i.Operand is FieldReference field && field.Name == "textfiles").Previous;
  if (data.OpCode != OpCodes.Newobj)
    throw new Exception("Unexpected Global.Init data initializer");
  var first = init.Body.Instructions[0];
  il.InsertBefore(first, il.Create(OpCodes.Ldc_I4_0));
  il.InsertBefore(first, il.Create(OpCodes.Stsfld, global.Fields.Single(f => f.Name == "steam")));
  il.InsertBefore(first, il.Create(OpCodes.Br, data));
}

{
  var method = M(global, "SetAchievement");
  var il = method.Body.GetILProcessor();
  var first = method.Body.Instructions[0];
  il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
  il.InsertBefore(first, il.Create(OpCodes.Call, H("Achievement")));
  n++;
}

// Draw.Awake: call Start(this) before every ret
{
  var m = M(draw, "Awake");
  var il = m.Body.GetILProcessor();
  foreach (var ret in m.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToList()) {
    il.InsertBefore(ret, il.Create(OpCodes.Ldarg_0));
    il.InsertBefore(ret, il.Create(OpCodes.Call, H("Start")));
    n++;
  }
}

// Draw.Update: pre/post hooks + input redirection
{
  var m = M(draw, "Update");
  m.Body.SimplifyMacros();
  Redirect(m, "UnityEngine.Input", "get_mousePosition", H("MousePosition"));
  Redirect(m, "UnityEngine.Input", "GetMouseButtonDown", H("GetMouseButtonDown"));
  var il = m.Body.GetILProcessor();
  foreach (var ret in m.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToList()) {
    // keep branch targets pointing at the ret valid: turn ret into the hook call and append a new ret
    var newRet = il.Create(OpCodes.Ret);
    il.InsertAfter(ret, newRet);
    il.InsertAfter(ret, il.Create(OpCodes.Call, H("Post")));
    ret.OpCode = OpCodes.Ldarg_0;
    ret.Operand = null;
    n++;
  }
  var first = m.Body.Instructions[0];
  il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
  il.InsertBefore(first, il.Create(OpCodes.Call, H("Pre")));
  m.Body.OptimizeMacros();
}

// New glyphs exposed through lookup may not yet have an annotation entry.
{
  var method = M(T("AlienCharacter"), "UpdateEarthText");
  var il = method.Body.GetILProcessor();
  var first = method.Body.Instructions[0];
  il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
  il.InsertBefore(first, il.Create(OpCodes.Call, H("EnsureAnnotation")));
  n++;
}

// save directory override
foreach (var name in new[] { "SaveGame", "SaveSetting", "LoadGame" })
  Redirect(M(global, name), "UnityEngine.Application", "get_persistentDataPath", H("SaveDir"));

foreach (var m in mod.GetTypes().SelectMany(type => type.Methods).Where(m => m.HasBody)) {
  Redirect(m, "UnityEngine.Application", "Quit", H("Quit"));
  Redirect(m, "UnityEngine.Application", "OpenURL", H("OpenUrl"));
  Redirect(m, "Steamworks.SteamClient", "get_SteamId", H("SteamId"));
}
asm.Write(target);
Console.WriteLine($"patched {target} ({n} edits)");
return 0;
