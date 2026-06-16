(function () {
  if (!("serviceWorker" in navigator)) return;

  var workerUrl = "/service-worker.js?v=5";

  window.addEventListener("load", function () {
    navigator.serviceWorker
      .getRegistrations()
      .then(function (registrations) {
        return Promise.all(
          registrations.map(function (registration) {
            if (!registration.active || !registration.active.scriptURL.endsWith(workerUrl)) {
              return registration.unregister().catch(function () {});
            }
          })
        );
      })
      .then(function () {
        return navigator.serviceWorker.register(workerUrl);
      })
      .then(function (registration) {
        registration.update().catch(function () {});
      })
      .catch(function () {
        // Some browser modes block service workers; install metadata still applies.
      });
  });
})();
