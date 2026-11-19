using System;
using System.Reflection;
using System.Reflection.Emit;

namespace StellarTool.Mod {
public struct CalibratedNativeNote {
 public int Player,Index,JudgeType,Timing;
 public float OffsetMs;
 public bool Ex,IsBreak;
 public object NotesManager;
}

// Resolve/validate once. Returned delegates read native fields without reflection,
// object arrays, enum boxing or allocations in the judgement callbacks.
public static class CalibratedHitReader {
 const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 public static Func<object,CalibratedNativeNote> Build(Type noteType,bool head,Type breakType) {
  if(noteType==null||breakType==null)throw new ArgumentException("Native note types missing");
  var method=new DynamicMethod("StellarCalibratedNote",typeof(CalibratedNativeNote),new[]{typeof(object)},typeof(CalibratedHitReader),true);
  var il=method.GetILGenerator();var value=il.DeclareLocal(typeof(CalibratedNativeNote));
  LoadField(il,value,noteType,"MonitorIndex","Player",typeof(int));
  LoadField(il,value,noteType,"NoteIndex","Index",typeof(int));
  LoadField(il,value,noteType,"JudgeType","JudgeType",null);
  LoadField(il,value,noteType,head?"JudgeHeadResult":"JudgeResult","Timing",null);
  LoadField(il,value,noteType,"JudgeTimingDiffMsec","OffsetMs",typeof(float));
  LoadField(il,value,noteType,"IsExNote","Ex",typeof(bool));
  LoadField(il,value,noteType,"NoteMng","NotesManager",typeof(object));
  il.Emit(OpCodes.Ldloca,value);il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Isinst,breakType);
  il.Emit(OpCodes.Ldnull);il.Emit(OpCodes.Cgt_Un);il.Emit(OpCodes.Stfld,typeof(CalibratedNativeNote).GetField("IsBreak"));
  il.Emit(OpCodes.Ldloc,value);il.Emit(OpCodes.Ret);
  return (Func<object,CalibratedNativeNote>)method.CreateDelegate(typeof(Func<object,CalibratedNativeNote>));
 }
 static void LoadField(ILGenerator il,LocalBuilder value,Type noteType,string source,string destination,Type expected) {
  var field=Field(noteType,source);
  if(field==null||field.IsStatic)throw new InvalidOperationException("Native note field missing: "+source);
  if(expected==null) {if(!field.FieldType.IsEnum||Enum.GetUnderlyingType(field.FieldType)!=typeof(int))throw new InvalidOperationException("Unverified enum: "+source);}
  else if(expected==typeof(object)) {if(field.FieldType.IsValueType)throw new InvalidOperationException("Unverified manager reference");}
  else if(field.FieldType!=expected)throw new InvalidOperationException("Native field type changed: "+source);
  il.Emit(OpCodes.Ldloca,value);il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Castclass,noteType);il.Emit(OpCodes.Ldfld,field);
  if(expected==null)il.Emit(OpCodes.Conv_I4);
  il.Emit(OpCodes.Stfld,typeof(CalibratedNativeNote).GetField(destination));
 }
 public static FieldInfo Field(Type type,string name) {
  for(var t=type;t!=null;t=t.BaseType) {
   var field=t.GetField(name,All|BindingFlags.DeclaredOnly);if(field!=null)return field;
  }
  return null;
 }

 public static Func<int,object> CurrentNotes(Type notes) {
  var get=notes.GetMethod("Instance",All,null,new[]{typeof(int)},null);
  if(get==null||!get.IsStatic||get.ReturnType!=notes)throw new InvalidOperationException("Unverified NotesManager.Instance");
  var m=new DynamicMethod("StellarCurrentNotes",typeof(object),new[]{typeof(int)},typeof(CalibratedHitReader),true);
  var il=m.GetILGenerator();il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Call,get);il.Emit(OpCodes.Ret);
  return (Func<int,object>)m.CreateDelegate(typeof(Func<int,object>));
 }

 public static Func<int,bool> CurrentScoreEnabled(Type manager,Type singletonDefinition,Type score) {
  var singleton=singletonDefinition.MakeGenericType(manager);
  var instance=singleton.GetProperty("Instance",All).GetGetMethod(true);
  var get=manager.GetMethod("GetGameScore",All,null,new[]{typeof(int),typeof(int)},null);
  var enable=Field(score,"IsEnable");var skip=Field(score,"IsTrackSkip");
  if(instance==null||!instance.IsStatic||get==null||get.IsStatic||get.ReturnType!=score||
     enable==null||skip==null||enable.FieldType!=typeof(bool)||skip.FieldType!=typeof(bool)||enable.IsStatic||skip.IsStatic)
   throw new InvalidOperationException("Unverified live score guard");
  var m=new DynamicMethod("StellarLiveHitScore",typeof(bool),new[]{typeof(int)},typeof(CalibratedHitReader),true);
  var il=m.GetILGenerator();var gm=il.DeclareLocal(manager);var gs=il.DeclareLocal(score);var no=il.DefineLabel();
  il.Emit(OpCodes.Call,instance);il.Emit(OpCodes.Stloc,gm);il.Emit(OpCodes.Ldloc,gm);il.Emit(OpCodes.Brfalse,no);
  il.Emit(OpCodes.Ldloc,gm);il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Ldc_I4_M1);il.Emit(OpCodes.Callvirt,get);il.Emit(OpCodes.Stloc,gs);
  il.Emit(OpCodes.Ldloc,gs);il.Emit(OpCodes.Brfalse,no);il.Emit(OpCodes.Ldloc,gs);il.Emit(OpCodes.Ldfld,enable);il.Emit(OpCodes.Brfalse,no);
  il.Emit(OpCodes.Ldloc,gs);il.Emit(OpCodes.Ldfld,skip);il.Emit(OpCodes.Ldc_I4_0);il.Emit(OpCodes.Ceq);il.Emit(OpCodes.Ret);
  il.MarkLabel(no);il.Emit(OpCodes.Ldc_I4_0);il.Emit(OpCodes.Ret);
  return (Func<int,bool>)m.CreateDelegate(typeof(Func<int,bool>));
 }
}
}
