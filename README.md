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

`type` is a plain string; `data` is usually a JSON document of its own. An optional
`refreshToken` (a long-lived login token) is present only when set. See `Envelope`.

## Types

| Type | Role |
| --- | --- |
| `Envelope` | The packet: `Type`, `SenderId`, `RoomId`, `Data`, `Token`, `RefreshToken` (optional, omitted when null); `Create`, `Serialize`, `TryParse` (never throws). |
| `LineFraming` | Newline framing over a stream: `ReadLineAsync` (single reader), `WriteLine` (thread-safe). |
| `NetConnection` | The lifecycle: `ConnectAsync(handshake)`, `Disconnect`, `Reconnect`, `Send`, `SetHeartbeatInterval`, `IsConnected`, `ClientId`; events `EnvelopeReceived`, `Disconnected`. |
| `NetConnectionOptions` | `HeartbeatType` (null: no heartbeat), `HeartbeatInterval` (1 s, the starting value), `HeartbeatTimeout` (8 s). |
| `ConnectionHandshake` | `delegate Task<bool> (NetConnection, CancellationToken abandoned)`: the application's opening exchange (version check, login…). |
| `INetLog` / `NetLogLevel` / `NullNetLog` | Diagnostics hook; the library has no logger of its own. |
| `Transport.IStreamFactory` | Opens the stream a connection talks over. |
| `Transport.TcpStreamFactory` | Plain TCP to a host and port (e.g. LAN play). |
| `Transport.TlsStreamFactory` | TLS 1.3 only over TCP to a host and port; the host is the TLS target name. |
| `Transport.TlsCertificateValidation` | `SystemTrust` (the OS's certificate authorities) or `PinnedSha256(fingerprint)` (accept exactly that certificate, whatever its chain says). |

## Lifecycle

1. `ConnectAsync` opens a stream from the `IStreamFactory` and starts reading lines.
2. The handshake (if any) runs; it subscribes to `EnvelopeReceived`, sends, and completes with
   true or false. Its token is cancelled when the connection goes away before it finishes.
3. On success the heartbeat starts: a packet of `HeartbeatType` every interval
   (`SetHeartbeatInterval` changes it at any time: one goes out at once, then at the new pace);
   when nothing at all is received within the timeout (any line is a sign of life, not only
   heartbeats), the connection is closed. Received heartbeats are not raised as `EnvelopeReceived`.
4. Any end (remote close, read or write error, heartbeat timeout, `Disconnect`) raises
   `Disconnected` once. `Reconnect` closes and connects again with the last handshake; work of an
   old connection never touches the new one.

## Transports

The transport is the replaceable part: anything that yields a connected `Stream` (TLS over TCP,
a peer accepted by a local host's listener for LAN play…) implements `IStreamFactory`; the
framing, handshake and heartbeat stay the same.

## TLS on macOS

.NET on macOS uses SecureTransport by default, which has no TLS 1.3: a `TlsStreamFactory`
handshake then fails. The application must switch the runtime to Network.framework in its
executable project:

```xml
<ItemGroup>
  <RuntimeHostConfigurationOption Include="System.Net.Security.UseNetworkFramework" Value="true" />
</ItemGroup>
```

(the same as the environment variable `DOTNET_SYSTEM_NET_SECURITY_USENETWORKFRAMEWORK=1`).

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
