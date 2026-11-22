#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace StellarTool.Mod {
internal static class GameCompatibility {
    const BindingFlags Visibility = BindingFlags.Public | BindingFlags.NonPublic;

    internal static MethodInfo? ProcessCallback(Type type, string name) {
        var method = type.GetMethod(name, Visibility | BindingFlags.Instance | BindingFlags.DeclaredOnly,
            null, Type.EmptyTypes, null);
        return method?.ReturnType == typeof(void) ? method : null;
    }

    internal static List<string> MissingMembers(Func<string, Type?> findType) {
        var missing = new List<string>();
        var notes = findType("Manager.NotesManager");
        var reader = findType("Manager.NotesReader");
        if (notes == null || reader == null) {
            if (notes == null) missing.Add("Manager.NotesManager");
            if (reader == null) missing.Add("Manager.NotesReader");
            return missing;
        }

        var statics = Visibility | BindingFlags.Static;
        var instances = Visibility | BindingFlags.Instance;
        if (notes.GetMethod("GetCurrentMsec", statics, null, Type.EmptyTypes, null)?.ReturnType != typeof(float))
            missing.Add("NotesManager.GetCurrentMsec()");
        if (notes.GetMethod("Instance", statics, null, new[] { typeof(int) }, null)?.ReturnType != notes)
            missing.Add("NotesManager.Instance(int)");
        if (notes.GetMethod("IsPlaying", instances, null, Type.EmptyTypes, null)?.ReturnType != typeof(bool))
            missing.Add("NotesManager.IsPlaying()");
        if (notes.GetMethod("getReader", instances, null, Type.EmptyTypes, null)?.ReturnType != reader)
            missing.Add("NotesManager.getReader()");
        var noteList = reader.GetMethod("GetNoteList", instances, null, Type.EmptyTypes, null);
        if (noteList == null || !typeof(ICollection).IsAssignableFrom(noteList.ReturnType))
            missing.Add("NotesReader.GetNoteList()");

        foreach (var name in new[] { "AdvertiseProcess", "MusicSelectProcess", "GameProcess", "ResultProcess" }) {
            var process = findType("Process." + name);
            if (process == null) {
                missing.Add("Process." + name);
                continue;
            }
            foreach (var callback in new[] { "OnStart", "OnRelease" }) {
                if (ProcessCallback(process, callback) == null) missing.Add(name + "." + callback + "()");
            }
        }
        return missing;
    }
}
}
