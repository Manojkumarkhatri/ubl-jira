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

  // Firefox starts a drag only when drag data is set, which Blazor cannot do from .NET (research R15).
  document.addEventListener('dragstart', (e) => {
    const source = e.target && e.target.closest ? e.target.closest('[data-drag-key]') : null;
    if (source && e.dataTransfer) {
      e.dataTransfer.setData('text/plain', source.getAttribute('data-drag-key'));
      e.dataTransfer.effectAllowed = 'move';
    }
  });
})();
