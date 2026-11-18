using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualBasic.FileIO;

namespace EmuClean.Engine
{
    public class RuleRow
    {
        public string Category, Label, Note;
        public List<string> Paths = new List<string>();
        public long Size;
        public int Files;
        public bool Auto { get { return Category == "junk"; } }
        public bool Locked { get { return Category == "lock"; } }
    }

    public class CleanItem
    {
        public string Emu, Label, Path;
    }

    public class CleanResult
    {
        public int Deleted, Failed;
        public long Freed;
        public List<string> Errors = new List<string>();
    }

    public static class Cleaner
    {
        // ---------------------------------------------------------- scanning
        private static Tuple<long, int> SizeOf(string path)
        {
            long total = 0;
            int count = 0;
            try
            {
                if (File.Exists(path))
                {
                    return Tuple.Create(new FileInfo(path).Length, 1);
                }
                if (!Directory.Exists(path)) return Tuple.Create(0L, 0);
                var stack = new Stack<string>();
                stack.Push(path);
                int guard = 0;
                while (stack.Count > 0 && guard < 20000)
                {
                    guard++;
                    var dir = stack.Pop();
                    try
                    {
                        foreach (var f in Directory.GetFiles(dir))
                        {
                            try { total += new FileInfo(f).Length; count++; } catch { }
                        }
                        foreach (var d in Directory.GetDirectories(dir)) stack.Push(d);
                    }
                    catch { }
                }
            }
            catch { }
            return Tuple.Create(total, count);
        }

        private static string Resolve(string tpl, string root, string data)
        {
            var p = tpl.Replace("%DATA%", data ?? "").Replace("%ROOT%", root ?? "")
                       .Replace('/', Path.DirectorySeparatorChar);
            return Detector.ExpandPath(p);
        }

        private static List<string> Matches(string pattern)
        {
            var hits = new List<string>();
            try
            {
                var name = Path.GetFileName(pattern);
                if (name.Contains("*"))
                {
                    var dir = Path.GetDirectoryName(pattern);
                    if (Directory.Exists(dir))
                        hits.AddRange(Directory.GetFiles(dir, name));
                }
                else if (File.Exists(pattern)) hits.Add(pattern);
                else if (Directory.Exists(pattern)) hits.Add(pattern);
            }
            catch { }
            return hits;
        }

        public static List<RuleRow> ScanEmulator(DetectedEmu emu)
        {
            var rows = new List<RuleRow>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in Detector.RulesFor(emu.Id))
            {
                var row = new RuleRow { Category = r.Category, Label = r.Label, Note = r.Note };
                foreach (var tpl in r.Templates)
                {
                    foreach (var hit in Matches(Resolve(tpl, emu.InstallRoot, emu.DataRoot)))
                    {
                        var key = hit.ToLowerInvariant();
                        if (seen.Contains(key)) continue;
                        seen.Add(key);
                        row.Paths.Add(hit);
                    }
                }
                if (row.Paths.Count == 0) continue;
                foreach (var p in row.Paths)
                {
                    var s = SizeOf(p);
                    row.Size += s.Item1;
                    row.Files += s.Item2;
                }
                rows.Add(row);
            }
            return rows;
        }

        public static string Human(long n)
        {
            double d = n;
            if (n < 1024) return n + " B";
            if ((d /= 1024) < 1024) return d.ToString("F1") + " KB";
            if ((d /= 1024) < 1024) return d.ToString("F1") + " MB";
            d /= 1024;
            return d.ToString("F2") + " GB";
        }

        // ---------------------------------------------------------- delete
        public static CleanResult Execute(List<CleanItem> plan, bool permanent, Action<string> log)
        {
            var res = new CleanResult();
            if (plan == null || plan.Count == 0)
            {
                res.Errors.Add("nothing selected");
                return res;
            }
            foreach (var item in plan)
            {
                long sz = 0;
                try { sz = SizeOf(item.Path).Item1; } catch { }
                res.Freed += sz;
                log("queued: " + item.Path + " (" + Human(sz) + ")");
                try
                {
                    if (File.Exists(item.Path))
                    {
                        if (permanent) File.Delete(item.Path);
                        else FileSystem.DeleteFile(item.Path, UIOption.OnlyErrorDialogs,
                             RecycleOption.SendToRecycleBin, UICancelOption.DoNothing);
                    }
                    else if (Directory.Exists(item.Path))
                    {
                        if (permanent) Directory.Delete(item.Path, true);
                        else FileSystem.DeleteDirectory(item.Path, UIOption.OnlyErrorDialogs,
                             RecycleOption.SendToRecycleBin, UICancelOption.DoNothing);
                    }
                    res.Deleted++;
                }
                catch (Exception ex)
                {
                    res.Failed++;
                    res.Errors.Add(item.Path + ": " + ex.Message);
                }
            }
            log(string.Format("done: {0} targets deleted, {1} failed, ~{2} freed",
                res.Deleted, res.Failed, Human(res.Freed)));
            return res;
        }

        // ---------------------------------------------------------- leftovers
        public static List<Dictionary<string, object>> FindLeftovers()
        {
            var outp = new List<Dictionary<string, object>>();
            foreach (var emu in Detector.Detect(false))
            {
                if (emu.Installed || emu.Running) continue;
                var paths = new List<string>();
                if (Directory.Exists(emu.DataRoot)) paths.Add(emu.DataRoot);
                if (paths.Count == 0) continue;
                long size = 0;
                foreach (var p in paths) size += SizeOf(p).Item1;
                outp.Add(new Dictionary<string, object>
                {
                    ["id"] = emu.Id,
                    ["name"] = emu.Name,
                    ["paths"] = paths,
                    ["size"] = size,
                });
            }
            return outp;
        }
    }
}
