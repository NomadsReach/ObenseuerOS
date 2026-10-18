(function () {
  "use strict";
  const { el, button, web } = Apps;
  const NEED_LOGIN = "Log in to EYEbook first.";
  const LOGIN_FAIL = "EYEbook could not read your name. Try again later.";
  const SHARED_ALREADY = "You already shared this post.";
  const SIGNUP_CLOSED = "New registrations are closed.";
  const SLOGAN = "...SO WE CAN KEEP AN EYE ON YOU!";
  const DISCLAIMER = "EYEbook will help us to collect information about you. Sign up is free and will always be. When you sign up for EYEbook, you are required to tell everything about yourself. We also collect information about you without your permission. You can sign up for EYEbook if you are over 3 years old. When you want to delete an account, it typically takes about 30 years to delete an account.";
  const FILE = "/eyebook/state.json";
  const KEY = "obx.os.eyebook";
  const NO_FILE = "no such file";
  const KINDS = ["like", "comment", "share"];
  const ACT_MAX = 40;
  const TEXT_MAX = 140;
  const STATUS_KEEP = 10;
  const NEWS_MAX = 3;
  const TIMES = "www.stalburgtimes.com";
  const ROUTES = { "/profile": "profile", "/friends": "friends", "/postal": "postal", "/notifications": "notifications", "/saved": "saved", "/photos": "photos", "/pages": "pages" };
  const NAME_MAX = 40;
  const CHUNK = 4000;
  const SHORTCUTS = [["profile", "Profile"], ["friends", "Friends"], ["postal", "Postal"], ["photos", "Photos"], ["pages", "Pages"], ["saved", "Saved"]];
  const TOP_LINKS = [["home", "Home"], ["friends", "Friends"], ["postal", "Postal"], ["notifications", "Notifications"]];
  const POSTS = [
    { name: "Stalburg Times", av: "a", when: "1 h", page: true, text: "The water tunnel story is on the front page again. Our timeline puts the dates in order before you believe the rumours.", pic: "industry", alt: "Mine tunnel with pipes and rails", likes: 412, comments: 58 },
    { name: "Jarmo Pettinen", av: "b", when: "3 h", text: "Queue at the Money Exchange in Kolhola again. The rate changes faster than the line moves.", likes: 37, comments: 9 },
    { name: "Osmo Olut", av: "c", when: "5 h", page: true, promoted: true, text: "Friday at the bar: a cold one, a quiet corner and nobody asking about the tunnel.", likes: 128, comments: 14 },
    { name: "Johan Wider", av: "d", when: "7 h", text: "Does the 7 tram run on Sunday? The route board says one thing and the platform says another.", likes: 21, comments: 17 },
    { name: "Stalburg Mass Transit", av: "e", when: "1 d", page: true, text: "Service update: the Cellecter line runs a reduced timetable this weekend. Check the stop boards before you travel.", likes: 96, comments: 6 },
    { name: "Pera Harju", av: "f", when: "1 d", text: "Evening walk through the old quarter. The lights are back on in the hill houses.", pic: "street", alt: "Town buildings at dusk", likes: 54, comments: 11 },
    { name: "Hammer Valley Hydro", av: "b", when: "2 d", page: true, text: "Visitor centre opening hours for the weekend are on the notice board at the dam. Bring a coat.", likes: 33, comments: 4 },
  ];
  const ADS = [
    { brand: "Osmo Olut", cls: "osmo", text: "Strong, cold and ready after work. Ask for it at your local bar." },
    { brand: "Prism", cls: "prism", text: "Weekly offers on the food you need. Find your nearest store." },
    { brand: "Stalburg Bank", cls: "bank", text: "Open an account at any branch and see your balance in one place.", site: "www.stalburgbank.ts" },
  ];
  const CONTACTS = [
    ["Jarmo Pettinen", "b"],
    ["Johan Wider", "d"],
    ["Pera Harju", "f"],
    ["Markku Siltanen", "c"],
  ];
  const THREADS = [
    { name: "Jarmo Pettinen", av: "b", when: "2 h", msgs: ["Are you still in the Kolhola queue at the Money Exchange? The rate moved again.", "Never mind, I got through. Bring OC next time."] },
    { name: "Johan Wider", av: "d", when: "1 d", msgs: ["Did the 7 tram run on Sunday? The board said one thing and the platform another.", "Forget it. I walked from Tilstrand."] },
    { name: "Pera Harju", av: "f", when: "3 d", msgs: ["I have new pictures of the old quarter in Obenseuer. Want a copy?", "Send me the Times piece when it is out. I want the water tunnel part."] },
    { name: "Markku Siltanen", av: "c", when: "1 w", msgs: ["Did you read the Bergmann Group story in the Times? Read it before you talk about the tunnels.", "Call me when you have a minute. Not on the phone."] },
  ];

  function photo(tag, seed) {
    const p = window.PHOTOS && window.PHOTOS.pick(tag, seed);
    return (p && p.thumb) || null;
  }

  function avatar(v) {
    return el("span", "eb-av eb-av-" + v);
  }

  function action(cls, label, fn) {
    const b = button(cls, label);
    b.addEventListener("click", fn);
    return b;
  }

  function panel(title, kids) {
    const box = el("section", "eb-card-side");
    box.append(el("h2", "eb-side-h", title), ...kids);
    return box;
  }

  function note(text) {
    return el("p", "eb-empty", text);
  }

  function heading(text) {
    return el("h2", "eb-view-h", text);
  }

  function posts(test) {
    return POSTS.map((p, i) => i).filter((i) => test(POSTS[i]));
  }

  function localGet() {
    try {
      return localStorage.getItem(KEY);
    } catch {
      return null;
    }
  }

  function localSet(text) {
    try {
      localStorage.setItem(KEY, text);
    } catch {}
  }

  function valid(a) {
    return Boolean(a) && KINDS.indexOf(a.k) >= 0 && Number.isInteger(a.p) && a.p >= 0 && a.p < POSTS.length && (a.k !== "comment" || (typeof a.t === "string" && a.t.length <= TEXT_MAX));
  }

  function validStatus(s) {
    return Boolean(s) && typeof s.t === "string" && s.t.length > 0 && s.t.length <= TEXT_MAX;
  }

  function parse(text) {
    try {
      const d = (text && JSON.parse(text)) || {};
      return {
        name: typeof d.name === "string" ? d.name.slice(0, NAME_MAX) : "",
        acts: Array.isArray(d.acts) ? d.acts.filter(valid).slice(-ACT_MAX) : [],
        status: Array.isArray(d.status) ? d.status.filter(validStatus).slice(-STATUS_KEEP) : [],
      };
    } catch {
      return { name: "", acts: [], status: [] };
    }
  }

  function find(query) {
    const q = query.trim().toLowerCase();
    const people = [];
    for (const [name, av] of CONTACTS.concat(POSTS.map((p) => [p.name, p.av]))) {
      if (name.toLowerCase().indexOf(q) >= 0 && !people.some((x) => x[0] === name)) people.push([name, av]);
    }
    const found = posts((p) => (p.name + " " + p.text).toLowerCase().indexOf(q) >= 0);
    return { people, posts: found };
  }

  function readAll(ctx) {
    return ctx.call("os.fs.size", { path: FILE }).then((size) => {
      const next = (offset, parts) => {
        if (offset >= size) return parts.join("");
        return ctx.call("os.fs.read", { path: FILE, offset, length: Math.min(CHUNK, size - offset) }).then((t) => {
          if (!t) throw { message: "short read" };
          return next(offset + t.length, parts.concat(t));
        });
      };
      return next(0, []);
    });
  }

  function load(ctx) {
    const text = ctx.live ? readAll(ctx) : Promise.resolve(localGet());
    return text.then(
      (t) => ({ state: parse(t), ok: true }),
      (e) => (e && e.message === NO_FILE ? { state: parse(""), ok: true } : { state: parse(""), ok: false, error: e && e.message })
    );
  }

  function warn(u, why) {
    if (u.warned) return;
    u.warned = true;
    console.warn("EYEbook: could not read the saved state (" + why + "). Saving waits and retries on the next change.");
  }

  function retry(u) {
    if (u.retrying) return;
    u.retrying = true;
    load(u.ctx).then((r) => {
      u.retrying = false;
      if (!r.ok) return;
      u.st.acts = r.state.acts.concat(u.st.acts).slice(-ACT_MAX);
      u.st.status = r.state.status.concat(u.st.status).slice(-STATUS_KEEP);
      u.st.name = u.st.name || r.state.name;
      u.writable = true;
      save(u);
      paint(u);
    });
  }

  function save(u) {
    if (!u.writable) return retry(u);
    const text = JSON.stringify(u.st);
    if (!u.ctx.live) localSet(text);
    else Apps.writeFile(u.ctx, FILE, text).catch(() => {});
  }

  function say(u, text) {
    u.notice.textContent = text;
    u.notice.hidden = !text;
  }

  function isLiked(u, i) {
    return u.st.acts.some((a) => a.k === "like" && a.p === i);
  }

  function need(u) {
    if (u.ready && u.st.name) return true;
    say(u, NEED_LOGIN);
    return false;
  }

  function commit(u) {
    u.st.acts = u.st.acts.slice(-ACT_MAX);
    save(u);
    paint(u);
  }

  function login(u) {
    if (!u.ready) return;
    u.ctx.call("os.account.name").then(
      (name) => {
        const n = typeof name === "string" ? name.trim().slice(0, NAME_MAX) : "";
        if (!n) return say(u, LOGIN_FAIL);
        u.st.name = n;
        say(u, "");
        commit(u);
      },
      () => say(u, LOGIN_FAIL)
    );
  }

  function toggleLike(u, i) {
    if (!need(u)) return;
    u.st.acts = isLiked(u, i) ? u.st.acts.filter((a) => a.k !== "like" || a.p !== i) : u.st.acts.concat([{ k: "like", p: i }]);
    commit(u);
  }

  function share(u, i) {
    if (!need(u)) return;
    if (u.st.acts.some((a) => a.k === "share" && a.p === i)) return say(u, SHARED_ALREADY);
    u.st.acts = u.st.acts.concat([{ k: "share", p: i }]);
    commit(u);
  }

  function comment(u, i, text) {
    if (!need(u)) return;
    const t = text.trim().slice(0, TEXT_MAX);
    if (!t) return;
    u.st.acts = u.st.acts.concat([{ k: "comment", p: i, t }]);
    u.form = -1;
    commit(u);
  }

  function publish(u, text) {
    if (!need(u)) return;
    const t = text.trim().slice(0, TEXT_MAX);
    if (!t) return;
    u.st.status = u.st.status.concat([{ t }]).slice(-STATUS_KEEP);
    commit(u);
  }

  function openForm(u, i) {
    if (!need(u)) return;
    u.form = u.form === i ? -1 : i;
    paint(u);
  }

  function search(u, q) {
    u.q = q;
    u.input.value = q;
    paint(u);
  }

  function go(u, view) {
    u.view = view;
    u.thread = -1;
    u.form = -1;
    u.input.value = "";
    u.q = "";
    say(u, "");
    paint(u);
  }

  function landing() {
    const top = el("section", "eb-landing");
    const map = el("div", "eb-map");
    const art = el("img", "eb-map-art");
    art.src = "brand/eyebook-map.svg";
    art.alt = "";
    map.append(art, el("p", "eb-slogan", SLOGAN));
    top.append(map, signup());
    return top;
  }

  function signup() {
    const panelEl = el("section", "eb-join");
    const closed = el("p", "eb-note", SIGNUP_CLOSED);
    closed.hidden = true;
    const join = button("eb-signup", "Create new account");
    join.addEventListener("click", () => {
      closed.hidden = false;
    });
    panelEl.append(el("h2", "eb-join-h", "Sign up"), join, closed, el("p", "eb-disclaimer", DISCLAIMER));
    return panelEl;
  }

  function inputRow(label, send) {
    const row = el("div", "eb-cmt-row");
    const box = el("input", "eb-cmt-input");
    box.type = "text";
    box.maxLength = TEXT_MAX;
    box.placeholder = label;
    box.setAttribute("aria-label", label);
    const go = () => send(box.value);
    box.addEventListener("keydown", (e) => {
      if (e.key === "Enter") go();
    });
    row.append(box, action("eb-btn", "Post", go));
    return row;
  }

  function commentForm(u, i) {
    return inputRow("Write a comment", (t) => comment(u, i, t));
  }

  function composer(u) {
    return panel("Status", [inputRow("What is on your mind?", (t) => publish(u, t))]);
  }

  function statusPost(u, s) {
    const card = el("article", "eb-post");
    const head = el("div", "eb-post-head");
    const who = el("div", "eb-who");
    who.append(el("span", "eb-name", u.st.name), el("span", "eb-meta", "Status · Public"));
    head.append(avatar("a"), who);
    card.append(head, el("p", "eb-text", s.t));
    return card;
  }

  function timesPost(u, n) {
    const card = el("article", "eb-post");
    const head = el("div", "eb-post-head");
    const who = el("div", "eb-who");
    who.append(el("span", "eb-name", "Stalburg Times"), el("span", "eb-meta", n.section + " · Page"));
    head.append(avatar("a"), who);
    card.append(head, el("p", "eb-text", n.headline), action("eb-act", "Read in the Times", () => (u.visit ? u.visit(TIMES) : u.ctx.open("browser", { url: TIMES }))));
    return card;
  }

  function post(u, i, shared) {
    const p = POSTS[i];
    const liked = isLiked(u, i);
    const notes = u.st.acts.filter((a) => a.k === "comment" && a.p === i);
    const card = el("article", "eb-post");
    if (shared) card.append(el("p", "eb-shared", "You shared this"));
    const head = el("div", "eb-post-head");
    const who = el("div", "eb-who");
    const name = el("span", "eb-name", p.name);
    name.addEventListener("click", () => search(u, p.name));
    const meta = el("span", "eb-meta", p.when + " · " + (p.promoted ? "Promoted" : "Public"));
    who.append(name, meta);
    head.append(avatar(p.av), who);
    card.append(head, el("p", "eb-text", p.text));
    const src = p.pic ? photo(p.pic, i) : null;
    if (src) {
      const img = el("img", "eb-pic");
      img.src = src;
      img.alt = p.alt;
      card.append(img);
    }
    const counts = el("div", "eb-counts", (p.likes + (liked ? 1 : 0)) + " likes · " + (p.comments + notes.length) + " comments");
    const like = action("eb-act", "Like", () => toggleLike(u, i));
    like.setAttribute("aria-pressed", String(liked));
    const bar = el("div", "eb-bar");
    bar.append(like, action("eb-act", "Comment", () => openForm(u, i)), action("eb-act", "Share", () => share(u, i)));
    card.append(counts, bar);
    for (const a of notes) card.append(el("p", "eb-cmt", u.st.name + ": " + a.t));
    if (u.form === i) card.append(commentForm(u, i));
    return card;
  }

  function ad(u, a) {
    const box = el("div", "eb-ad");
    box.append(el("div", "eb-ad-art eb-ad-" + a.cls, a.brand), el("p", "eb-ad-text", a.text));
    if (a.site) box.append(action("eb-ad-link", "Learn more", () => (u.visit ? u.visit(a.site) : u.ctx.open("browser", { url: a.site }))));
    return box;
  }

  function contact(u, c) {
    const row = el("div", "eb-contact");
    row.append(avatar(c[1]), el("span", "eb-contact-name", c[0]), el("span", "eb-dot"));
    row.addEventListener("click", () => search(u, c[0]));
    return row;
  }

  function thread(u, t, i) {
    const row = button("eb-thread", "");
    row.append(avatar(t.av), el("span", "eb-thread-name", t.name), el("span", "eb-meta", t.when), el("span", "eb-thread-last", t.msgs[t.msgs.length - 1]));
    row.addEventListener("click", () => {
      u.thread = i;
      paint(u);
    });
    return row;
  }

  function homeView(u) {
    const kids = [composer(u)];
    if (!u.st.name) kids.push(landing());
    for (const s of u.st.status.slice().reverse()) kids.push(statusPost(u, s));
    for (const a of u.st.acts.filter((x) => x.k === "share").reverse()) kids.push(post(u, a.p, true));
    return kids.concat(u.news.map((n) => timesPost(u, n)), POSTS.map((p, i) => post(u, i, false)));
  }

  function friendsView(u) {
    return [panel("Friends", CONTACTS.map((c) => contact(u, c)))];
  }

  function postalView(u) {
    if (u.thread < 0) return [panel("Postal", THREADS.map((t, i) => thread(u, t, i)))];
    const t = THREADS[u.thread];
    const back = action("eb-back", "Back to messages", () => {
      u.thread = -1;
      paint(u);
    });
    return [panel(t.name, [back].concat(t.msgs.map((m) => el("p", "eb-msg", m))))];
  }

  function notifyView(u) {
    if (!u.st.name) return [panel("Notifications", [note(NEED_LOGIN)])];
    const rows = u.st.acts
      .filter((a) => a.k !== "share")
      .reverse()
      .map((a) => el("p", "eb-note-row", a.k === "like" ? "You liked a post by " + POSTS[a.p].name + "." : "You commented on a post by " + POSTS[a.p].name + ": " + a.t));
    return [panel("Notifications", rows.length ? rows : [note("No likes or comments yet.")])];
  }

  function profileView(u) {
    if (!u.st.name) return [panel("Profile", [note(NEED_LOGIN)])];
    const shares = u.st.acts.filter((a) => a.k === "share").reverse();
    return [heading(u.st.name)].concat(u.st.status.slice().reverse().map((s) => statusPost(u, s)), shares.length ? shares.map((a) => post(u, a.p, true)) : [note("You have not shared anything yet.")]);
  }

  function savedView(u) {
    if (!u.st.name) return [panel("Saved", [note(NEED_LOGIN)])];
    const liked = u.st.acts.filter((a) => a.k === "like").map((a) => a.p);
    return [heading("Saved")].concat(liked.length ? liked.map((i) => post(u, i)) : [note("Nothing saved yet. Like a post to save it.")]);
  }

  function photosView(u) {
    const list = posts((p) => p.pic);
    return [heading("Photos")].concat(list.length ? list.map((i) => post(u, i)) : [note("No photos yet.")]);
  }

  function pagesView(u) {
    const list = posts((p) => p.page);
    return [heading("Pages")].concat(list.length ? list.map((i) => post(u, i)) : [note("No pages yet.")]);
  }

  function resultsView(u) {
    const r = find(u.q);
    return [panel("People", r.people.length ? r.people.map((c) => contact(u, c)) : [note("No people match.")])].concat(
      r.posts.length ? r.posts.map((i) => post(u, i)) : [note("No posts match.")]
    );
  }

  const VIEWS = { home: homeView, friends: friendsView, postal: postalView, notifications: notifyView, profile: profileView, saved: savedView, photos: photosView, pages: pagesView };

  function content(u) {
    return u.q.trim() ? resultsView(u) : VIEWS[u.view](u);
  }

  function paint(u) {
    u.who.replaceChildren(...(u.st.name ? [el("span", "eb-signed", "Signed in as " + u.st.name)] : [action("eb-btn", "Log in", () => login(u))]));
    u.main.replaceChildren(u.notice, ...content(u));
  }

  function topBar(u) {
    const bar = el("header", "eb-top");
    const logo = el("img", "eb-logo");
    logo.src = "brand/eyebook.png";
    logo.alt = "EYEbook";
    u.input = el("input", "eb-search");
    u.input.type = "search";
    u.input.placeholder = "Search EYEbook";
    u.input.setAttribute("aria-label", "Search EYEbook");
    u.input.addEventListener("input", () => {
      u.q = u.input.value;
      paint(u);
    });
    const links = el("div", "eb-icons");
    for (const [view, label] of TOP_LINKS) links.append(action("eb-ic", label, () => go(u, view)));
    bar.append(logo, u.input, links, u.who);
    return bar;
  }

  function sideNav(u) {
    const nav = el("nav", "eb-nav");
    nav.setAttribute("aria-label", "Shortcuts");
    for (const [view, label] of SHORTCUTS) nav.append(action("eb-nav-item", label, () => go(u, view)));
    return nav;
  }

  Apps.register("eyebook", {
    mount(slot, ctx, opts) {
      const page = web(slot, "EYEbook", "https://www.eyebook.com/", ctx.embedded);
      page.classList.add("eb-page");
      const notice = el("p", "eb-notice");
      notice.setAttribute("role", "alert");
      notice.hidden = true;
      const u = {
        ctx,
        visit: opts && opts.visit,
        st: { name: "", acts: [], status: [] },
        view: (opts && ROUTES[opts.path]) || "home",
        q: opts && opts.path === "/search" ? new URLSearchParams(opts.query || "").get("q") || "" : "",
        news: [],
        thread: -1,
        form: -1,
        ready: false,
        writable: false,
        warned: false,
        retrying: false,
        notice,
        who: el("div", "eb-login"),
        main: el("main", "eb-feed"),
        input: null,
      };
      const side = el("aside", "eb-rail");
      const sponsored = el("section", "eb-card-side");
      sponsored.append(el("h2", "eb-side-h", "Sponsored"), ...ADS.map((a) => ad(u, a)));
      const people = el("section", "eb-card-side");
      people.append(el("h2", "eb-side-h", "Contacts"), ...CONTACTS.map((c) => contact(u, c)));
      side.append(sponsored, people);
      const body = el("div", "eb-body");
      body.append(sideNav(u), u.main, side);
      page.append(topBar(u), body);
      u.input.value = u.q;
      paint(u);
      ctx.call("news.front", {}).then((r) => {
        u.news = r && Array.isArray(r.stories) ? r.stories.slice(0, NEWS_MAX) : [];
        paint(u);
      }, () => {});
      load(ctx).then((r) => {
        u.st = r.state;
        u.writable = r.ok;
        u.ready = true;
        if (!r.ok) warn(u, r.error || "read failed");
        paint(u);
      });
    },
  });
})();
