(function () {
  "use strict";
  const { el, button } = Apps;

  const HOST = "jobs.stalburg.ts";
  const NO_HOST = "Jobs works inside the game.";
  const APPLY_FAILED = "The application could not be sent.";
  const LEAD = "Paid work at the stations in the area you are in. Pay is per hour and is added while you work.";
  const LIMIT = "The game has no way to start a job yet, so Apply does not start your shift. It sends a reply to your Stalburg Mail inbox. Go to the station in person to start work.";
  const DUPES = "Stations with the same name, pay and area count as one job: applying for one marks them all as applied.";
  const EMPTY = "No paid work is listed near you right now. This list shows the paid jobs at the work stations in the area you are in. Go to a work station in the game and check again.";
  const NO_MATCH = "No job matches these filters.";
  const NO_SKILL = "No skill needed";
  const SENT = "Application sent. The reply is in your Stalburg Mail inbox.";
  const SORTS = [
    ["pay", "Pay, highest first"],
    ["pay_up", "Pay, lowest first"],
    ["name", "Name, A to Z"],
  ];
  const ORDER = {
    pay: (a, b) => b.oc - a.oc || b.rm - a.rm || a.name.localeCompare(b.name),
    pay_up: (a, b) => a.oc - b.oc || a.rm - b.rm || a.name.localeCompare(b.name),
    name: (a, b) => a.name.localeCompare(b.name) || b.oc - a.oc || b.rm - a.rm,
  };
  const SERVICES = [
    ["Stalburg Mail", "mail.stalburg.ts"],
    ["Stalburg Bank", "www.stalburgbank.ts"],
    ["Stal-Ex", "www.stalex.ts"],
  ];

  function pay(j) {
    const parts = [j.oc > 0 ? Apps.money(j.oc, "OC") : "", j.rm > 0 ? Apps.money(j.rm, "RM") : ""].filter(Boolean);
    return (parts.join(" and ") || Apps.money(0, "OC")) + " per hour";
  }

  function skillKey(j) {
    return j.skill || "none";
  }

  function fact(label, value) {
    const d = el("div", "jobs-fact");
    d.append(el("dt", "", label), el("dd", "", value));
    return d;
  }

  function labelled(text, input) {
    const w = el("label", "jobs-field");
    w.append(el("span", "", text), input);
    return w;
  }

  function options(sel, items) {
    sel.replaceChildren(
      ...items.map(([value, text]) => {
        const o = el("option", "", text);
        o.value = value;
        return o;
      })
    );
  }

  Apps.register("jobs", {
    mount(slot, ctx, opts) {
      const st = { jobs: null, skill: "", hard: "", sort: "pay" };

      const band = el("span", "bw-band");
      band.dataset.brand = "jobs";
      band.append(el("span", "bw-mark", "JOBS"));
      const bar = el("header", "bw-sitebar");
      bar.append(band, el("span", "bw-sitebar-name", "Jobs"), el("span", "bw-sitebar-host", HOST));

      const hero = el("section", "jobs-hero");
      hero.append(el("h2", "jobs-title", "Work in Stalburg"), el("p", "jobs-lead", LEAD), el("p", "jobs-warn", LIMIT), el("p", "jobs-warn", DUPES));

      const skill = el("select", "jobs-select");
      skill.addEventListener("change", () => {
        st.skill = skill.value;
        render();
      });
      const hard = el("select", "jobs-select");
      hard.addEventListener("change", () => {
        st.hard = hard.value;
        render();
      });
      const sort = el("select", "jobs-select");
      options(sort, SORTS);
      sort.addEventListener("change", () => {
        st.sort = sort.value;
        render();
      });
      const count = el("p", "jobs-count");
      const controls = el("div", "jobs-controls");
      controls.append(labelled("Skill", skill), labelled("Hardness", hard), labelled("Sort", sort));

      const notice = el("p", "jobs-notice");
      notice.setAttribute("role", "status");
      notice.hidden = true;
      const list = el("ul", "jobs-list");

      const foot = el("footer", "bw-foot");
      const links = el("ul", "bw-foot-list");
      links.append(
        ...SERVICES.map(([label, host]) => {
          const li = el("li");
          const b = button("bw-foot-link", label);
          b.addEventListener("click", () => opts.visit && opts.visit(host));
          li.append(b);
          return li;
        })
      );
      foot.append(el("h4", "bw-foot-title", "Services"), links, el("p", "bw-copy", "Jobs. Stalburg, Stolland."));

      const root = el("div", "bw bw-site jobs");
      root.append(bar, hero, controls, count, notice, list, foot);
      slot.append(root);

      function say(text) {
        notice.textContent = text || "";
        notice.hidden = !text;
      }

      function keep(sel, value) {
        sel.value = [...sel.options].some((o) => o.value === value) ? value : "";
        return sel.value;
      }

      function fill(jobs) {
        const skills = [...new Set(jobs.map(skillKey))];
        options(skill, [["", "Any skill"], ...skills.map((k) => [k, k === "none" ? NO_SKILL : k])]);
        const hards = [...new Set(jobs.map((j) => j.hardness))].sort((a, b) => a - b);
        options(hard, [["", "Any hardness"], ...hards.map((h) => [String(h), "Hardness " + h])]);
        st.skill = keep(skill, st.skill);
        st.hard = keep(hard, st.hard);
      }

      function sameJob(a, b) {
        return a.name === b.name && a.place === b.place && a.oc === b.oc && a.rm === b.rm;
      }

      function visible() {
        return st.jobs
          .filter((j) => (!st.skill || skillKey(j) === st.skill) && (!st.hard || String(j.hardness) === st.hard))
          .sort(ORDER[st.sort]);
      }

      function apply(j, send) {
        send.disabled = true;
        say("");
        ctx.call("jobs.apply", { name: j.name, place: j.place, oc: j.oc, rm: j.rm }).then(
          () => {
            for (const x of st.jobs) if (sameJob(x, j)) x.applied = true;
            say(SENT + " Go to " + j.name + " in person to start work.");
            render();
          },
          (e) => {
            say((e && e.message) || APPLY_FAILED);
            load();
          }
        );
      }

      function row(j) {
        const li = el("li", "jobs-row");
        const main = el("div", "jobs-main");
        main.append(el("h3", "jobs-name", j.name));
        const facts = el("dl", "jobs-facts");
        facts.append(fact("Pay", pay(j)), fact("Hardness", String(j.hardness)), fact("Skill", j.skill || NO_SKILL), fact("Place", j.place));
        main.append(facts);
        const act = el("div", "jobs-act");
        if (j.applied) act.append(el("span", "jobs-applied", "Applied"));
        else {
          const send = button("bw-go", "Apply");
          send.addEventListener("click", () => apply(j, send));
          act.append(send);
        }
        li.append(main, act);
        return li;
      }

      function render() {
        if (!st.jobs) {
          count.textContent = "";
          list.replaceChildren(el("li", "jobs-empty", NO_HOST));
          return;
        }
        const shown = visible();
        count.textContent = st.jobs.length
          ? shown.length === st.jobs.length
            ? `${st.jobs.length} paid ${st.jobs.length === 1 ? "job" : "jobs"} listed near you`
            : `${shown.length} of ${st.jobs.length} paid jobs shown`
          : "";
        if (!st.jobs.length) list.replaceChildren(el("li", "jobs-empty", EMPTY));
        else if (!shown.length) list.replaceChildren(el("li", "jobs-empty", NO_MATCH));
        else list.replaceChildren(...shown.map(row));
      }

      function load() {
        ctx.call("jobs.list", {}).then(
          (r) => {
            st.jobs = r && Array.isArray(r.jobs) ? r.jobs : [];
            fill(st.jobs);
            render();
          },
          (e) => {
            st.jobs = null;
            if (ctx.live) say((e && e.message) || APPLY_FAILED);
            render();
          }
        );
      }

      load();
    },
  });
})();
