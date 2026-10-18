(function () {
  "use strict";

  const POLL_MS = 400;
  const POLL_LIMIT_MS = 180000;
  const SEARCH_LIMIT = 10;
  const TITLE_LIMIT = 50;
  const FILE_RE = /^pages\/[0-9a-f]{40}\.html$/;
  const NO_RESPONSE = "No response from game host";
  const STOPPED = { error: "stopped", message: "" };
  const TIMEOUT = { error: "timeout", message: "" };
  const BAD = { error: "bad_response", message: "" };

  function wait(ms) {
    return new Promise((resolve) => setTimeout(resolve, ms));
  }

  function create(ctx, alive) {
    function call(method, args) {
      return ctx.call(method, args).catch((e) => {
        throw e && e.message === NO_RESPONSE ? TIMEOUT : e;
      });
    }

    function poll(job, started) {
      if (!alive()) return Promise.reject(STOPPED);
      if (Date.now() - started > POLL_LIMIT_MS) return Promise.reject(TIMEOUT);
      return wait(POLL_MS)
        .then(() => call("wiki.poll", { job }))
        .then((r) => (r && r.state === "pending" ? poll(job, started) : r));
    }

    function settle(r) {
      return r && r.state === "pending" ? poll(r.job, Date.now()) : Promise.resolve(r);
    }

    function page(title) {
      return call("wiki.page", { title }).then(settle).then((p) => {
        if (!p || p.state !== "ready" || typeof p.title !== "string" || typeof p.file !== "string" || !FILE_RE.test(p.file)) throw BAD;
        return p;
      });
    }

    function titles(offset) {
      const args = { limit: TITLE_LIMIT };
      if (offset) args.offset = offset;
      return call("wiki.titles", args).then((r) => {
        if (!r || !Array.isArray(r.items)) throw BAD;
        return r;
      });
    }

    function search(q, offset) {
      const args = { q, limit: SEARCH_LIMIT };
      if (offset) args.offset = offset;
      return call("wiki.search", args).then(settle).then((r) => {
        if (!r || !Array.isArray(r.items)) throw BAD;
        return r;
      });
    }

    return {
      status: () => call("wiki.status"),
      consent: (allow) => call("wiki.consent", { allow: allow === true }),
      page,
      titles,
      search,
      bulk: (action, images) => call("wiki.bulk", { action, images: images === true }),
      clear: () => call("wiki.clear"),
    };
  }

  window.WikiNet = { create };
})();
