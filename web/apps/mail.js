(function () {
  "use strict";
  const { el, button, field, web, num } = Apps;

  const PAGE = 20;
  const SEARCH_PAGES = 10;
  const FOLDERS = [["inbox", "Inbox"], ["important", "Important"], ["sent", "Sent Mail"], ["trash", "Trash"]];
  const DOMAINS = ["stalburg.ts", "opensewer.ts"];
  const NO_HOST = "Stalburg Mail works inside the game.";
  const BUSY = "The game is saving, try again in a moment.";
  const FAILED = "Something went wrong.";
  const NONE = "No messages here.";
  const SEARCH_MORE = "No match in the messages loaded so far. Show more to keep searching.";
  const HINT = "Choose a message to read it.";
  const SENT = "Sent.";
  const DELETED = "Deleted.";
  const TRASHED = "Moved to Trash.";
  const FEATURES = [
    ["Your own address", "A mailbox at @stalburg.ts or @opensewer.ts, kept with this save."],
    ["Inbox, Sent and Trash", "Deleted mail waits in Trash until you delete it for good."],
    ["Quick replies", "Answer people you have already written to, in one click."],
  ];
  const FOOT = [
    ["Stalburg Mail", ["About", "Help", "Security"]],
    ["Accounts", ["Create account", "Domains", "Account help"]],
    ["Legal", ["Terms", "Privacy", "Acceptable use"]],
  ];

  function textOf(e) {
    if (e && e.message === "No game host") return NO_HOST;
    if (e && e.error === "busy") return BUSY;
    return (e && e.message) || FAILED;
  }

  function reply(subject) {
    return /^re: /i.test(subject) ? subject : "Re: " + subject;
  }

  function fwd(subject) {
    return /^fwd: /i.test(subject) ? subject : "Fwd: " + subject;
  }

  function quote(body) {
    return body.split("\n").map((line) => "> " + line).join("\n");
  }

  function nameOf(addr) {
    return addr.replace(/\s*<[^>]*>\s*$/, "") || addr;
  }

  function addrOf(text) {
    const m = /<([^>]*)>\s*$/.exec(text);
    return (m ? m[1] : text).trim();
  }

  function brand(cls) {
    const logo = el("span", "mail-brand " + cls);
    logo.append(el("span", "mail-a", "Stalburg"), el("span", "mail-b", "MAIL"));
    return logo;
  }

  function labelled(text, input) {
    const row = el("label", "mail-field");
    row.append(el("span", "mail-label", text), input);
    return row;
  }

  Apps.register("mail", {
    mount(slot, ctx) {
      const page = web(slot, "Stalburg Mail", "https://mail.stalburg.ts/", ctx.embedded);
      page.classList.add("mail-page");
      const notice = el("p", "mail-notice");
      notice.setAttribute("role", "alert");
      notice.hidden = true;
      const body = el("div", "mail-body");
      page.append(notice, body);
      const st = { recipients: [], unread: 0, counts: {}, folder: "inbox", loaded: 0, seq: 0, find: 0, picked: new Set(), open: null };
      const menus = [];
      let box = null;

      page.addEventListener("click", (e) => {
        if (!e.target.closest(".mail-drop")) closeMenus();
      });

      function say(text) {
        notice.textContent = text || "";
        notice.hidden = !text;
      }

      function call(method, args) {
        return ctx.call(method, args).catch((e) => {
          say(textOf(e));
          return null;
        });
      }

      function closeMenus() {
        for (const m of menus) m.hidden = true;
      }

      function dropdown(label, items) {
        const wrap = el("span", "mail-drop");
        const trigger = button("mail-tb", label);
        const menu = el("div", "mail-menu");
        menu.hidden = true;
        for (const [text, fn] of items) {
          const it = button("mail-item", text);
          it.addEventListener("click", () => {
            closeMenus();
            fn();
          });
          menu.append(it);
        }
        trigger.addEventListener("click", () => {
          const open = menu.hidden;
          closeMenus();
          menu.hidden = !open;
        });
        menus.push(menu);
        wrap.append(trigger, menu);
        return wrap;
      }

      function setView(node, quick) {
        box.view.replaceChildren(node);
        box.quick = quick || null;
        box.reply.disabled = !box.quick;
        for (const b of box.steps) b.disabled = !st.open;
      }

      function showHint() {
        st.open = null;
        const empty = el("div", "mail-empty");
        empty.append(el("p", "", HINT));
        setView(empty);
      }

      function renderFolders() {
        for (const f of box.folders) {
          const n = f.id === "inbox" ? st.unread : st.counts[f.id];
          f.count.textContent = n ? num(n, 0) : "";
          f.count.hidden = !n;
          f.b.classList.toggle("active", f.id === st.folder);
        }
        box.unread.disabled = st.folder !== "inbox";
      }

      function filterRows(text) {
        const q = text.trim().toLowerCase();
        for (const li of box.list.children) li.hidden = Boolean(q) && !li.textContent.toLowerCase().includes(q);
      }

      function visibleRows() {
        return [...box.list.children].some((li) => !li.hidden);
      }

      function searchPages(run, left) {
        filterRows(box.search.value);
        if (run !== st.find || !box.search.value.trim() || visibleRows() || box.more.hidden) return;
        if (left) loadList(true).then(() => searchPages(run, left - 1));
        else say(SEARCH_MORE);
      }

      function pickRows(test) {
        st.picked.clear();
        for (const pick of box.list.querySelectorAll(".mail-pick")) {
          const on = !pick.parentElement.hidden && test(pick.nextElementSibling);
          pick.checked = on;
          if (on) st.picked.add(Number(pick.dataset.id));
        }
      }

      function targets() {
        if (st.picked.size) return [...st.picked];
        return st.open ? [st.open.id] : [];
      }

      function rowFor(m) {
        const b = button("mail-row" + (m.read ? "" : " unread"), "");
        b.dataset.id = String(m.id);
        const sent = st.folder === "sent";
        const main = el("span", "mail-main");
        const line = el("span", "mail-line");
        line.append(el("span", "mail-who", sent ? "To: " + nameOf(m.to) : nameOf(m.from)), el("span", "mail-when", m.when));
        main.append(line, el("span", "mail-subj", m.subject));
        b.append(main);
        b.addEventListener("click", () => openMessage(m.id, b));
        const pick = field("checkbox", "mail-pick", "Select message");
        pick.dataset.id = String(m.id);
        pick.checked = st.picked.has(m.id);
        pick.addEventListener("change", () => {
          if (pick.checked) st.picked.add(m.id);
          else st.picked.delete(m.id);
        });
        const li = el("li", "");
        li.append(pick, b);
        return li;
      }

      function loadList(more) {
        const seq = ++st.seq;
        const offset = more ? st.loaded : 0;
        return call("mail.list", { folder: st.folder, offset, limit: PAGE }).then((r) => {
          if (!r || seq !== st.seq) return;
          if (!more) {
            box.list.replaceChildren();
            st.loaded = 0;
          }
          if (!r.items.length && !more) box.list.append(el("li", "mail-none", NONE));
          for (const m of r.items) box.list.append(rowFor(m));
          st.loaded += r.items.length;
          box.more.hidden = st.loaded >= r.total;
          filterRows(box.search.value);
        });
      }

      function step(dir) {
        const rows = [...box.list.querySelectorAll(".mail-row")].filter((r) => !r.parentElement.hidden);
        const at = st.open ? rows.findIndex((r) => r.dataset.id === String(st.open.id)) : -1;
        const next = rows[at + dir];
        if (next) next.click();
      }

      async function markUnread() {
        const ids = targets();
        if (st.folder !== "inbox" || !ids.length) return;
        say("");
        for (const id of ids) {
          if (!(await call("mail.unread", { id }))) return;
        }
        st.picked.clear();
        return refresh();
      }

      async function removeTargets() {
        const ids = targets();
        if (!ids.length) return;
        say("");
        let r = null;
        for (const id of ids) {
          r = await call("mail.delete", { id });
          if (!r) return;
        }
        st.picked.clear();
        if (st.open && ids.includes(st.open.id)) showHint();
        say(r.purged ? DELETED : TRASHED);
        return refresh();
      }

      function mailbox() {
        if (box) {
          body.replaceChildren(...box.parts);
          return box;
        }
        const search = field("search", "mail-search-in", "Search mail");
        search.placeholder = "Search mail";
        search.addEventListener("input", () => {
          say("");
          searchPages(++st.find, SEARCH_PAGES);
        });
        const address = el("span", "mail-address");

        const write = button("mail-compose-btn", "Compose");
        write.addEventListener("click", () => writeMail({}));
        const folders = FOLDERS.map(([id, label]) => {
          const b = button("mail-folder", "");
          const count = el("span", "mail-count");
          b.append(el("span", "", label), count);
          b.addEventListener("click", () => {
            st.folder = id;
            const run = ++st.find;
            st.picked.clear();
            closeMenus();
            renderFolders();
            showHint();
            loadList(false).then(() => searchPages(run, SEARCH_PAGES));
          });
          return { id, b, count };
        });
        const rail = el("nav", "mail-rail");
        rail.setAttribute("aria-label", "Folders");
        rail.append(brand("sm"), write, ...folders.map((f) => f.b), address);

        const list = el("ul", "mail-list");
        const more = button("mail-more", "Show more");
        more.hidden = true;
        more.addEventListener("click", () => loadList(true));
        const pane = el("section", "mail-pane");
        pane.append(search, list, more);

        const pickAll = button("mail-tb mail-tb-box", "");
        pickAll.setAttribute("aria-label", "Select all");
        pickAll.append(el("span", "mail-cbox"));
        pickAll.addEventListener("click", () => {
          const all = !st.picked.size;
          pickRows(() => all);
        });
        const select = el("span", "mail-group");
        select.append(pickAll, dropdown("▾", [
          ["All", () => pickRows(() => true)],
          ["None", () => pickRows(() => false)],
          ["Read", () => pickRows((r) => !r.classList.contains("unread"))],
          ["Unread", () => pickRows((r) => r.classList.contains("unread"))],
        ]));
        const reload = button("mail-tb", "↻");
        reload.setAttribute("aria-label", "Refresh");
        reload.addEventListener("click", () => refresh());
        const moreMenu = dropdown("More ▾", [["Mark as unread", markUnread], ["Delete", removeTargets]]);

        const replyBtn = button("mail-tb mail-tb-reply", "↩");
        replyBtn.setAttribute("aria-label", "Reply");
        replyBtn.addEventListener("click", () => {
          if (box.quick) box.quick.focus();
        });
        const prev = button("mail-tb", "◀");
        prev.setAttribute("aria-label", "Previous message");
        prev.addEventListener("click", () => step(-1));
        const next = button("mail-tb", "▶");
        next.setAttribute("aria-label", "Next message");
        next.addEventListener("click", () => step(1));
        const right = el("span", "mail-tb-right");
        right.append(replyBtn, prev, next);
        const top = el("header", "mail-top");
        top.append(select, reload, moreMenu, right);

        const view = el("section", "mail-view");
        const side = el("div", "mail-side");
        side.append(top, view);
        const cols = el("div", "mail-cols");
        cols.append(pane, side);
        const shell = el("div", "mail-shell");
        shell.append(rail, cols);

        body.replaceChildren(shell);
        box = { address, search, folders, list, more, view, reply: replyBtn, steps: [prev, next], unread: moreMenu.querySelector(".mail-item"), quick: null, parts: [shell] };
        showHint();
        return box;
      }

      function signUp() {
        const name = field("text", "mail-user-in", "Username");
        name.maxLength = 20;
        name.placeholder = "name";
        const domain = el("select", "mail-domain");
        domain.setAttribute("aria-label", "Domain");
        for (const d of DOMAINS) {
          const o = el("option", "", d);
          o.value = d;
          domain.append(o);
        }
        const row = el("div", "mail-address-row");
        row.append(name, el("span", "mail-at", "@"), domain);
        const create = button("mail-btn", "Create account");
        const submit = () => {
          if (create.disabled) return;
          say("");
          create.disabled = true;
          call("mail.signup", { username: name.value, domain: domain.value }).then((acc) => {
            create.disabled = false;
            if (acc) refresh();
          });
        };
        create.addEventListener("click", submit);
        name.addEventListener("keydown", (e) => {
          if (e.key === "Enter" && !e.isComposing) submit();
        });
        const card = el("section", "mail-card");
        card.append(el("h2", "mail-card-title", "Create your Stalburg Mail account"), el("p", "mail-help", "Pick a name and a domain. The account belongs to this save, and you stay signed in."), row, create);

        const intro = el("section", "mail-intro");
        const feats = el("ul", "mail-feats");
        feats.append(...FEATURES.map(([title, text]) => {
          const li = el("li", "");
          li.append(el("h3", "", title), el("p", "", text));
          return li;
        }));
        intro.append(el("h1", "mail-headline", "Email for Stalburg residents."), el("p", "mail-lede", "Send and receive mail from your own address, with folders for everything you write and keep."), feats);
        const landing = el("div", "mail-landing");
        landing.append(intro, card);

        const bar = el("header", "mail-bar");
        bar.append(brand("lg"));
        const cols = el("div", "mail-foot-cols");
        cols.append(...FOOT.map(([head, links]) => {
          const col = el("div", "mail-foot-col");
          col.append(el("h4", "", head), ...links.map((l) => el("span", "mail-foot-link", l)));
          return col;
        }));
        const foot = el("footer", "mail-foot");
        foot.append(cols, el("p", "mail-copy", "Stalburg Mail. Mailboxes are kept in this save."));
        body.replaceChildren(bar, landing, foot);
        name.focus();
      }

      function canReply(m) {
        return m.folder === "inbox" && st.recipients.includes(addrOf(m.from));
      }

      function sendMail(to, subject, text) {
        say("");
        return call("mail.send", { to, subject, body: text }).then((r) => {
          if (!r) return false;
          st.folder = "sent";
          say(SENT);
          return refresh().then(() => true);
        });
      }

      function attachment(m) {
        const row = el("div", "mail-links");
        const open = button("mail-link", m.attachment.opened ? "Opened" : "Open");
        const state = el("span", "mail-attach-state");
        open.disabled = Boolean(m.attachment.opened);
        open.addEventListener("click", () => {
          open.disabled = true;
          call("mail.attachment", { id: m.id }).then((r) => {
            if (!r) {
              open.disabled = false;
              return;
            }
            open.textContent = "Opened";
            state.textContent = r.outcome === "blocked" ? "Blocked by your security software." : "The attachment opened.";
          });
        });
        row.append(el("strong", "", m.attachment.file), open, state);
        return row;
      }

      function showMessage(m) {
        const from = el("div", "mail-meta-text");
        from.append(el("strong", "", m.from), el("span", "mail-addr", "to " + m.to));
        const star = button("mail-star", m.starred ? "★" : "☆");
        star.setAttribute("aria-label", "Important");
        star.addEventListener("click", () => {
          const on = star.textContent !== "★";
          call("mail.star", { id: m.id, on }).then((r) => {
            if (!r) return;
            star.textContent = r.starred ? "★" : "☆";
            return refresh();
          });
        });
        const meta = el("div", "mail-meta");
        meta.append(from, el("span", "mail-date", m.when), star);
        const article = el("article", "mail-read");
        article.append(el("h2", "mail-subject", m.subject), meta, el("pre", "mail-text", m.body));
        if (m.attachment) article.append(attachment(m));
        let quick = null;
        const links = el("div", "mail-links");
        if (canReply(m)) {
          quick = el("textarea", "mail-quick");
          quick.setAttribute("aria-label", "Quick reply");
          quick.maxLength = 3000;
          const back = button("mail-link", "↩ Reply");
          back.addEventListener("click", () => {
            if (!quick.value.trim()) {
              quick.focus();
              return;
            }
            back.disabled = true;
            sendMail(addrOf(m.from), reply(m.subject), quick.value).then((ok) => {
              back.disabled = false;
              if (ok) showHint();
            });
          });
          article.append(quick);
          links.append(back);
        }
        const forward = button("mail-link", "➤ Forward");
        forward.addEventListener("click", () => writeMail({ subject: fwd(m.subject), body: "\n\n" + quote(m.body) }));
        links.append(forward);
        article.append(links);
        setView(article, quick);
      }

      function openMessage(id, row) {
        say("");
        return call("mail.read", { id }).then((m) => {
          if (!m) return;
          st.open = { id };
          showMessage(m);
          row.classList.remove("unread");
          return status().then((s) => {
            if (s) renderFolders();
          });
        });
      }

      function writeMail(pre) {
        say("");
        const to = el("select", "mail-to");
        to.setAttribute("aria-label", "To");
        for (const a of st.recipients) {
          const o = el("option", "", a);
          o.value = a;
          to.append(o);
        }
        if (pre.to) to.value = pre.to;
        const subject = field("text", "mail-subject-in", "Subject");
        subject.maxLength = 100;
        subject.value = pre.subject || "";
        const text = el("textarea", "mail-text-in");
        text.setAttribute("aria-label", "Message");
        text.maxLength = 3000;
        text.value = pre.body || "";
        const send = button("mail-btn", "Send");
        const cancel = button("mail-ghost", "Discard");
        const go = () => {
          if (send.disabled) return;
          send.disabled = true;
          sendMail(to.value, subject.value, text.value).then((ok) => {
            send.disabled = false;
            if (ok) showHint();
          });
        };
        send.addEventListener("click", go);
        subject.addEventListener("keydown", (e) => {
          if (e.key === "Enter" && !e.isComposing) {
            e.preventDefault();
            text.focus();
          }
        });
        text.addEventListener("keydown", (e) => {
          if (e.key === "Enter" && e.ctrlKey) {
            e.preventDefault();
            go();
          }
        });
        cancel.addEventListener("click", () => showHint());
        const actions = el("div", "mail-actions");
        actions.append(send, cancel);
        const head = el("div", "mail-compose-head");
        const close = button("mail-x", "×");
        close.setAttribute("aria-label", "Close");
        close.addEventListener("click", () => showHint());
        head.append(el("span", "", "New message"), close);
        const form = el("div", "mail-compose");
        form.append(head, labelled("To", to), labelled("Subject", subject), labelled("Message", text), actions);
        setView(form);
        subject.focus();
      }

      function status() {
        return call("mail.status").then((s) => {
          if (!s) return null;
          st.recipients = s.recipients;
          st.unread = s.unread;
          return s;
        });
      }

      function totals() {
        return Promise.all(FOLDERS.map(([id]) => call("mail.list", { folder: id, offset: 0, limit: 1 }).then((r) => {
          if (r) st.counts[id] = r.total;
        })));
      }

      function refresh() {
        return status().then((s) => {
          if (!s) return;
          if (!s.account) return signUp();
          const m = mailbox();
          m.address.textContent = s.account.address;
          renderFolders();
          return totals().then(() => {
            renderFolders();
            return loadList(false);
          });
        });
      }

      refresh();
      return { onOpen: () => refresh() };
    },
  });
})();
