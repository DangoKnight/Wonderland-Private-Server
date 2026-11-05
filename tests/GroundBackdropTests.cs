using System;
using System.IO;
using System.Linq;
using System.Text;
using Game.DataFiles;
using Game.Battle;

internal static class GroundBackdropTests
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); }

    // Independent native bytes: 20x20 grid, one sound zone, one object,
    // default 80, then lake rectangle (10,20)-(300,400).
    private static readonly byte[] GoldenPayload = {
        20,0,0,0,20,0,0,0,0,1,0,1,0,0,
        1,0,7,0,8,0,9,0,
        1,0,1,0,0,0,2,0,3,0,
        100,80,1,91,10,0,20,0,44,1,144,1
    };

    private static byte[] Archive(byte[] payload, string name)
    {
        using (var memory = new MemoryStream())
        using (var writer = new BinaryWriter(memory))
        {
            writer.Write(payload);
            byte[] entry = new byte[29];
            entry[0] = (byte)name.Length;
            Encoding.ASCII.GetBytes(name).CopyTo(entry, 1);
            BitConverter.GetBytes((uint)payload.Length).CopyTo(entry, 25);
            writer.Write(entry);
            writer.Write((ushort)1);
            return memory.ToArray();
        }
    }

    private static void Reject(byte[] raw, string message)
    {
        try { GroundBackdropCatalog.Load(new MemoryStream(raw)); }
        catch (InvalidDataException) { return; }
        catch (EndOfStreamException) { return; }
        throw new Exception("Accepted malformed archive: " + message);
    }

    private static void Resolve(GroundBackdropCatalog catalog, ushort map, int x, int y, byte expected)
    {
        byte actual;
        Check(catalog.TryResolve(map, x, y, out actual) && actual == expected,
            "Wrong backdrop for " + map + " at " + x + "," + y + ": " + actual);
    }

    public static int Main(string[] args)
    {
        try
        {
            var raw = Archive(GoldenPayload, "60001.map");
            var catalog = GroundBackdropCatalog.Load(new MemoryStream(raw));
            Check(catalog.Maps.Count == 1, "Golden map count");
            Check(catalog.Maps[60001].DefaultSelector == 80, "Golden default");
            var r = catalog.Maps[60001].Regions[0];
            Check(r.Selector == 91 && r.Left == 10 && r.Top == 20 && r.Right == 300 && r.Bottom == 400, "Golden region bytes");
            Resolve(catalog, 60001, 10, 20, 91);
            Resolve(catalog, 60001, 300, 400, 91);
            Resolve(catalog, 60001, 301, 400, 80);
            byte unknown;
            Check(!catalog.TryResolve(65535, 0, 0, out unknown), "Unknown map claimed");
            for (int i = 0; i < GoldenPayload.Length; i++)
                Reject(Archive(GoldenPayload.Take(i).ToArray(), "60001.map"), "truncated map " + i);
            Reject(Archive(GoldenPayload, "0.map"), "zero map ID");
            var bad = (byte[])GoldenPayload.Clone();
            bad[bad.Length - 8] = 255; bad[bad.Length - 7] = 255;
            Reject(Archive(bad, "60001.map"), "inverted rectangle");
            bad = (byte[])raw.Clone(); bad[GoldenPayload.Length + 21] = 1;
            Reject(bad, "bad payload offset");
            Reject(raw.Take(raw.Length - 1).ToArray(), "truncated index");
            var overlap = GoldenPayload.Concat(new byte[] {50,30,0,40,0,88,2,188,2}).ToArray();
            overlap[GoldenPayload.Length - 10] = 2;
            var ordered = GroundBackdropCatalog.Load(new MemoryStream(Archive(overlap, "60001.map")));
            Resolve(ordered, 60001, 100, 100, 91);
            Resolve(ordered, 60001, 500, 500, 50);
            Console.WriteLine("PASS: independent raw-byte goldens, boundaries, overlaps, malformed archives and missing maps.");
            if (args.Length > 0)
            {
                var native = GroundBackdropCatalog.Load(args[0]);
                Check(native.Maps.Count == 1147, "Native map count");
                Check(native.Maps.Values.Sum(m => m.Regions.Count) == 1398, "Native rectangle count");
                Resolve(native, 60001, 2202, 1395, 91);
                Resolve(native, 60001, 2000, 400, 90);
                Resolve(native, 60001, 5000, 3000, 50);
                Resolve(native, 60001, 2622, 755, 81);
                Resolve(native, 60000, 1694, 963, 50);
                Resolve(native, 12000, 1800, 700, 130);
                Resolve(native, 12002, 500, 455, 140);
                Check(BattleBackdropManager.Resolve(60001, 2202, 1395) == 1, "Missing catalog fallback");
                // Uses the real startup path resolver; no database/UI initialization.
                BattleBackdropManager.Initialize();
                Check(BattleBackdropManager.Resolve(60001, 2202, 1395) == 91, "Startup manager native lookup");
                Check(BattleBackdropManager.Resolve(65535, 0, 0) == 1, "Manager missing map fallback");
                string corruptPath = Path.Combine(Path.GetTempPath(), "wlo-ground-" + Guid.NewGuid().ToString("N") + ".mmg");
                BattleBackdropManager.Initialize(corruptPath);
                Check(BattleBackdropManager.Resolve(60001, 2202, 1395) == 91, "Missing archive replaced the good catalog");
                try
                {
                    File.WriteAllBytes(corruptPath, new byte[] {0});
                    BattleBackdropManager.Initialize(corruptPath);
                    Check(BattleBackdropManager.Resolve(60001, 2202, 1395) == 91, "Corrupt archive replaced the good catalog");
                }
                finally { if (File.Exists(corruptPath)) File.Delete(corruptPath); }
                Console.WriteLine("PASS: all 1147 native maps / 1398 rectangles, seven known locations and actual startup manager.");
            }
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
