(function () {
  "use strict";
  Apps.register("wiki", {
    mount(slot) {
      const root = Apps.el("div", "wk");
      root.append(Apps.el("p", "wk-note", "Offline wiki articles are not installed yet."));
      slot.append(root);
    },
  });
})();
