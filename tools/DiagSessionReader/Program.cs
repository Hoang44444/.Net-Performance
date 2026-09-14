// Đọc file .diagsession do Visual Studio Diagnostics Hub (VSDiagnostics.exe) ghi ra.
//
//   DiagSessionReader cpu   <file.diagsession> [--proc Project] [--top 25] [--ham TênHàm]
//     --ham: chỉ tính các mẫu có hàm này trong call stack (vùng đo), % tính trên số mẫu đó.
//   DiagSessionReader alloc <file.diagsession> [--top 20]
//
// .diagsession là gói nén; bên trong có file ETL (sc.user_aux.etl).
//   · CPU Usage agent       → mẫu PerfInfo/SampledProfile + call stack + sự kiện CLR để giải tên hàm JIT.
//   · .NET Object Allocation → provider 8bc9e67b-ca34-4b9a-9442-8f75403f357b, EventID 1 = một object:
//       [0..8) số thứ tự, [8..12) byte, [12..16) loại (0x1D mảng, 0x33 generic),
//       [16..24) ModuleID, [24..28) TypeDef token (mảng: token phần tử),
//       generic: [40..42) số đối số, [42..50) ModuleID + [50..54) token của đối số thứ nhất.
//     Tên kiểu giải bằng System.Reflection.Metadata trên bản metadata VS chép sẵn trong gói
//     (MetadataCache/*.metadata, map ModuleID → file ở MetadataLookupMap.txt) — không phụ thuộc bản build hiện tại.
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Microsoft.Diagnostics.Symbols;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;

if (args.Length < 2)
{
    Console.WriteLine("DiagSessionReader cpu|alloc <file.diagsession> [--proc Project] [--top N]");
    return 1;
}

string mode = args[0];
string etl = TimEtl(args[1]);
string procName = GiaTri("--proc") ?? "Project";
int top = int.Parse(GiaTri("--top") ?? (mode == "cpu" ? "25" : "20"));

return mode switch
{
    "cpu" => DocCpu(etl, procName, top, GiaTri("--ham")),
    "alloc" => DocAlloc(etl, top),
    _ => 1,
};

string? GiaTri(string ten)
{
    int i = Array.IndexOf(args, ten);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static string TimEtl(string duongDan)
{
    if (duongDan.EndsWith(".etl", StringComparison.OrdinalIgnoreCase))
        return duongDan;

    string thuMuc = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(duongDan))!, Path.GetFileNameWithoutExtension(duongDan));
    if (!Directory.Exists(thuMuc))
    {
        string vsdiag = TimVsDiagnostics();
        using var p = Process.Start(new ProcessStartInfo(vsdiag, $"expandDiagSession \"{Path.GetFullPath(duongDan)}\"") { RedirectStandardOutput = true, UseShellExecute = false })!;
        p.StandardOutput.ReadToEnd();
        p.WaitForExit();
    }
    return Directory.EnumerateFiles(thuMuc, "*.etl", SearchOption.AllDirectories).OrderByDescending(f => new FileInfo(f).Length).First();
}

static string TimVsDiagnostics()
{
    string vswhere = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio", "Installer", "vswhere.exe");
    using var p = Process.Start(new ProcessStartInfo(vswhere, "-latest -prerelease -property installationPath") { RedirectStandardOutput = true, UseShellExecute = false })!;
    string vs = p.StandardOutput.ReadToEnd().Trim();
    return Path.Combine(vs, "Team Tools", "DiagnosticsHub", "Collector", "VSDiagnostics.exe");
}

