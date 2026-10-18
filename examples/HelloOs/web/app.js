(function () {
  "use strict";
  var theme = document.getElementById("theme");
  var hour = document.getElementById("hour");
  var greeting = document.getElementById("greeting");

  document.getElementById("greet").addEventListener("click", function () {
    os.call("greet", {}).then(function (res) {
      greeting.textContent = res.text;
    }, function (err) {
      greeting.textContent = err.message || err.error;
    });
  });

  os.ready.then(function (r) {
    theme.textContent = r.theme;
    os.window.title("Hello");
    os.window.size(420, 300);
  });

  os.on("theme", function (mode) {
    theme.textContent = mode;
  });

  os.on("hour", function (data) {
    hour.textContent = String(data.hour);
  });
})();
