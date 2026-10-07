# Net-Client

A small, reusable client connection layer for .NET (net9.0): JSON line packets over a
replaceable stream transport, with connect, an application-defined handshake, heartbeat and
reconnect. It references nothing of any game; the application maps packet kinds and writes the
protocol on top of it.

## Wire format

One packet per line (UTF-8, ending in `\n`):

```json
{"type":"Heartbeat","senderId":"…","roomId":"","data":"","token":""}
```

`type` is a plain string; `data` is usually a JSON document of its own. See `Envelope`.

## Types

| Type | Role |
| --- | --- |
| `Envelope` | The packet: `Type`, `SenderId`, `RoomId`, `Data`, `Token`; `Create`, `Serialize`, `TryParse` (never throws). |
| `LineFraming` | Newline framing over a stream: `ReadLineAsync` (single reader), `WriteLine` (thread-safe). |
| `NetConnection` | The lifecycle: `ConnectAsync(handshake)`, `Disconnect`, `Reconnect`, `Send`, `IsConnected`, `ClientId`; events `EnvelopeReceived`, `Disconnected`. |
| `NetConnectionOptions` | `HeartbeatType` (null: no heartbeat), `HeartbeatInterval` (1 s), `HeartbeatTimeout` (8 s). |
| `ConnectionHandshake` | `delegate Task<bool> (NetConnection, CancellationToken abandoned)`: the application's opening exchange (version check, login…). |
| `INetLog` / `NetLogLevel` / `NullNetLog` | Diagnostics hook; the library has no logger of its own. |
| `Transport.IStreamFactory` | Opens the stream a connection talks over. |
| `Transport.TcpStreamFactory` | Plain TCP to a host and port. |

## Lifecycle

1. `ConnectAsync` opens a stream from the `IStreamFactory` and starts reading lines.
2. The handshake (if any) runs; it subscribes to `EnvelopeReceived`, sends, and completes with
   true or false. Its token is cancelled when the connection goes away before it finishes.
3. On success the heartbeat starts: a packet of `HeartbeatType` every interval; when none is
   received within the timeout, the connection is closed. Received heartbeats are not raised as
   `EnvelopeReceived`.
4. Any end (remote close, read or write error, heartbeat timeout, `Disconnect`) raises
   `Disconnected` once. `Reconnect` closes and connects again with the last handshake; work of an
   old connection never touches the new one.

## Transports

The transport is the replaceable part: anything that yields a connected `Stream` (TLS over TCP,
a peer accepted by a local host's listener for LAN play…) implements `IStreamFactory`; the
framing, handshake and heartbeat stay the same.

## Usage

```csharp
var connection = new NetConnection(
    new TcpStreamFactory("127.0.0.1", 8080),
    new NetConnectionOptions { HeartbeatType = "Heartbeat" },
    myLog);

connection.EnvelopeReceived += envelope => { /* map envelope.Type to your packet kinds */ };
bool connected = await connection.ConnectAsync(async (conn, abandoned) =>
{
    // send a login packet, await the reply (complete with false when `abandoned` fires)
    return true;
});
connection.Send(Envelope.Create("Chat", data: "hello"));
```