// ───────────────────────────────────────────────────────────────────────────
// CPU
// ───────────────────────────────────────────────────────────────────────────
static int DocCpu(string etl, string procName, int top, string? locHam)
{
    using var log = TraceLog.OpenOrConvert(etl, new TraceLogOptions { ConversionLog = TextWriter.Null });
    var proc = log.Processes.Where(p => p.Name.Equals(procName, StringComparison.OrdinalIgnoreCase))
                            .OrderByDescending(p => p.CPUMSec).FirstOrDefault();
    if (proc is null) { Console.WriteLine($"Không thấy process '{procName}'."); return 2; }

    // Giải symbol cho phần native của runtime (GC, cấp phát, box) và CoreLib biên dịch sẵn.
    string cache = Path.Combine(Path.GetTempPath(), "SymbolCache");
    using var sym = new SymbolReader(TextWriter.Null, $"SRV*{cache}*https://msdl.microsoft.com/download/symbols") { SecurityCheck = _ => true };
    foreach (var m in proc.LoadedModules)
    {
        string ten = Path.GetFileName(m.FilePath).ToLowerInvariant();
        if (ten is "coreclr.dll" or "system.private.corelib.dll" or "clrjit.dll")
            log.CodeAddresses.LookupSymbolsForModule(sym, m.ModuleFile);
    }

    var incl = new Dictionary<string, int>();
    var excl = new Dictionary<string, int>();
    var nhom = new Dictionary<string, int>();
    var seen = new HashSet<string>();
    int total = 0;

    int tongProcess = 0;
    var frames = new List<(string Module, string Ham)>();
    foreach (var ev in proc.EventsInProcess)
    {
        if (ev is not SampledProfileTraceData) continue;
        tongProcess++;

        frames.Clear();
        for (var st = ev.CallStack(); st is not null; st = st.Caller)
        {
            var ca = st.CodeAddress;
            frames.Add((ca.ModuleName, string.IsNullOrEmpty(ca.FullMethodName) ? "?" : ca.FullMethodName));
        }
        if (locHam is not null && !frames.Any(fr => fr.Ham.Contains(locHam)))
            continue;

        total++;
        seen.Clear();
        for (int i = 0; i < frames.Count; i++)
        {
            string ten = $"{frames[i].Module}!{frames[i].Ham}";
            if (i == 0) excl[ten] = excl.GetValueOrDefault(ten) + 1;
            if (seen.Add(ten)) incl[ten] = incl.GetValueOrDefault(ten) + 1;
        }
        string k = frames.Select(fr => PhanNhom(fr.Module, fr.Ham)).FirstOrDefault(x => x is not null)
                   ?? (frames.Count > 0 && LaKernel(frames[0].Module) ? "Kernel / I/O / page fault" : null)
                   ?? (frames.Count > 0 && frames[0].Module.Equals("project", StringComparison.OrdinalIgnoreCase) && frames[0].Ham != "?" ? "Code của ứng dụng (self)" : null)
                   ?? "Khác (runtime/thư viện)";
        nhom[k] = nhom.GetValueOrDefault(k) + 1;
    }

    Console.WriteLine($"Process {proc.Name} (pid {proc.ProcessID}) · CPU {proc.CPUMSec:N0} ms · {tongProcess:N0} mẫu" +
                      (locHam is null ? "" : $" · {total:N0} mẫu nằm trong '{locHam}'"));
    if (total == 0) return 3;
    Console.WriteLine();
    Console.WriteLine("Nhóm chi phí (mỗi mẫu xếp vào nhóm của frame gần đỉnh stack nhất khớp một nhóm):");
    foreach (var kv in nhom.OrderByDescending(k => k.Value))
        Console.WriteLine($"  {kv.Value,7:N0} {kv.Value * 100.0 / total,6:F1}%  {kv.Key}");
    Console.WriteLine();
    Console.WriteLine($"Hàm của ứng dụng — Total CPU (inclusive), top {top}:");
    foreach (var kv in incl.Where(k => k.Key.StartsWith("project!", StringComparison.OrdinalIgnoreCase) && !k.Key.EndsWith("!?")).OrderByDescending(k => k.Value).Take(top))
        Console.WriteLine($"  {kv.Value,7:N0} {kv.Value * 100.0 / total,6:F1}%  {RutGon(kv.Key)}");
    Console.WriteLine();
    Console.WriteLine($"Hàm tốn nhiều nhất — Self CPU (exclusive), top {top}:");
    foreach (var kv in excl.OrderByDescending(k => k.Value).Take(top))
        Console.WriteLine($"  {kv.Value,7:N0} {kv.Value * 100.0 / total,6:F1}%  {RutGon(kv.Key)}");
    return 0;
}

