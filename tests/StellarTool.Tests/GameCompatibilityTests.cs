using System.Collections;
using StellarTool.Mod;

namespace StellarTool.Tests;

internal static class GameCompatibilityTests {
    internal static void Run() {
        Type? FindType(string name) => name switch {
            "Manager.NotesManager" => typeof(Notes),
            "Manager.NotesReader" => typeof(Reader),
            "Process.AdvertiseProcess" or "Process.MusicSelectProcess"
                or "Process.GameProcess" or "Process.ResultProcess" => typeof(Process),
            _ => null
        };

        if (GameCompatibility.MissingMembers(FindType).Count != 0)
            throw new Exception("A compatible game contract should load without a known DLL hash");

        var missing = GameCompatibility.MissingMembers(name =>
            name == "Process.ResultProcess" ? typeof(IncompatibleProcess) : FindType(name));
        if (!missing.SequenceEqual(new[] { "ResultProcess.OnStart()" }))
            throw new Exception("A missing zero-argument callback should be reported before patching");

        missing = GameCompatibility.MissingMembers(name =>
            name == "Manager.NotesReader" ? null : FindType(name));
        if (!missing.SequenceEqual(new[] { "Manager.NotesReader" }))
            throw new Exception("A missing reader should be reported before starting the bridge");
        Console.WriteLine("3 game compatibility checks passed");
    }

    sealed class Notes {
        public static float GetCurrentMsec() => 0;
        public static Notes Instance(int player) => new();
        public bool IsPlaying() => false;
        public Reader getReader() => new();
    }

    sealed class Reader {
        public ICollection GetNoteList() => Array.Empty<object>();
    }

    sealed class Process {
        public void OnStart() {}
        public void OnStart(int argument) {}
        public void OnRelease() {}
    }

    sealed class IncompatibleProcess {
        public void OnStart(int argument) {}
        public void OnRelease() {}
    }
}
