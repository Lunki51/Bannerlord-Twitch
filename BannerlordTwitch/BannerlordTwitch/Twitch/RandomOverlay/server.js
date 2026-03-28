/**
 * BLT Overlay — Local Relay Server
 * Bridges the Bannerlord game mod to a browser-based overlay via WebSocket.
 * OBS Browser Source → http://localhost:3000
 * Game mod          → ws://localhost:9001 (x-client-type: game header)
 * Overlay browser   → ws://localhost:9001 (no special header)
 */

const express = require('express');
const http = require('http');
const { WebSocketServer, WebSocket } = require('ws');
const fs = require('fs');
const path = require('path');
const open = require('open');

// ── Config ────────────────────────────────────────────────────
const CONFIG_PATH = path.join(__dirname, 'config.json');
const PUBLIC_DIR = path.join(__dirname, 'public');
const PORT_HTTP = 3000;
const PORT_WS = 9001;

const DEFAULT_CONFIG = {
    clientId: '',   // Optional: Twitch Client ID for bot auth
    accessToken: '',   // Optional: filled by /auth flow
};

function loadConfig() {
    if (fs.existsSync(CONFIG_PATH)) {
        try { return { ...DEFAULT_CONFIG, ...JSON.parse(fs.readFileSync(CONFIG_PATH, 'utf8')) }; }
        catch { console.warn('[Config] Failed to parse config.json, using defaults'); }
    }
    return { ...DEFAULT_CONFIG };
}
function saveConfig(cfg) {
    fs.writeFileSync(CONFIG_PATH, JSON.stringify(cfg, null, 2));
}

let config = loadConfig();
if (!fs.existsSync(CONFIG_PATH)) saveConfig(config);

// ── Express ───────────────────────────────────────────────────
const app = express();
const server = http.createServer(app);
app.use(express.json());
app.use(express.static(PUBLIC_DIR));

// Status endpoint — useful for health checks / debugging
app.get('/status', (_req, res) => {
    res.json({
        gameConnected: gameSocket !== null && gameSocket.readyState === WebSocket.OPEN,
        overlayClients: overlayClients.size,
        hasToken: !!config.accessToken,
        uptime: process.uptime(),
    });
});

// ── Optional Twitch OAuth (implicit grant — no secret required) ─
app.get('/auth', (_req, res) => {
    if (!config.clientId)
        return res.send('<h2>Set clientId in config.json first, then restart.</h2>');
    const scopes = 'channel:read:redemptions channel:manage:redemptions chat:read chat:edit';
    const redirect = `http://localhost:${PORT_HTTP}/callback`;
    const url = `https://id.twitch.tv/oauth2/authorize`
        + `?client_id=${config.clientId}`
        + `&redirect_uri=${encodeURIComponent(redirect)}`
        + `&response_type=token`
        + `&scope=${encodeURIComponent(scopes)}`;
    res.redirect(url);
});

// Implicit grant delivers the token in the URL fragment (#), so we need
// a small JS snippet in the page to extract and POST it here.
app.get('/callback', (_req, res) => {
    res.send(`<!DOCTYPE html><html><body style="font-family:sans-serif;padding:2em">
    <p>Authenticating with Twitch…</p>
    <script>
      const params = Object.fromEntries(new URLSearchParams(location.hash.slice(1)));
      if (params.access_token) {
        fetch('/save-token', {
          method: 'POST',
          headers: {'Content-Type':'application/json'},
          body: JSON.stringify({ token: params.access_token })
        }).then(() => {
          document.body.innerHTML = '<h2 style="color:green">✓ Authenticated! You can close this tab.</h2>';
        });
      } else {
        document.body.innerHTML = '<h2 style="color:red">✗ No token received. Try /auth again.</h2>';
      }
    </script>
  </body></html>`);
});

app.post('/save-token', (req, res) => {
    config.accessToken = req.body?.token ?? '';
    saveConfig(config);
    console.log('[Auth] Token saved.');
    // Broadcast new auth status to any connected overlays
    broadcast({ type: 'auth', ok: true });
    res.json({ ok: true });
});

