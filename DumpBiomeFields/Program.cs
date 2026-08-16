using System;
using System.Linq;
using Microsoft.Diagnostics.Runtime;

var processes = System.Diagnostics.Process.GetProcessesByName("Terraria");
if (processes.Length == 0) { Console.WriteLine("Terraria not running"); return; }

using var dataTarget = DataTarget.AttachToProcess(processes[0].Id, suspend: false);
using var runtime = dataTarget.ClrVersions.FirstOrDefault()?.CreateRuntime();
var appDomain = runtime.AppDomains.First();

var mainType = runtime.EnumerateModules()
  .SelectMany(m => m.EnumerateTypeDefToMethodTableMap())
  .Select(map => runtime.GetTypeByMethodTable(map.MethodTable))
  .FirstOrDefault(t => t?.Name == "Terraria.Main");

int myPlayer = mainType.StaticFields.FirstOrDefault(f => f.Name == "myPlayer")?.Read<int>(appDomain) ?? -1;
ulong playersAddr = mainType.StaticFields.FirstOrDefault(f => f.Name == "player")?.Read<ulong>(appDomain) ?? 0;
var playerArrayObj = runtime.Heap.GetObject(playersAddr);
ulong playerAddr = playerArrayObj.AsArray().GetObjectValue(myPlayer).Address;
var playerObj = runtime.Heap.GetObject(playerAddr);

Console.WriteLine($"statLife: {playerObj.ReadField<int>("statLife")} / {playerObj.ReadField<int>("statLifeMax2")}");
Console.WriteLine($"statMana: {playerObj.ReadField<int>("statMana")} / {playerObj.ReadField<int>("statManaMax2")}");
try {
    Console.WriteLine($"statDefense (int): {playerObj.ReadField<int>("statDefense")}");
} catch {
    Console.WriteLine($"statDefense might not be int");
}
