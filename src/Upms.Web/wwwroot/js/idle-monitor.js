// Idle session support (FR-006). User input (not the page's own traffic) is reported to the server at
// most once a minute through POST /account/keepalive, which restarts the session's idle clock and renews
// the cookie. The server decides when to warn and when the session ends; expire() posts the sign-out form.
(() => {
  'use strict';
  const KEEPALIVE_URL = 'account/keepalive';
  const MIN_INTERVAL_MS = 60 * 1000;
  let lastActivity = 0;
  let lastSent = Date.now();

  const hasSession = () => document.querySelector('[data-upms-session]') !== null;

  async function send() {
    lastSent = Date.now();
    try {
      const response = await fetch(KEEPALIVE_URL, {
        method: 'POST',
        credentials: 'same-origin',
        headers: { 'X-Upms-Request': 'keepalive' }
      });
      return response.status === 204;
    } catch {
      return false;
    }
  }

  function maybeSend() {
    if (hasSession() && lastActivity > lastSent && Date.now() - lastSent >= MIN_INTERVAL_MS) {
      send();
    }
  }

  function markActivity() {
    lastActivity = Date.now();
    maybeSend();
  }

  for (const type of ['pointerdown', 'keydown', 'wheel', 'touchstart', 'input']) {
    document.addEventListener(type, markActivity, { passive: true, capture: true });
  }
  setInterval(maybeSend, 15 * 1000);

  window.upmsIdle = {
    keepAlive: () => send(),
    expire() {
      const form = document.getElementById('upms-logout-form');
      if (!form) {
        window.location.href = 'Account/Login?expired=1';
        return;
      }
      const returnUrl = form.querySelector('input[name="returnUrl"]');
      if (returnUrl) returnUrl.value = 'Account/Login?expired=1';
      form.submit();
    }
  };
})();
