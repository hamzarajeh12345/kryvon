// Lets the terminal drive the open editor: write a command into <project>/kryvon_cmd.txt,
// the editor runs it and writes the result to <project>/kryvon_cmd.out.
// Commands: refresh | showcase | capture <file> | Namespace.Type.Method (static, no args)
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kryvon.EditorTools
{
    [InitializeOnLoad]
    static class KryvonRemote
    {
        static double next;
        static string Dir => Directory.GetCurrentDirectory();

        static KryvonRemote() { EditorApplication.update += Tick; }

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + 1.0;
            var f = Path.Combine(Dir, "kryvon_cmd.txt");
            if (!File.Exists(f) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            var cmd = File.ReadAllText(f).Trim();
            File.Delete(f);
            var outp = Path.Combine(Dir, "kryvon_cmd.out");
            try
            {
                AssetDatabase.Refresh();
                if (EditorApplication.isCompiling) { File.WriteAllText(f, cmd); return; } // run after the recompile
                var parts = cmd.Split(new[] { ' ' }, 2);
                switch (parts[0])
                {
                    case "refresh": break;
                    case "showcase": KryvonShowcase.BuildAndCapture(); break;
                    case "capture": KryvonShowcase.Capture(Path.Combine(Dir, parts.Length > 1 ? parts[1] : "capture.png")); break;
                    default:
                        int dot = cmd.LastIndexOf('.');
                        Type t = null;
                        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) { t = asm.GetType(cmd.Substring(0, dot)); if (t != null) break; }
                        if (t == null) throw new Exception("unknown command " + cmd);
                        t.GetMethod(cmd.Substring(dot + 1)).Invoke(null, null);
                        break;
                }
                File.WriteAllText(outp, "ok " + cmd + "\n");
            }
            catch (Exception e) { File.WriteAllText(outp, "ERR " + cmd + "\n" + e + "\n"); Debug.LogException(e); }
        }
    }
}
