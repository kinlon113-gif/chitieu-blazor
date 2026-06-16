(function () {
  if (!("serviceWorker" in navigator)) return;

  window.addEventListener("load", function () {
    navigator.serviceWorker
      .register("/service-worker.js?v=4")
      .then(function (registration) {
        registration.update().catch(function () {});
      })
      .catch(function () {
        // Some browser modes block service workers; install metadata still applies.
      });
  });
})();
