(function () {
  "use strict";
  const { el, button } = Apps;
  const { HOME } = BrowserSites;
  const FREERM = "www.free-rm.ts";
  const WAREZ = "www.kolhola-warez.ts";
  const VOITTAJA = "www.voittaja.ts";
  const PROGRESS_MS = 3000;
  const TICK_MS = 1000;
  const VISITORS_START = 4817;
  const ONLINE_START = 1337;
  const ONLINE_MIN = 900;
  const NO_HOST = "This site works inside the game.";
  const BUSY = "The game is saving, try again in a moment.";
  const FAILED = "The server did not answer. Try again.";
  const TOP = [
    ["stalex-insider", "Stal-Ex Insider 2.0", "12,8 MB", "3 519"],
    ["naspo-autobet", "NaSpoBet autobet bot", "0,6 MB", "2 376"],
    ["lotto-predictor", "LOTTO number predictor (100 % working)", "0,3 MB", "1 589"],
    ["minesweeper-gold", "Minesweeper GOLD crack", "0,4 MB", "1 204"],
    ["zigzag-bot", "Zigzag views bot 2001", "0,9 MB", "1 031"],
    ["solitaire-keygen", "Solitaire Unlimited keygen", "0,1 MB", "887"],
    ["postal-spy", "Postal Messenger spy 1.3", "2,2 MB", "741"],
    ["eyebook-finder", "EYEbook password finder", "1,1 MB", "652"],
    ["torchat-skin", "TorChat turnip skin pack", "4,7 MB", "418"],
    ["stalmail-sender", "Stalburg Mail sender PRO", "1,8 MB", "298"],
  ];
  const TESTIMONIALS = [
    "I generated 50 000 RM in one night!!! My wife does not know yet. - Pertti, Koksburg",
    "Gave my bank card to the generator. Nothing happened, which is how I know it works!!! - Ilkka, Ershfors",
    "Got a virus from the generator but it was only the FREE kind. 5 stars!!! - Mirjam, Tarfell",
  ];
  const SMALL_PRINT = [
    "The prize is paid in OC at the Money Exchange rate of the day, minus a 40 % handling fee and a 15 % fee for the Tax Office of Stalburg (not a real office).",
    "Pressing NO THANKS also claims the prize. This is a feature.",
    "The prize is not available in Ershfors, Koksburg or on Tuesdays.",
    "Winners must be 18 years or older and hold a mail account at any .ts address.",
    "VOITTAJA! is not affiliated with the Monetary Bank of Stalburg, NaSpoBet or any other bank or betting company.",
    "This offer ends today. Prize offers end today. Prize offers end today.",
  ];
  const LINKS = [
    ["Stalburg Web", HOME],
    ["FREE RM GENERATOR", FREERM],
    ["Kolhola Warez", WAREZ],
    ["VOITTAJA!", VOITTAJA],
  ];

  function errorText(e) {
    if (e && e.message === "No game host") return NO_HOST;
    if (e && e.error === "busy") return BUSY;
    return (e && e.message) || FAILED;
  }

  function downloaded(r) {
    return r.outcome === "blocked" ? "Download blocked. Your security software stopped " + r.file + "." : "Download complete: " + r.file + ".";
  }

  function claimed(r) {
    return r.outcome === "blocked" ? "Claim blocked. Your security software stopped the prize." : "Congratulations! Your prize has been claimed.";
  }

  function run(ctx, method, args, btn, out, describe) {
    btn.disabled = true;
    out.textContent = "";
    return ctx.call(method, args)
      .then((r) => {
        out.textContent = describe(r);
      }, (e) => {
        out.textContent = errorText(e);
      })
      .then(() => {
        btn.disabled = false;
      });
  }

  function footer(opts, here, copy) {
    const foot = el("footer", "sh-foot");
    for (const [label, host] of LINKS) {
      if (host === here) continue;
      const b = button("sh-link", label);
      b.addEventListener("click", () => opts.visit(host));
      foot.append(b);
    }
    foot.append(el("p", "sh-copy", copy));
    return foot;
  }

  function ticker(text) {
    const band = el("div", "sh-ticker");
    band.append(el("span", "", text));
    return band;
  }

  function fmt(n) {
    return String(n).replace(/\B(?=(\d{3})+(?!\d))/g, " ");
  }

  function counter(start, next) {
    const span = el("span", "sh-count", fmt(start));
    let total = start;
    function tick() {
      if (!span.isConnected) return;
      total = next(total);
      span.textContent = fmt(total);
      setTimeout(tick, TICK_MS);
    }
    setTimeout(tick, TICK_MS);
    return span;
  }

  function freeRm(slot, ctx, opts) {
    const page = el("div", "sh sh-freerm");
    const flash = el("div", "sh-flash");
    flash.append(el("span", "sh-blink", "*** FREE *** FREE *** NO VIRUS *** FREE ***"));
    const bar = el("div", "sh-bar");
    bar.append(el("div", "sh-bar-fill"));
    const generate = button("sh-btn", "GENERATE 1 000 RM");
    const get = button("sh-btn sh-get", "Download generator.exe");
    get.hidden = true;
    const out = el("p", "sh-out");
    out.setAttribute("role", "status");
    generate.addEventListener("click", () => {
      generate.disabled = true;
      bar.classList.add("run");
      setTimeout(() => {
        get.hidden = false;
      }, PROGRESS_MS);
    });
    get.addEventListener("click", () => run(ctx, "virus.download", { item: "generator" }, get, out, downloaded));
    const online = counter(ONLINE_START, (n) => Math.max(ONLINE_MIN, n + Math.floor(Math.random() * 21) - 10));
    const users = el("p", "sh-online", "Users online now: ");
    users.append(online);
    const reviews = el("div", "sh-quotes");
    reviews.append(el("h2", "sh-h", "WHAT OUR CUSTOMERS SAY!!!"));
    for (const q of TESTIMONIALS) reviews.append(el("p", "sh-quote", q));
    page.append(
      flash,
      el("h1", "sh-title", "FREE RM GENERATOR"),
      ticker("Works with every bank in Stolland!!! Fast, safe and FREE!!!"),
      users,
      el("p", "sh-lead", "Generate 1 000 RM in seconds!!! 100 % working!!!"),
      bar,
      generate,
      get,
      out,
      reviews,
      footer(opts, FREERM, "FREE RM GENERATOR. Not affiliated with any bank.")
    );
    slot.append(page);
  }

  function warez(slot, ctx, opts) {
    const page = el("div", "sh sh-warez");
    const head = el("header", "sh-warez-head");
    head.append(el("h1", "sh-warez-title", "KOLHOLA WAREZ"), el("p", "sh-warez-tag", "Cracked games and tools. Fast servers!!!"));
    const table = el("table", "sh-table");
    const heads = el("tr");
    heads.append(el("th", "", "#"), el("th", "", "Name"), el("th", "", "Size"), el("th", "", "Downloads"), el("th", "", ""), el("th", "", "Status"));
    table.append(heads);
    TOP.forEach(([item, name, size, count], i) => {
      const row = el("tr");
      const act = el("td");
      const out = el("td", "sh-cell");
      const get = button("sh-btn sh-get", "Download");
      get.addEventListener("click", () => run(ctx, "virus.download", { item }, get, out, downloaded));
      act.append(get);
      row.append(el("td", "", String(i + 1)), el("td", "sh-name", name), el("td", "", size), el("td", "", count), act, out);
      table.append(row);
    });
    const visits = el("p", "sh-visits", "You are visitor number ");
    visits.append(counter(VISITORS_START, (n) => n + 1 + Math.floor(Math.random() * 3)), ". Best viewed in 800 x 600.");
    page.append(
      head,
      ticker("New cracks added daily!!! Fast servers!!! Scan everything twice!!!"),
      visits,
      el("h2", "sh-top", "TOP 10 DOWNLOADS"),
      table,
      el("p", "sh-note", "Every download is 100 % free!!! No registration needed!!!"),
      footer(opts, WAREZ, "Kolhola Warez. Please scan everything twice!!!")
    );
    slot.append(page);
  }

  function voittaja(slot, ctx, opts) {
    const page = el("div", "sh sh-voittaja");
    const main = el("div");
    const terms = el("div", "sh-terms");
    terms.hidden = true;
    const claim = button("sh-btn sh-claim", "CLAIM PRIZE");
    const out = el("p", "sh-out");
    out.setAttribute("role", "status");
    const rules = button("sh-rules", "Prize rules and small print");
    const decoy = button("sh-decoy", "No thanks, I do not want my 5 000 RM");
    const decoyOut = el("p", "sh-out");
    decoyOut.setAttribute("role", "status");
    const back = button("sh-link", "Back to the prize page");
    claim.addEventListener("click", () => run(ctx, "virus.claim", {}, claim, out, claimed));
    decoy.addEventListener("click", () => run(ctx, "virus.claim", {}, decoy, decoyOut, claimed));
    rules.addEventListener("click", () => {
      main.hidden = true;
      terms.hidden = false;
    });
    back.addEventListener("click", () => {
      terms.hidden = true;
      main.hidden = false;
    });
    const small = el("ul", "sh-small");
    for (const line of SMALL_PRINT) small.append(el("li", "", line));
    main.append(
      el("div", "sh-wheel"),
      el("h1", "sh-big", "VOITTAJA!"),
      ticker("CONGRATULATIONS!!! You are our 1 000 000th visitor!!! Spin to win!!!"),
      el("p", "sh-lead", "You are our 1 000 000th visitor and you have WON a prize!!!"),
      el("p", "sh-prize", "5 000 RM"),
      claim,
      out,
      rules
    );
    terms.append(el("h2", "sh-h", "HOW THE PRIZE WORKS"), small, decoy, decoyOut, back);
    page.append(main, terms, footer(opts, VOITTAJA, "VOITTAJA! Lucky visitor. Prize offers end today."));
    slot.append(page);
  }

  Apps.register("freerm", { mount: freeRm });
  Apps.register("warez", { mount: warez });
  Apps.register("voittaja", { mount: voittaja });
})();
