(function () {
  "use strict";
  const { el, button, money } = Apps;
  const { HOME } = BrowserSites;
  const NO_HOST = "This site works inside the game.";
  const BUSY = "The game is saving, try again in a moment.";
  const FAILED = "The server did not answer. Try again.";
  const BOUGHT = "Order accepted. PC Master Guard is on your desktop.";
  const INSTALLED = "Already installed. PC Master Guard is on your desktop.";
  const OWNED = "You already own PC Master Guard.";
  const NEWS = "NEW!!! Cheaper than ever!!! Ask our staff about your PC!!!";
  const PERKS = [
    "Real-time protection: stops viruses before they land",
    "Full scan with one click",
    "Quarantine, and puts your files back",
    "Shows every OC and RM a virus stole",
  ];
  const CATALOGUE_NOTE = "Catalogue only. PC Master Guard is the one product you can order online.";
  const SIDE = [
    ["Desktops", [["Stalburg Home 2001", "Beige tower, floppy drive, 40 MB disk", 1290], ["Office Korso", "Grey tower for offices, quiet fan", 1790], ["Mine Edition", "Dust filter for damp tunnels", 1490]]],
    ["Laptops", [["Kolhola Portable", "14 inch screen, battery for one shift", 2190], ["Traveller 2", "Heavy and warm, and says so on the box", 1990]]],
    ["Parts", [["RAM 64 MB", "Fits most towers, still most towers", 89], ["Disk 4 GB", "Hums in tune with the water pipes", 240], ["LagTel modem", "Connects to the internet, slowly", 65]]],
    ["Software", [["Notepad Plus", "Writes text, saves to floppy only", 35], ["Tramtimes 2001", "Timetable for the 7 tram, updated when it runs", 49], ["Solitaire Deluxe", "Included free with every desktop", 0]]],
  ];

  function errorText(e) {
    if (e && e.message === "No game host") return NO_HOST;
    if (e && e.error === "busy") return BUSY;
    return (e && e.message) || FAILED;
  }

  function newCid() {
    return "pcm-" + Date.now().toString(36) + "-" + Math.random().toString(36).slice(2, 10);
  }

  function catalogue(cat) {
    const page = el("div", "avs-cat-page");
    page.append(el("h2", "avs-h", cat[0]), el("p", "avs-cat-note", CATALOGUE_NOTE));
    for (const [name, spec, price] of cat[1]) {
      const row = el("div", "avs-cat-row");
      row.append(el("span", "avs-cat-name", name), el("span", "avs-cat-spec", spec), el("span", "avs-cat-price", price ? money(price, "RM") : "Free"));
      page.append(row);
    }
    return page;
  }

  function store(slot, ctx, opts) {
    const page = el("div", "avs");
    const cid = newCid();
    const wallet = el("p", "avs-wallet");
    const buy = button("avs-buy", "BUY NOW");
    const out = el("p", "avs-out");
    out.setAttribute("role", "status");

    function paint(s, message) {
      wallet.textContent = "Your wallet: " + money(s.walletRm, "RM");
      burst.textContent = money(s.priceRm, "RM");
      buy.disabled = s.owned;
      buy.textContent = s.owned ? "ALREADY INSTALLED" : "BUY NOW";
      out.textContent = message || (s.owned ? OWNED : "");
    }

    buy.addEventListener("click", () => {
      buy.disabled = true;
      out.textContent = "";
      ctx.call("av.buy", { cid }).then((s) => paint(s, s.known ? INSTALLED : BOUGHT), (e) => {
        buy.disabled = false;
        out.textContent = errorText(e);
      });
    });
    ctx.call("av.state", {}).then((s) => paint(s), (e) => {
      out.textContent = errorText(e);
    });

    const head = el("header", "avs-head");
    head.append(el("h1", "avs-logo", "PC MASTER"), el("p", "avs-tag", "Computers, parts and software for all of Stolland."));

    const box = el("div", "avs-box");
    box.append(el("span", "avs-shield"), el("span", "avs-box-name", "PC MASTER GUARD 3.0"), el("span", "avs-box-sub", "The real antivirus"));
    const burst = el("div", "avs-burst");
    const product = el("div", "avs-product");
    product.append(box, burst);

    const list = el("ul", "avs-list");
    for (const perk of PERKS) list.append(el("li", "", perk));
    const copy = el("div", "avs-copy");
    copy.append(el("h2", "avs-h", "PC Master Guard 3.0"), el("p", "", "One payment. No subscription. It keeps running for as long as you own your computer."), list, buy, out, wallet);

    const main = el("div", "avs-main");
    const secure = el("p", "avs-secure", "Secure order. Payment is taken from your wallet.");
    const shelf = [];
    function show(name) {
      const cat = SIDE.find((c) => c[0] === name);
      for (const b of shelf) b.classList.toggle("avs-here", b.textContent === name);
      main.replaceChildren(...(cat ? [catalogue(cat)] : [product, copy, secure]));
    }
    const side = el("div", "avs-side");
    side.append(el("p", "avs-side-title", "Our shop"));
    for (const name of SIDE.map((c) => c[0]).concat(["Security"])) {
      const b = button("avs-cat", name);
      b.addEventListener("click", () => show(name));
      shelf.push(b);
      side.append(b);
    }
    show("Security");

    const foot = el("footer", "avs-foot");
    const back = button("avs-link", "Back to Stalburg Web");
    back.addEventListener("click", () => opts.visit(HOME));
    foot.append(back, el("span", "", " | PC Master. Prices in RM include tax."));

    const cols = el("div", "avs-cols");
    cols.append(side, main);
    page.append(head, el("div", "avs-news", NEWS), cols, foot);
    slot.append(page);
  }

  const TICK_MS = 1000;
  const COUNT_START = 4582311;
  const AWARDS = ["BEST ANTIVIRUS 2001", "5 STARS!!!!!", "TOP PICK", "100% DETECTION!!!", "SAFEST PC AWARD"];
  const QUOTES = [
    "Before TurvaPro my PC was slow. Now it is slow AND protected!!! - A happy customer, Kolhola",
    "I was told I had 14 000 viruses. TurvaPro removed them all for 99 OC! - A happy customer, Ershfors",
    "My grandson said I should not buy this. He is wrong. - A happy customer, Stalburg",
  ];
  const LABEL = { active: "PROTECTED. THANK YOU!", lapsed: "PAY NOW: 99 OC", ended: "SUBSCRIBE AGAIN: 99 OC", none: "SUBSCRIBE NOW: 99 OC A WEEK" };
  const STATUS = {
    active: "Your PC is protected. Thank you for choosing TurvaPro 2000!!!",
    lapsed: "YOUR PROTECTION HAS EXPIRED. Pay now or your PC is at risk!!!",
    ended: "Your subscription has ended. Subscribe again to protect your PC!!!",
    none: "",
  };
  const SUBSCRIBED = "Thank you! Your PC is protected. A receipt is on its way to your mail.";

  function fmt(n) {
    return String(n).replace(/\B(?=(\d{3})+(?!\d))/g, " ");
  }

  function turva(slot, ctx, opts) {
    const page = el("div", "tvs");
    const cid = newCid();
    const count = el("span", "tvs-count", fmt(COUNT_START));
    const wallet = el("p", "tvs-wallet");
    const notice = el("p", "tvs-out");
    notice.setAttribute("role", "status");
    const scanOut = el("p", "tvs-scan");
    scanOut.setAttribute("role", "status");
    const freeBtn = button("tvs-free", "FREE ONLINE SCAN");
    const subBtn = button("tvs-sub", LABEL.none);

    function paint(s, message) {
      wallet.textContent = "Your wallet: " + money(s.walletOc, "OC");
      subBtn.disabled = s.status === "active";
      subBtn.textContent = LABEL[s.status] || LABEL.none;
      notice.textContent = message || STATUS[s.status] || "";
    }

    subBtn.addEventListener("click", () => {
      subBtn.disabled = true;
      notice.textContent = "";
      ctx.call("av.buy", { cid }).then((s) => paint(s, SUBSCRIBED), (e) => {
        subBtn.disabled = false;
        notice.textContent = errorText(e);
      });
    });

    freeBtn.addEventListener("click", () => {
      freeBtn.disabled = true;
      scanOut.textContent = "Scanning your PC...";
      ctx.call("av.freescan", {}).then((r) => {
        scanOut.textContent = "Scan complete: " + r.threats + " threats found!!! Subscribe now to remove them.";
      }, (e) => {
        scanOut.textContent = errorText(e);
      }).then(() => {
        freeBtn.disabled = false;
      });
    });

    let total = COUNT_START;
    function bump() {
      if (!count.isConnected) return;
      total += 1 + Math.floor(Math.random() * 40);
      count.textContent = fmt(total);
      setTimeout(bump, TICK_MS);
    }
    setTimeout(bump, TICK_MS);

    const ticker = el("div", "tvs-ticker");
    ticker.append(el("span", "", "WARNING!!! 14 000 VIRUSES FOUND ON THE INTERNET TODAY!!! DO NOT SURF WITHOUT PROTECTION!!! WARNING!!! "));
    const head = el("header", "tvs-head");
    head.append(el("h1", "tvs-logo", "#1 ANTIVIRUS!!"), el("p", "tvs-brand", "TurvaPro 2000"), el("p", "tvs-tag", "Trusted by 9 out of 10 PCs in Stolland!!!"));
    const live = el("p", "tvs-live", "Threats found on the Internet today: ");
    live.append(count);
    const awards = el("div", "tvs-awards");
    for (const a of AWARDS) awards.append(el("div", "tvs-award", a));
    const scanBox = el("div", "tvs-scanbox");
    scanBox.append(el("h2", "tvs-h", "Is your PC infected?"), freeBtn, scanOut);
    const offer = el("div", "tvs-offer");
    offer.append(el("h2", "tvs-h", "TODAY ONLY!!!"), el("p", "tvs-price", "99 OC a week"), el("p", "", "Full protection. Cancel any time!!!"), subBtn, notice, wallet);
    const quotes = el("div", "tvs-quotes");
    for (const q of QUOTES) quotes.append(el("p", "tvs-quote", q));
    const foot = el("footer", "tvs-foot");
    const back = button("tvs-link", "Back to Stalburg Web");
    back.addEventListener("click", () => opts.visit(HOME));
    foot.append(back, el("span", "", " | (c) 2001 TurvaPro. All rights reserved. Prices include VAT."));

    page.append(ticker, head, el("p", "tvs-blink", "YOUR PC MAY BE AT RISK!!!"), live, awards, scanBox, offer, quotes, foot);
    slot.append(page);
    ctx.call("av.state", {}).then((s) => paint(s), (e) => {
      notice.textContent = errorText(e);
    });
  }

  Apps.register("turvasite", { mount: turva });
  Apps.register("pcmaster", { mount: store });
})();
