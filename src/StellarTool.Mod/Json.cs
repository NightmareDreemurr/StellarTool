#nullable disable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
namespace StellarTool.Mod {
internal static class Json {
 public static string Write(object value){var b=new StringBuilder();Append(b,value);return b.ToString();}
 static void Quote(StringBuilder b,string s){b.Append('"');foreach(char c in s){switch(c){case '"':b.Append("\\\"");break;case '\\':b.Append("\\\\");break;case '\n':b.Append("\\n");break;case '\r':b.Append("\\r");break;case '\t':b.Append("\\t");break;default:if(c<32)b.Append("\\u"+((int)c).ToString("x4"));else b.Append(c);break;}}b.Append('"');}
 static void Append(StringBuilder b,object v){if(v==null){b.Append("null");return;}if(v is string){Quote(b,(string)v);return;}if(v is bool){b.Append((bool)v?"true":"false");return;}if(v is IConvertible&&v.GetType().IsPrimitive||v is decimal){var s=v is double d?d.ToString("R",CultureInfo.InvariantCulture):v is float f?f.ToString("R",CultureInfo.InvariantCulture):Convert.ToString(v,CultureInfo.InvariantCulture);if(s=="NaN"||s.Contains("Infinity"))throw new ArgumentException("non-finite number");b.Append(s);return;}if(v is IDictionary){b.Append('{');bool first=true;foreach(DictionaryEntry p in (IDictionary)v){if(!first)b.Append(',');first=false;Quote(b,Convert.ToString(p.Key));b.Append(':');Append(b,p.Value);}b.Append('}');return;}if(v is IEnumerable){b.Append('[');bool first=true;foreach(var p in (IEnumerable)v){if(!first)b.Append(',');first=false;Append(b,p);}b.Append(']');return;}var fields=v.GetType().GetProperties().ToDictionary(p=>p.Name,p=>p.GetValue(v,null));Append(b,fields);}
}
}
