using System.Net.WebSockets;
using System.Text.Json;

namespace StellarTool;

public static class InputWebSocket {
 private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
 public static async Task Handle(HttpContext ctx,InputSnapshotStore store,CancellationToken stopping) {
  if(!ctx.WebSockets.IsWebSocketRequest){ctx.Response.StatusCode=426;return;}
  // No cross-origin browser access, and browsers have the same read-only data as GET /api/input.
  var origin=ctx.Request.Headers.Origin.ToString();
  if(origin.Length!=0&&!string.Equals(origin,$"{ctx.Request.Scheme}://{ctx.Request.Host}",StringComparison.OrdinalIgnoreCase)){ctx.Response.StatusCode=403;return;}
  var channel=store.Subscribe();
  if(channel==null){ctx.Response.StatusCode=503;return;}
  try{
   using var socket=await ctx.WebSockets.AcceptWebSocketAsync();
   using var session=CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted,stopping);
   var receive=Watch(socket,session);
   try{
    InputPacket? previous=null;
    var ready=channel.Reader.WaitToReadAsync(session.Token).AsTask();
    while(!session.IsCancellationRequested){
     using var timer=CancellationTokenSource.CreateLinkedTokenSource(session.Token);
     var heartbeat=Task.Delay(50,timer.Token);
     if(await Task.WhenAny(ready,heartbeat)==ready){
      timer.Cancel();
      if(!await ready)break;
      while(channel.Reader.TryRead(out var packet)){
       previous=packet;
       await Send(socket,store.Push(packet),session.Token);
      }
      ready=channel.Reader.WaitToReadAsync(session.Token).AsTask();
     }else await Send(socket,store.Push(previous,heartbeat:true),session.Token);
    }
   }catch(Exception e)when(e is OperationCanceledException or WebSocketException or IOException){}
   finally{session.Cancel();await receive;}
  }finally{store.Unsubscribe(channel);}
 }
 private static async Task Watch(WebSocket socket,CancellationTokenSource session){
  try{await socket.ReceiveAsync(new byte[1].AsMemory(),session.Token);} // First application message/close ends this read-only connection.
  catch(Exception e)when(e is OperationCanceledException or WebSocketException or IOException){}
  finally{session.Cancel();}
 }
 private static async Task Send(WebSocket socket,InputPush push,CancellationToken ct){
  using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(250);
  await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(push,Json).AsMemory(),WebSocketMessageType.Text,true,timeout.Token);
 }
}
