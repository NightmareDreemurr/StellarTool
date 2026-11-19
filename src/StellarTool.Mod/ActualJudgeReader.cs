using System;
using System.Reflection;
using System.Reflection.Emit;
namespace StellarTool.Mod {
public static class ActualJudgeReader {
 static readonly BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 public static Func<object,int,CommittedJudge> Build(Type scoreType){
  var at=scoreType.GetMethod("GetScoreAt",All,null,new[]{typeof(int)},null);
  if(at==null)throw new InvalidOperationException("Committed judge accessor missing");
  var source=at.ReturnType;var dynamicRead=new DynamicMethod("StellarReadCommittedJudge",typeof(CommittedJudge),new[]{typeof(object),typeof(int)},typeof(ActualJudgeReader),true);
  var il=dynamicRead.GetILGenerator();var raw=il.DeclareLocal(source);var result=il.DeclareLocal(typeof(CommittedJudge));
  il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Castclass,scoreType);il.Emit(OpCodes.Ldarg_1);il.Emit(OpCodes.Callvirt,at);il.Emit(OpCodes.Stloc,raw);
  foreach(var name in new[]{"Judged","Timing","Type"}){
   var field=source.GetField(name,All);if(field==null)throw new InvalidOperationException("Judge field missing: "+name);
   il.Emit(OpCodes.Ldloca,result);il.Emit(OpCodes.Ldloca,raw);il.Emit(OpCodes.Ldfld,field);
   if(name!="Judged")il.Emit(OpCodes.Conv_I4);
   il.Emit(OpCodes.Stfld,typeof(CommittedJudge).GetField(name));
  }
  il.Emit(OpCodes.Ldloca,result);il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Castclass,scoreType);
  // SDEZ exposes a bool field; a validated property fallback supports alternate builds.
  var skipField=scoreType.GetField("IsTrackSkip",All);
  if(skipField!=null){
   if(skipField.IsStatic||skipField.FieldType!=typeof(bool))throw new InvalidOperationException("IsTrackSkip must be an instance bool field");
   il.Emit(OpCodes.Ldfld,skipField);
  }else{
   var skipProperty=scoreType.GetProperty("IsTrackSkip",All);var getter=skipProperty?.GetGetMethod(true);
   if(getter==null||getter.IsStatic||skipProperty.PropertyType!=typeof(bool)||getter.GetParameters().Length!=0)
    throw new InvalidOperationException("IsTrackSkip instance bool accessor missing");
   il.Emit(OpCodes.Callvirt,getter);
  }
  il.Emit(OpCodes.Stfld,typeof(CommittedJudge).GetField("Skipped"));
  il.Emit(OpCodes.Ldloc,result);il.Emit(OpCodes.Ret);return (Func<object,int,CommittedJudge>)dynamicRead.CreateDelegate(typeof(Func<object,int,CommittedJudge>));
 }
}
}
