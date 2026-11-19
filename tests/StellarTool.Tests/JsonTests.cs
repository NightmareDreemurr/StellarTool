using System;
using System.Text.Json;
namespace StellarTool.Tests;
internal static class JsonTests {public static void Run(){var text=StellarTool.Mod.Json.Write(new System.Collections.Generic.Dictionary<string,object>{{"text","中文\n\"\\"},{"x",new[]{1,2}},{"none",null!}});using var doc=JsonDocument.Parse(text);if(doc.RootElement.GetProperty("text").GetString()!="中文\n\"\\")throw new Exception("JSON escape");if(doc.RootElement.GetProperty("x")[1].GetInt32()!=2)throw new Exception("JSON array");double exact=(double)17.234560012817383f;using var timing=JsonDocument.Parse(StellarTool.Mod.Json.Write(exact));if(timing.RootElement.GetDouble()!=exact)throw new Exception("JSON raw offset precision");Console.WriteLine("3 Mod JSON checks passed");}}
