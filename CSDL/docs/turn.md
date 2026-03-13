# TURN / coturn setup for WebRTC

This document explains how to configure a TURN server (coturn) and how to wire it into the Flutter client for reliable WebRTC connections across NAT/firewalls.

## Why TURN
- STUN helps peers discover their public IP but doesn't relay media when both peers are behind symmetric NATs or strict firewalls.
- TURN relays media through a publicly reachable server.

## Quick coturn install (Ubuntu)
1. Install:

```bash
sudo apt update
sudo apt install coturn
```

2. Configure `/etc/turnserver.conf` (minimal):

```
listening-port=3478
tls-listening-port=5349
listening-ip=0.0.0.0
relay-ip=0.0.0.0
fingerprint
lt-cred-mech
use-auth-secret
static-auth-secret=YOUR_STATIC_SECRET_HERE
realm=your.domain.com
userdb=/var/lib/turn/turndb
no-stdout-log

alt-allowed-peer-ip=0.0.0.0/0

tls-cert=/etc/ssl/certs/your_cert.pem
tls-key=/etc/ssl/private/your_key.pem
```

3. Start coturn as a service:

```bash
sudo systemctl enable coturn
sudo systemctl start coturn
sudo systemctl status coturn
```

4. If you don't use `use-auth-secret`, you can add long-lived users via `turnadmin`.

## Security / credentials
- Use `use-auth-secret` + `static-auth-secret` or short-lived credentials (recommended). For production, prefer time-limited credentials.
- Open only necessary ports (3478 for UDP/TCP, 5349 for TLS).

## Example environment variables (for Flutter client)
In your `.env` or environment where you run the app, set:

```
TURN_URL=turn:your.turn.server:3478
TURN_USERNAME=turnuser
TURN_CREDENTIAL=turnpassword
```

If using TLS (recommended):
```
TURN_URL=turns:your.turn.server:5349
```

## Client-side: using the existing ApiConfig
The Flutter app already reads `TURN_URL`, `TURN_USERNAME`, `TURN_CREDENTIAL` from environment (see `lib/config/api_config.dart`). When set, the client includes TURN entry in `rtcIceServers`.

Example `rtcIceServers` result with TURN:

```json
[
  { "urls": "stun:stun.l.google.com:19302" },
  { "urls": "turn:your.turn.server:3478", "username": "turnuser", "credential": "turnpassword" }
]
```

## Testing notes
- Start server (coturn) and confirm port reachable: `telnet your.turn.server 3478` or `nc -vz your.turn.server 3478`.
- Run two clients behind different networks (or enable airplane mode to simulate strict networks) and verify media flows.
- Use WebRTC internals (browser) or logging in the app to confirm candidates and relay usage.

## Troubleshooting
- If clients never exchange candidates, check SignalR reachability and hub logs.
- If candidates establish but no media, check TURN ports and firewall.
- Use `turnutils_uclient` (from coturn) to test allocation.

---
If you want, tôi có thể:
- (1) Tự động thêm a sample `.env.example` file with TURN placeholders, hoặc
- (2) Thêm a small helper in server to generate time-limited TURN credentials (if coturn supports TURN REST API) — cần coturn config.

Chọn 1 hoặc 2 hoặc tôi sẽ tiếp tục với bước kiểm thử `dotnet build` để chắc server vẫn build ok.