static string? PhanNhom(string module, string ham)
{
    string m = module.ToLowerInvariant();
    if (m == "coreclr" && (ham.Contains("gc_heap") || ham.Contains("GCHeap") || ham.Contains("WKS::") || ham.Contains("SVR::")))
        return "GC (dọn rác, coreclr gc_heap)";
    if (ham.Contains("CastHelpers.Box") || ham.Contains("CastHelpers::Box") || ham.Contains("JIT_Box"))
        return "Boxing (CastHelpers.Box)";
    if (m == "coreclr" && (ham.Contains("Alloc") || ham.Contains("JIT_New") || ham.StartsWith("RhpNew") || ham.StartsWith("memset")))
        return "Cấp phát object (RhpNewFast, memset, InternalAlloc)";
    if (ham.Contains("System.Reflection") || ham.Contains("RuntimeFieldHandle") || ham.Contains("FieldAccessor"))
        return "Reflection";
    if (ham.Contains("ValueType.Equals") || ham.Contains("ValueType::Equals") || ham.Contains("CanCompareBits") || ham.Contains("GetNumInstanceFieldBytes"))
        return "ValueType.Equals(object) mặc định";
    if (ham.Contains("System.Collections.Hashtable") || ham.Contains("System.Collections.ArrayList"))
        return "Hashtable / ArrayList (kho chứa object)";
    if (m == "coreclr" && (ham.StartsWith("JIT_CountProfile") || ham.StartsWith("JIT_ClassProfile")))
        return "Đếm PGO của tiered JIT (bản tier1-instr)";
    if (m == "clrjit")
        return "JIT biên dịch";
    return null;
}

static bool LaKernel(string module)
{
    string m = module.ToLowerInvariant();
    return m is "ntoskrnl" or "ntdll" or "kernelbase" or "kernel32" || m.EndsWith(".sys");
}

static string RutGon(string s) => s
    .Replace("Project.ValueTypeVsReferenceType.Modules.", "")
    .Replace("Project.ValueTypeVsReferenceType.Models.", "")
    .Replace("value class ", "")
    .Replace("class ", "")
    .Replace("System.Private.CoreLib!", "corelib!")
    .Replace("system.private.corelib!", "corelib!");

