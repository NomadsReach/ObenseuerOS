(function () {
  "use strict";
  const { el, button, field } = Apps;

  const POLL_MS = 2000;
  const TEXT_MAX = 200;
  const NO_HOST = "TOR-Turnip works inside the game.";
  const BADGE_MAX = 99;
  const HEADS = [["Private Chats", true], ["Channels", false]];

  function textOf(e, fallback) {
    if (e && e.message === "No game host") return NO_HOST;
    return (e && e.message) || fallback;
  }

  Apps.register("chat", {
    mount(slot, ctx) {
      const st = { room: "internet", from: 0, gen: 0, layout: "", rows: new Map() };
      const title = el("p", "chat-title", "TOR-Turnip");
      const side = el("aside", "chat-side");
      const status = el("p", "chat-status");
      const log = el("div", "chat-log");
      const input = field("text", "", "Message");
      input.maxLength = TEXT_MAX;
      const send = button("", "Send");
      const bar = el("div", "chat-send");
      bar.append(input, send);
      const main = el("div", "chat-main");
      main.append(title, status, log, bar);
      const root = el("div", "chat");
      root.append(side, main);
      slot.append(root);

      function build(rooms) {
        for (const [head, contact] of HEADS) {
          side.append(el("p", "chat-head", head));
          for (const r of rooms) {
            if (r.contact !== contact) continue;
            const badge = el("span", "chat-badge");
            const btn = button("chat-room", "");
            btn.append(el("span", "chat-name", r.name), badge);
            btn.addEventListener("click", () => switchTo(r.id));
            side.append(btn);
            st.rows.set(r.id, { btn, badge, name: r.name });
          }
        }
      }

      function layout(rooms) {
        const key = JSON.stringify(rooms.map((r) => [r.id, r.name, r.contact]));
        if (key === st.layout) return;
        st.layout = key;
        side.replaceChildren();
        st.rows = new Map();
        build(rooms);
      }

      function mark() {
        for (const [id, row] of st.rows) row.btn.setAttribute("aria-pressed", String(id === st.room));
        const row = st.rows.get(st.room);
        title.textContent = row ? row.name : "TOR-Turnip";
      }

      function badges(rooms) {
        for (const r of rooms) {
          const row = st.rows.get(r.id);
          if (!row) continue;
          const on = r.id === st.room;
          row.badge.textContent = !on && r.unread > 0 ? (r.unread > BADGE_MAX ? BADGE_MAX + "+" : Apps.num(r.unread, 0)) : "";
        }
      }

      function addLine(line) {
        const row = el("p", line.own ? "chat-line chat-own" : "chat-line");
        row.append(el("span", "chat-from", line.from + ": "), el("span", "", line.text));
        log.append(row);
      }

      function load() {
        const gen = st.gen;
        const from = st.from;
        ctx.call("chat.since", { room: st.room, from }).then(
          (res) => {
            if (gen !== st.gen || from !== st.from) return;
            layout(res.rooms);
            for (const line of res.lines) addLine(line);
            if (res.lines.length) log.scrollTop = log.scrollHeight;
            st.from = res.next;
            badges(res.rooms);
            mark();
            status.textContent = "";
          },
          (e) => {
            if (gen !== st.gen) return;
            const next = e && e.error === "bad_args" ? [...st.rows.keys()].find((id) => id !== st.room) : undefined;
            if (next) return switchTo(next);
            status.textContent = textOf(e, "Offline");
          }
        );
      }

      function switchTo(id) {
        if (id === st.room) return;
        st.room = id;
        st.gen++;
        st.from = 0;
        log.replaceChildren();
        status.textContent = "";
        mark();
        load();
      }

      function post() {
        const text = input.value.trim();
        if (!text) return;
        const room = st.room;
        input.value = "";
        ctx.call("chat.send", { room, text }).then(
          () => load(),
          (e) => {
            input.value = text;
            status.textContent = textOf(e, "Not sent");
          }
        );
      }

      input.addEventListener("keydown", (e) => {
        if (e.key === "Escape") {
          e.stopPropagation();
          return;
        }
        if (e.key !== "Enter") return;
        e.preventDefault();
        post();
      });
      send.addEventListener("click", post);

      load();
      const timer = setInterval(() => {
        if (!root.isConnected) {
          clearInterval(timer);
          return;
        }
        load();
      }, POLL_MS);
    },
  });
})();
