(function () {
  if (!("serviceWorker" in navigator)) return;

  window.addEventListener("load", function () {
    navigator.serviceWorker.register("/service-worker.js").catch(function () {
      // Some browser modes block service workers; install metadata still applies.
    });
  });
})();
