// U-PMS browser helpers. Loaded as a classic script from this site only (CSP script-src 'self').
(() => {
  'use strict';
  const upms = (window.upms = window.upms || {});

  // Native <dialog>: showModal gives focus containment, Esc and an inert page (research R3, R20).
  upms.dialog = {
    open(dialog, dotnetRef) {
      if (!dialog || dialog.open) return;
      const returnFocus = document.activeElement;
      // In a dialog marked data-escape-guard (the task drawer), Esc does not close the dialog while the
      // focused text field holds typed text, so the text is not lost; the field's own Esc handling still runs.
      const onCancel = (e) => {
        const active = document.activeElement;
        const typing = active && dialog.contains(active) && active.closest('dialog') === dialog &&
          (active.tagName === 'TEXTAREA' || (active.tagName === 'INPUT' && active.type === 'text')) &&
          active.value.trim() !== '';
        if (typing && dialog.hasAttribute('data-escape-guard')) e.preventDefault();
      };
      dialog.addEventListener('cancel', onCancel);
      dialog.addEventListener('close', function onClose() {
        dialog.removeEventListener('close', onClose);
        dialog.removeEventListener('cancel', onCancel);
        if (returnFocus && returnFocus.isConnected && typeof returnFocus.focus === 'function') returnFocus.focus();
        if (dotnetRef) dotnetRef.invokeMethodAsync('OnDialogClosed').catch(() => {});
      });
      dialog.showModal();
    },
    close(dialog) {
      if (dialog && dialog.open) dialog.close();
    }
  };

  upms.clipboard = {
    async copy(text) {
      try {
        await navigator.clipboard.writeText(text);
        return true;
      } catch {
        return false;
      }
    }
  };

  upms.focusById = (id) => {
    const el = document.getElementById(id);
    if (el) el.focus();
  };

  // Password fields (OWASP ASVS 2.1.8, 2.1.12): "Show password" reveals what was typed, and new passwords get a
  // strength hint. Listeners are on the document, so pages loaded by enhanced navigation work too.
  document.addEventListener('click', (e) => {
    const toggle = e.target && e.target.closest ? e.target.closest('[data-password-toggle]') : null;
    const input = toggle && document.getElementById(toggle.getAttribute('data-password-toggle'));
    if (!input) return;
    const show = input.type === 'password';
    input.type = show ? 'text' : 'password';
    toggle.setAttribute('aria-pressed', show ? 'true' : 'false');
  });

  upms.passwordStrength = (value) => {
    if (!value) return '';
    if (value.length < 12) return 'Too short: use at least 12 characters.';
    if (new Set(value.toLowerCase()).size <= 3) return 'Weak: too repetitive.';
    const kinds = [/[a-z]/, /[A-Z]/, /[0-9]/, /[^A-Za-z0-9]/].filter((r) => r.test(value)).length;
    const words = value.trim().split(/\s+/).length;
    if (value.length >= 16 && (words >= 3 || kinds >= 3)) return 'Strong.';
    return 'Fair: a few more words make it stronger.';
  };

  document.addEventListener('input', (e) => {
    const input = e.target;
    if (!input || !input.id || !window.CSS) return;
    const hint = document.querySelector('[data-password-strength="' + CSS.escape(input.id) + '"]');
    if (!hint) return;
    const text = upms.passwordStrength(input.value);
    if (hint.textContent !== text) hint.textContent = text; // only changes are announced
  });

  // Firefox starts a drag only when drag data is set, which Blazor cannot do from .NET (research R15).
  document.addEventListener('dragstart', (e) => {
    const source = e.target && e.target.closest ? e.target.closest('[data-drag-key]') : null;
    if (source && e.dataTransfer) {
      e.dataTransfer.setData('text/plain', source.getAttribute('data-drag-key'));
      e.dataTransfer.effectAllowed = 'move';
    }
  });
})();