// ── WebSocket server ──────────────────────────────────────────
const wss = new WebSocketServer({ server: server, path: '/ws' });
// NOTE: using the same HTTP server so OBS doesn't block a second port

let gameSocket = null;               // Single game mod connection
const overlayClients = new Set();    // All browser overlay connections
let lastStateMsg = null;             // Cache last state for late-joiners

wss.on('connection', (ws, req) => {
    const isGame = req.headers['x-client-type'] === 'game';

    if (isGame) {
        // ── Game mod connection ─────────────────────────────────
        if (gameSocket && gameSocket.readyState === WebSocket.OPEN) {
            console.log('[WS] New game connection replacing stale one');
            gameSocket.terminate();
        }
        gameSocket = ws;
        console.log('[WS] Game mod connected');
        broadcast({ type: 'game_status', connected: true });

        ws.on('message', data => {
            const raw = data.toString();
            // Cache state messages so late-joining overlays get current state
            try {
                const m = JSON.parse(raw);
                if (m.type === 'state' || m.Kind === 'event') lastStateMsg = raw;
            } catch { /* ignore non-JSON */ }
            // Forward to all overlay clients
            for (const client of overlayClients) {
                if (client.readyState === WebSocket.OPEN) client.send(raw);
            }
        });

        ws.on('close', () => {
            console.log('[WS] Game mod disconnected');
            gameSocket = null;
            broadcast({ type: 'game_status', connected: false });
        });

        ws.on('error', e => console.error('[WS] Game error:', e.message));

    } else {
        // ── Overlay browser connection ──────────────────────────
        overlayClients.add(ws);
        console.log(`[WS] Overlay connected (${overlayClients.size} total)`);

        // Send cached state immediately so overlay isn't blank
        if (lastStateMsg) ws.send(lastStateMsg);
        // Also send current game connection status
        ws.send(JSON.stringify({ type: 'game_status', connected: gameSocket?.readyState === WebSocket.OPEN }));

        ws.on('message', data => {
            const raw = data.toString();
            // Forward overlay commands to game mod
            if (gameSocket?.readyState === WebSocket.OPEN) {
                gameSocket.send(raw);
            } else {
                console.warn('[WS] Command dropped — game not connected:', raw.slice(0, 120));
            }
        });

        ws.on('close', () => {
            overlayClients.delete(ws);
            console.log(`[WS] Overlay disconnected (${overlayClients.size} remaining)`);
        });

        ws.on('error', e => console.error('[WS] Overlay error:', e.message));
    }
});

// Helper: send JSON to all overlay clients
function broadcast(obj) {
    const msg = JSON.stringify(obj);
    for (const client of overlayClients) {
        if (client.readyState === WebSocket.OPEN) client.send(msg);
    }
}

// ── Start ─────────────────────────────────────────────────────
server.listen(PORT_HTTP, () => {
    console.log('');
    console.log('╔══════════════════════════════════════════════╗');
    console.log('║         BLT Overlay — Local Server           ║');
    console.log('╠══════════════════════════════════════════════╣');
    console.log(`║  Overlay URL  →  http://localhost:${PORT_HTTP}        ║`);
    console.log(`║  Game WS      →  ws://localhost:${PORT_HTTP}/ws       ║`);
    console.log(`║  Status       →  http://localhost:${PORT_HTTP}/status ║`);
    if (config.clientId)
        console.log(`║  Auth         →  http://localhost:${PORT_HTTP}/auth   ║`);
    console.log('╚══════════════════════════════════════════════╝');
    console.log('');
    console.log('Add OBS Browser Source: http://localhost:3000');
    console.log('Waiting for game mod connection…');

    // Auto-open the overlay in browser on startup
    open(`http://localhost:${PORT_HTTP}`).catch(() => { });
});

// Graceful shutdown
process.on('SIGINT', () => { console.log('\n[Server] Shutting down…'); server.close(() => process.exit(0)); });