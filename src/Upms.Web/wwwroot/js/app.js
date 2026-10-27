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
        // Esc in an open menu inside the dialog (the item's "⋯" menu) closes only the menu; the menu handles the key.
        if (dialog.querySelector(':scope .menu-backdrop')) {
          e.preventDefault();
          return;
        }
        // A guarded dialog (the Create form) asks its owner, which confirms before typed text is lost.
        if (dialog.hasAttribute('data-close-guard')) {
          e.preventDefault();
          if (dotnetRef) dotnetRef.invokeMethodAsync('OnDialogCloseRequested').catch(() => {});
          return;
        }
        const active = document.activeElement;
        const typing = active && dialog.contains(active) && active.closest('dialog') === dialog &&
          (active.tagName === 'TEXTAREA' || (active.tagName === 'INPUT' && active.type === 'text') || active.isContentEditable) &&
          (active.isContentEditable ? active.textContent : active.value).trim() !== '';
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
        // Older browsers and pages without clipboard permission: copy through a selected, unseen text field.
        const area = document.createElement('textarea');
        area.value = text;
        area.setAttribute('readonly', '');
        area.style.position = 'fixed';
        area.style.opacity = '0';
        document.body.appendChild(area);
        area.select();
        let copied = false;
        try {
          copied = document.execCommand('copy');
        } catch {
          copied = false;
        }
        area.remove();
        return copied;
      }
    }
  };

  // A space's Share button copies the page's address while the click still counts as the person's own action, then says
  // so in the note beside it (a live region).
  document.addEventListener('click', async (e) => {
    const button = e.target && e.target.closest ? e.target.closest('[data-copy-link]') : null;
    if (!button) return;
    const note = button.parentElement && button.parentElement.querySelector('.share-note');
    const copied = await upms.clipboard.copy(window.location.href);
    if (!note) return;
    note.textContent = copied ? 'Link copied' : 'The link could not be copied';
    clearTimeout(note.upmsTimer);
    note.upmsTimer = setTimeout(() => { note.textContent = ''; }, 2500);
  });

  // Per-viewer choices such as the List's columns; storage can be off (private windows), so failures are ignored.
  upms.store = {
    get(key) {
      try { return window.localStorage.getItem(key); } catch { return null; }
    },
    set(key, value) {
      try { window.localStorage.setItem(key, value); } catch { /* not stored */ }
    }
  };

  // Jira's "Enter full screen": the page without the top bar and the sidebar, kept while the person moves between views.
  upms.fullScreen = {
    toggle() {
      return document.documentElement.classList.toggle('full-screen');
    },
    isOn() {
      return document.documentElement.classList.contains('full-screen');
    }
  };

  // A space's tabs, as Jira's: tabs that do not fit move into "More", from the last one back; the current tab, Shortcuts
  // and "+" always stay. Runs again when the row changes size or the header renders.
  upms.fitTabs = (nav) => {
    if (!nav || !nav.isConnected) return;
    const fit = () => {
      const more = nav.querySelector(':scope > .tab-more');
      if (!more) return;
      const tabs = [...nav.querySelectorAll(':scope > a[data-view]')];
      tabs.forEach((t) => t.classList.remove('is-overflow'));
      more.hidden = true;
      const gap = parseFloat(getComputedStyle(nav).columnGap) || 0;
      const shown = () => [...nav.children].filter((c) => !c.hidden && !c.classList.contains('is-overflow'));
      const needed = () => shown().reduce((sum, c) => sum + c.getBoundingClientRect().width + gap, -gap);
      if (needed() > nav.clientWidth) {
        more.hidden = false;
        for (let i = tabs.length - 1; i >= 0 && needed() > nav.clientWidth; i--) {
          if (tabs[i].getAttribute('aria-current') !== 'page') tabs[i].classList.add('is-overflow');
        }
      }
      const hidden = new Set(tabs.filter((t) => t.classList.contains('is-overflow')).map((t) => t.dataset.view));
      more.querySelectorAll('li[data-view]').forEach((li) => { li.hidden = !hidden.has(li.dataset.view); });
      const count = more.querySelector('.tab-more-count');
      if (count) count.textContent = hidden.size ? `(${hidden.size})` : '';
      if (!hidden.size) more.open = false;
    };
    if (!nav.upmsFit) {
      nav.upmsFit = fit;
      if (window.ResizeObserver) new ResizeObserver(() => nav.upmsFit()).observe(nav);
    }
    fit();
  };

  // An open "More" tab menu closes when the person clicks elsewhere or presses Escape.
  document.addEventListener('click', (e) => {
    document.querySelectorAll('details.tab-more[open]').forEach((d) => { if (!d.contains(e.target)) d.open = false; });
  });
  document.addEventListener('keydown', (e) => {
    if (e.key !== 'Escape') return;
    document.querySelectorAll('details.tab-more[open]').forEach((d) => {
      d.open = false;
      const summary = d.querySelector('summary');
      if (summary) summary.focus();
    });
  });

  upms.focusById = (id) => {
    const el = document.getElementById(id);
    if (el) el.focus();
  };

  // For putting focus back after the drawer closes or an item moves, which reaches the page after a round trip: only
  // when focus was lost (it is on the page itself), never taking it from where the person has moved it meanwhile.
  upms.focusByIdIfLost = (id) => {
    const active = document.activeElement;
    if (active && active !== document.body && active.isConnected) return;
    upms.focusById(id);
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

  // The formatted-text editor (Phase 4 research R2) is large, so it is loaded the first time an editor opens.
  let editorLoading = null;
  upms.loadEditor = () => {
    if (upms.editor) return Promise.resolve();
    editorLoading ??= new Promise((resolve, reject) => {
      const script = document.createElement('script');
      script.src = new URL('js/editor.js', document.baseURI).href;
      script.onload = () => resolve();
      script.onerror = () => {
        editorLoading = null;
        reject(new Error('The editor could not be loaded.'));
      };
      document.head.appendChild(script);
    });
    return editorLoading;
  };

  // Escape inside a menu or panel that handles it (data-escape-local) closes only that, never the dialog around it: the
  // key's default is what closes a dialog.
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && e.target && e.target.closest && e.target.closest('[data-escape-local]')) e.preventDefault();
  }, true);

  // Jira's "Export > Image (PNG)": the server draws the chart as SVG; the browser paints it at twice its size on a white
  // canvas and saves it. The image loads from a data address, which the page's policy allows for images.
  upms.downloadSvgAsPng = async (url, fileName) => {
    try {
      const response = await fetch(url, { credentials: 'same-origin' });
      if (!response.ok) return false;
      const svg = await response.text();
      const bytes = new TextEncoder().encode(svg);
      let binary = '';
      for (let i = 0; i < bytes.length; i += 0x8000) binary += String.fromCharCode.apply(null, bytes.subarray(i, i + 0x8000));
      const image = new Image();
      await new Promise((resolve, reject) => {
        image.onload = resolve;
        image.onerror = reject;
        image.src = 'data:image/svg+xml;base64,' + btoa(binary);
      });
      const scale = Math.min(2, 16000 / Math.max(image.width, image.height));
      const canvas = document.createElement('canvas');
      canvas.width = Math.round(image.width * scale);
      canvas.height = Math.round(image.height * scale);
      const context = canvas.getContext('2d');
      context.fillStyle = '#ffffff';
      context.fillRect(0, 0, canvas.width, canvas.height);
      context.drawImage(image, 0, 0, canvas.width, canvas.height);
      const blob = await new Promise((resolve) => canvas.toBlob(resolve, 'image/png'));
      if (!blob) return false;
      const link = document.createElement('a');
      link.href = URL.createObjectURL(blob);
      link.download = fileName;
      document.body.appendChild(link);
      link.click();
      link.remove();
      setTimeout(() => URL.revokeObjectURL(link.href), 2000);
      return true;
    } catch {
      return false;
    }
  };

  // Jira's click-to-edit: clicking a description opens its editor through the Edit button named in data-click-to-edit.
  // Links, buttons and selected text keep working as they are.
  document.addEventListener('click', (e) => {
    const area = e.target && e.target.closest ? e.target.closest('[data-click-to-edit]') : null;
    if (!area || e.target.closest('a, button, input, textarea, select')) return;
    const selection = window.getSelection();
    if (selection && selection.toString().trim() !== '') return;
    const button = document.getElementById(area.getAttribute('data-click-to-edit'));
    if (button) button.click();
  });

  // The viewer's local time for a "datetime-local" field, and its offset from UTC in minutes (Jira's "Date started").
  upms.localNow = () => {
    const now = new Date();
    const pad = (n) => String(n).padStart(2, '0');
    const value = `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}T${pad(now.getHours())}:${pad(now.getMinutes())}`;
    return [value, String(-now.getTimezoneOffset())];
  };

  // Jira's keys in an open work item: Ctrl+K (Cmd+K) opens the command palette and Q logs work, unless the person is
  // typing or a dialog inside the item is open.
  upms.itemKeys = (dialog, dotnetRef) => {
    if (!dialog || dialog.upmsItemKeys) return;
    dialog.upmsItemKeys = true;
    dialog.addEventListener('keydown', (e) => {
      const target = e.target;
      if (!target || !target.closest || target.closest('dialog') !== dialog) return;
      const key = (e.key || '').toLowerCase();
      if ((e.ctrlKey || e.metaKey) && !e.altKey && key === 'k') {
        e.preventDefault();
        dotnetRef.invokeMethodAsync('OpenPalette').catch(() => {});
        return;
      }
      if (e.ctrlKey || e.metaKey || e.altKey || key !== 'q') return;
      const typing = target.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(target.tagName);
      if (typing) return;
      e.preventDefault();
      dotnetRef.invokeMethodAsync('OpenLogWork').catch(() => {});
    });
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