// ───────────────────────────────────────────────────────────────────────────
// Allocation
// ───────────────────────────────────────────────────────────────────────────
static int DocAlloc(string etl, int top)
{
    var modules = new Dictionary<ulong, string>();
    var agg = new Dictionary<(ulong Mod, int Tok, int Kind, ulong ArgMod, int ArgTok), (long Count, long Bytes)>();

    using (var src = new ETWTraceEventSource(etl))
    {
        src.AllEvents += e =>
        {
            string pg = e.ProviderGuid.ToString();
            // CLR ModuleLoad (152) / rundown DCStart (153) / DCStop (154): ModuleID(8) AssemblyID(8) Flags(4) Reserved(4) ILPath(UTF-16)
            if ((pg.StartsWith("e13c0d23") || pg.StartsWith("a669021c")) && (int)e.ID is 152 or 153 or 154)
            {
                var p = e.EventData();
                if (p.Length > 26)
                {
                    int end = 24;
                    while (end + 1 < p.Length && (p[end] != 0 || p[end + 1] != 0)) end += 2;
                    modules[BitConverter.ToUInt64(p, 0)] = System.Text.Encoding.Unicode.GetString(p, 24, end - 24);
                }
                return;
            }
            if (!pg.StartsWith("8bc9e67b") || (int)e.ID != 1) return;

            var d = e.EventData();
            int size = BitConverter.ToInt32(d, 8);
            int kind = BitConverter.ToInt32(d, 12);
            ulong mod = BitConverter.ToUInt64(d, 16);
            int tok = BitConverter.ToInt32(d, 24);
            ulong argMod = 0; int argTok = 0;
            if (kind == 0x33 && d.Length >= 54) { argMod = BitConverter.ToUInt64(d, 42); argTok = BitConverter.ToInt32(d, 50); }
            var key = (mod, tok, kind, argMod, argTok);
            var cur = agg.GetValueOrDefault(key);
            agg[key] = (cur.Count + 1, cur.Bytes + size);
        };
        src.Process();
    }

    // MetadataLookupMap.txt (UTF-16): "pid;ModuleID;tên file .metadata" — ảnh metadata thô (BSJB) lúc chạy.
    var cacheMetadata = new Dictionary<ulong, string>();
    string goc = Directory.GetParent(Path.GetDirectoryName(etl)!)!.FullName;
    foreach (var map in Directory.EnumerateFiles(goc, "MetadataLookupMap.txt", SearchOption.AllDirectories))
        foreach (var dong in File.ReadAllLines(map, System.Text.Encoding.Unicode))
        {
            var phan = dong.Trim().Split(';');
            if (phan.Length == 3 && ulong.TryParse(phan[1], out ulong id))
                cacheMetadata[id] = Path.Combine(Path.GetDirectoryName(map)!, phan[2]);
        }

    var readers = new Dictionary<ulong, MetadataReader?>();
    MetadataReader? Reader(ulong mod)
    {
        if (readers.TryGetValue(mod, out var r)) return r;
        r = null;
        if (cacheMetadata.TryGetValue(mod, out var anh) && File.Exists(anh))
            r = MetadataReaderProvider.FromMetadataImage(System.Collections.Immutable.ImmutableArray.Create(File.ReadAllBytes(anh))).GetMetadataReader();
        else if (modules.TryGetValue(mod, out var path) && File.Exists(path))
        {
            var pe = new PEReader(File.OpenRead(path));
            if (pe.HasMetadata) r = pe.GetMetadataReader();
        }
        return readers[mod] = r;
    }
    string Ten(ulong mod, int tok)
    {
        var r = Reader(mod);
        if (r is null || (tok >> 24) != 0x02) return $"{Path.GetFileNameWithoutExtension(modules.GetValueOrDefault(mod) ?? "?")}!0x{tok:X8}";
        var td = r.GetTypeDefinition(MetadataTokens.TypeDefinitionHandle(tok & 0xFFFFFF));
        string n = r.GetString(td.Name);
        if (td.GetDeclaringType() is { IsNil: false } dt)
            return Ten(mod, MetadataTokens.GetToken(dt)) + "+" + n;
        string ns = r.GetString(td.Namespace);
        return (string.IsNullOrEmpty(ns) ? n : ns + "." + n)
            .Replace("Project.ValueTypeVsReferenceType.Modules.", "")
            .Replace("Project.ValueTypeVsReferenceType.Models.", "")
            .Replace("System.Collections.Generic.", "");
    }

    long tongObj = agg.Values.Sum(v => v.Count), tongByte = agg.Values.Sum(v => v.Bytes);
    Console.WriteLine($"Tổng: {tongObj:N0} object · {tongByte:N0} byte");
    Console.WriteLine();
    Console.WriteLine($"  {"object",12} {"byte",15} {"% byte",7}  kiểu");
    foreach (var kv in agg.OrderByDescending(k => k.Value.Bytes).Take(top))
    {
        string ten = Ten(kv.Key.Mod, kv.Key.Tok);
        if (kv.Key.Kind == 0x33)
        {
            int ngoac = ten.IndexOf('`');
            ten = (ngoac > 0 ? ten[..ngoac] : ten) + "<" + Ten(kv.Key.ArgMod, kv.Key.ArgTok) + (ngoac > 0 && !ten[(ngoac + 1)..].StartsWith('1') ? ",…" : "") + ">";
        }
        if (kv.Key.Kind == 0x1D) ten += "[]";
        Console.WriteLine($"  {kv.Value.Count,12:N0} {kv.Value.Bytes,15:N0} {kv.Value.Bytes * 100.0 / tongByte,6:F1}%  {ten}");
    }
    return 0;
}
