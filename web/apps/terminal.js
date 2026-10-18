(function () {
  "use strict";
  const { el, field, exists, writeFile } = Apps;

  const BANNER = "Viewports [Version 8]";
  const DIRS = [
    { name: "Documents", dir: "/docs" },
    { name: "Downloads", dir: "/downloads" },
  ];
  const PAGE = 50;
  const READ = 2000;
  const HISTORY_MAX = 50;
  const NOT_FOUND = "The system cannot find the file specified.";
  const NO_PATH = "The system cannot find the path specified.";
  const SYNTAX = "The syntax of the command is incorrect.";
  const NOT_READY = "The game clock is not ready.";
  const FAILED = "The command could not be completed.";
  const HELP = [
    "CD        Show or change the current folder.",
    "CLS       Clear the screen.",
    "DATE      Show the game date.",
    "DIR       List the files in the current folder.",
    "ECHO      Show a line of text.",
    "EXIT      Close Command Prompt.",
    "HELP      Show this list.",
    "MONEY     Show your OC and RM.",
    "NOTEPAD   Open a file in Notepad.",
    "START     Open an app by name.",
    "TIME      Show the game time.",
    "TYPE      Show the contents of a file.",
    "VER       Show the version.",
    "WHOAMI    Show the player name.",
    "Type an app name, such as files or bank, to open it.",
  ];

  function nameOf(p) {
    return p.slice(p.lastIndexOf("/") + 1);
  }

  Apps.register("terminal", {
    mount(slot, ctx) {
      const cmd = el("div", "cmd");
      const prompt = el("span", "");
      const input = field("text", "cmd-input", "Command");
      input.autocomplete = "off";
      input.spellcheck = false;
      const line = el("p", "cmd-line");
      line.append(prompt, input);
      cmd.append(el("p", "", BANNER), line);
      slot.append(cmd);
      cmd.addEventListener("click", () => input.focus());

      let here = DIRS[0];
      let queue = Promise.resolve();
      const past = [];
      let hi = 0;

      function where() {
        return `C:\\Users\\${ctx.info().user}${here ? `\\${here.name}` : ""}`;
      }

      function show() {
        prompt.textContent = `${where()}>`;
      }

      function out(text) {
        cmd.insertBefore(el("p", "cmd-out", text), line);
        cmd.scrollTop = cmd.scrollHeight;
      }

      function appFor(name) {
        return ctx.info().apps.find((a) => a.id === name || a.title.toLowerCase() === name);
      }

      async function listAll(dir) {
        const got = [];
        for (;;) {
          const r = await ctx.call("os.fs.list", { dir, offset: got.length, limit: PAGE });
          if (!r.items.length) return got;
          got.push(...r.items);
          if (got.length >= r.total) return got;
        }
      }

      async function readAll(path) {
        const total = await ctx.call("os.fs.size", { path });
        let got = "";
        while (got.length < total) {
          const part = await ctx.call("os.fs.read", { path, offset: got.length, length: Math.min(READ, total - got.length) });
          if (!part) throw new Error("the file changed while reading");
          got += part;
        }
        return got;
      }

      const cmds = {
        cls() {
          cmd.replaceChildren(line);
          input.focus();
        },
        help() {
          HELP.forEach((l) => out(l));
        },
        ver() {
          out(BANNER);
        },
        echo(arg) {
          out(arg || "ECHO is on.");
        },
        whoami() {
          out(ctx.info().user);
        },
        date() {
          const c = ctx.info().clock;
          if (!c) return out(NOT_READY);
          out(`The current date is: ${Apps.num(c.day, 0)}.1.1843, ${c.weekday}`);
        },
        time() {
          const c = ctx.info().clock;
          if (!c) return out(NOT_READY);
          out(`The current time is: ${Apps.num(c.hour, 0).padStart(2, "0")}:${Apps.num(c.minute, 0).padStart(2, "0")}`);
        },
        money() {
          const m = ctx.info().money;
          if (!m) return out(NOT_READY);
          out(`${Apps.money(m.oc, "OC")}   ${Apps.money(m.rm, "RM")}`);
        },
        async dir() {
          out(`Directory of ${where()}`);
          out("");
          if (!here) {
            for (const d of DIRS) out(`<DIR>          ${d.name}`);
            return;
          }
          const items = await listAll(here.dir);
          if (!items.length) return out("File Not Found");
          for (const it of items) out(`${nameOf(it.path).padEnd(32)}${Apps.num(it.bytes, 0)} bytes`);
          out("");
          out(`${Apps.num(items.length, 0)} File(s)`);
        },
        cd(arg) {
          if (!arg) return out(where());
          if (arg === ".." || arg === "\\") here = null;
          else {
            const d = DIRS.find((x) => x.name.toLowerCase() === arg.toLowerCase());
            if (!d) return out(NO_PATH);
            here = d;
          }
        },
        async type(arg) {
          if (!arg) return out(SYNTAX);
          if (!here) return out(NOT_FOUND);
          const path = `${here.dir}/${arg}`;
          if (!(await exists(ctx, path))) return out(NOT_FOUND);
          out(await readAll(path));
        },
        async notepad(arg) {
          if (!arg) return ctx.open("notes");
          if (!here) return out(NOT_FOUND);
          const path = `${here.dir}/${arg}`;
          if (!(await exists(ctx, path))) await writeFile(ctx, path, "");
          ctx.open("notes", { path });
        },
        start(arg) {
          if (!arg) return out(SYNTAX);
          const app = appFor(arg.toLowerCase());
          if (!app) return out(`Viewports cannot find '${arg}'. Make sure you typed the name correctly, and then try again.`);
          ctx.open(app.id);
        },
        exit() {
          ctx.close();
        },
      };

      async function exec(text) {
        const [, word, arg] = /^(\S+)\s*(.*)$/.exec(text);
        const w = word.toLowerCase();
        if (Object.hasOwn(cmds, w)) return cmds[w](arg);
        const app = appFor(w);
        if (app) return ctx.open(app.id);
        out(`'${word}' is not recognized as an internal or external command, operable program or batch file.`);
      }

      function run(raw) {
        const text = raw.trim();
        out(`${where()}>${text}`);
        if (!text) return;
        past.push(text);
        if (past.length > HISTORY_MAX) past.shift();
        hi = past.length;
        queue = queue
          .then(() => exec(text))
          .catch((e) => out(e.message || FAILED))
          .then(show);
      }

      input.addEventListener("keydown", (e) => {
        if (e.key === "Enter") {
          run(input.value);
          input.value = "";
        } else if (e.key === "ArrowUp" || e.key === "ArrowDown") {
          e.preventDefault();
          hi = Math.min(past.length, Math.max(0, hi + (e.key === "ArrowUp" ? -1 : 1)));
          input.value = hi < past.length ? past[hi] : "";
        }
      });

      show();
    },
  });
})();
