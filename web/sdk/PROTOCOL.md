# ObenseuerOS frame protocol

Mod pages run in a sandboxed iframe inside the OS window. The page talks only to the shell, with `window.parent.postMessage`. The shell checks `event.source` against its own frames, so it knows which app sent a message. The app id is taken from the frame, never from the message.

Every message is an object with `obxos: 1` and a `kind`. Any other message is ignored.

## Messages

| Direction | kind | Fields | Handled by |
| --- | --- | --- | --- |
| frame to shell | `hello` | none | shell replies `init` |
| frame to shell | `call` | `id`, `method`, `args` | `ctx.call`, then `res` |
| frame to shell | `title` | `text` (1 to 40) | window title, app windows only |
| frame to shell | `size` | `w`, `h` | clamped resize, app windows only |
| frame to shell | `close` | none | `closeApp` |
| frame to shell | `notify` | `title`, `body` | `toast` with the "from" line, same caps as C# |
| frame to shell | `url` | `url` | `openApp("browser", {url})` |
| frame to shell | `key` | `key` (`"Escape"`) | Closes the shell's open menu, or else the app's window (the browser window for a site frame). Sent only when the focus is not in a text field or a select. |
| shell to frame | `init` | `app`, `theme`, `user`, `opts` | |
| shell to frame | `res` | `id`, `ok`, `result` or `error`, `message` | |
| shell to frame | `event` | `name`, `data` | from C# `Os.Emit` |
| shell to frame | `theme` | `mode` | |
| shell to frame | `open` | `opts` | app reopened |

`call.args` is a JSON object. `res.result` is the handler's JSON text, parsed. `res.error` is one of the codes below, and `res.message` is English text for a developer.

## Error codes

| Code | Meaning |
| --- | --- |
| `bad_args` | `args` is not a JSON object, or it cannot be converted to JSON (circular, BigInt), or a value is missing or out of range. |
| `busy` | The game is saving or loading. |
| `denied` | The method is not allowed for this app, for example a file or money method from a mod page. Also sent for a call still in flight when its app was removed. |
| `failed` | The handler or the OS failed. `message` says why, for example "no such file". |
| `too_large` | Call args or a result are over 12 KB. |
| `unknown_method` | This app has no such method. |
| `rate_limited` | More than 16 calls are in flight, or more than 30 calls were sent in one second. |

## Limits

- Call args: at most 12 KB of JSON, counted in UTF-8 bytes (the same count as C#), so the forwarded message stays under the 16,384-character view limit.
- Calls: at most 16 in flight and 30 a second per frame. A call over a limit is answered at once with `rate_limited`. The SDK gives up on a call after 10 seconds, the same as the shell.
- Results and `event` data: at most 12 KB.
- Notices: `notify` is limited to 3 per 10 seconds per mod, shared by all its frames. Title at most 60 characters, body at most 200.
- Links: `url` is limited to 5 per 10 seconds per mod.
- Mod text is set with `textContent` in the shell. It is never parsed as HTML.

## Messages between the shell and C#

Every message has `v: 1` and a `type`. Mod text is shown with `textContent`.

| Direction | type | Fields | Notes |
| --- | --- | --- | --- |
| shell to C# | `open` | `app` | A window or a site frame opened. |
| shell to C# | `closed` | `app` | A window closed, or a site frame was removed from the browser. |
| shell to C# | `req` | `id`, `app`, `method`, `args` | A frame call, with the app id taken from the frame. |
| C# to shell | `apps` | `apps`, `sites`, `user` | Replaces the app list and the mod sites. A frame whose app or site is no longer listed, or whose entry now has another `mod` or `frame`, is blanked, and its calls in flight get `denied`. |
| C# to shell | `openapp` | `app` | Opens that app's window. |
| C# to shell | `toast` | `title`, `body`, `from`, `quiet` | `from` is the mod's app title. `quiet: true` only adds the notice to the list. |
| C# to shell | `badge` | `app`, `count` | Also sent in each `apps` entry. |

App entries: `id`, `title`, `icon` (a bundled icon name), `launch`. Page apps add `frame`, `w`, `h` (0 for the default size) and `iconUrl` (a file icon). Mod apps add `mod` (the owning mod id), `desk`, `pin` and `badge`.

Site entries in `sites`: `id`, `mod`, `host`, `title`, `description`, `keywords`, `frame`.

`frame` is `https://<label>.<view host>/<page>`. The browser mounts a site frame with its path in the hash. The shell sends `open` when a site frame mounts and `closed` when it is removed.

Methods a mod frame may call through `req`: `os.time.now`, `os.account.name`, `os.mod.read` (`name`), `os.mod.write` (`name`, `text`), `os.mod.open` (`name`). Any other method is `denied`. Args are at most 12 KB, and a mod app may make 30 calls a second.
