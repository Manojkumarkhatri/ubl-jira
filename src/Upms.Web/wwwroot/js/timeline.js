// U-PMS timeline (Phase 2 research R12): dragging bars with a pointer. The component draws bars at their dates; this
// script only previews a drag with a floating copy and, on release, calls the component once with the whole days
// moved. Loaded as a classic script from this site only (CSP script-src 'self').
(() => {
  'use strict';
  const upms = (window.upms = window.upms || {});
  const threshold = 3; // pixels before a press becomes a drag

  upms.timeline = {
    init(scroller, dotnetRef) {
      if (!scroller || scroller.dataset.timelineReady) return;
      scroller.dataset.timelineReady = 'true';
      let drag = null;
      let swallowClick = false;

      // The component moves a focused bar with the arrow keys and treats Enter itself: keep the arrows from
      // scrolling and Enter from also "clicking" the bar.
      scroller.addEventListener('keydown', (e) => {
        if (!e.target.closest || !e.target.closest('.tl-bar')) return;
        if (e.key === 'ArrowLeft' || e.key === 'ArrowRight' || e.key === 'Enter') e.preventDefault();
      });

      // A drag ends with a click on the bar; it must not also open the task.
      scroller.addEventListener('click', (e) => {
        if (!swallowClick) return;
        swallowClick = false;
        e.stopPropagation();
        e.preventDefault();
      }, true);

      scroller.addEventListener('pointerdown', (e) => {
        const bar = e.target.closest && e.target.closest('.tl-bar[data-draggable="true"]');
        if (!bar || e.button !== 0) return;
        const handle = e.target.closest('[data-drag]');
        drag = { bar, mode: handle ? handle.dataset.drag : 'move', x: e.clientX, pointerId: e.pointerId, ghost: null, days: 0 };
        bar.setPointerCapture(e.pointerId);
      });

      scroller.addEventListener('pointermove', (e) => {
        if (!drag || e.pointerId !== drag.pointerId) return;
        const dx = e.clientX - drag.x;
        if (!drag.ghost && Math.abs(dx) < threshold) return;
        const ppd = parseFloat(scroller.dataset.ppd) || 8;
        const rect = drag.bar.getBoundingClientRect();
        const dayCount = Math.max(1, Math.round(rect.width / ppd));
        let days = Math.round(dx / ppd);
        // An end never passes the other end: the task keeps at least one day.
        if (drag.mode === 'end') days = Math.max(days, 1 - dayCount);
        if (drag.mode === 'start') days = Math.min(days, dayCount - 1);
        drag.days = days;
        if (!drag.ghost) {
          drag.ghost = document.createElement('div');
          drag.ghost.className = 'tl-ghost';
          drag.ghost.setAttribute('aria-hidden', 'true');
          document.body.appendChild(drag.ghost);
          drag.bar.classList.add('is-dragging');
        }

        const shift = days * ppd;
        const left = drag.mode === 'end' ? rect.left : rect.left + shift;
        const width = drag.mode === 'move' ? rect.width : drag.mode === 'end' ? rect.width + shift : rect.width - shift;
        Object.assign(drag.ghost.style, { left: `${left}px`, top: `${rect.top}px`, width: `${width}px`, height: `${rect.height}px` });
      });

      const finish = (e, cancelled) => {
        if (!drag || e.pointerId !== drag.pointerId) return;
        const done = drag;
        drag = null;
        if (!done.ghost) return; // a plain click
        done.ghost.remove();
        done.bar.classList.remove('is-dragging');
        swallowClick = true;
        setTimeout(() => { swallowClick = false; }, 0);
        if (!cancelled && done.days !== 0 && dotnetRef) {
          dotnetRef.invokeMethodAsync('OnBarDragged', done.bar.dataset.key, done.mode, done.days).catch(() => {});
        }
      };
      scroller.addEventListener('pointerup', (e) => finish(e, false));
      scroller.addEventListener('pointercancel', (e) => finish(e, true));
    },

    // Brings today's line into the middle of the visible track.
    scrollToToday(scroller) {
      const line = scroller && scroller.querySelector('.tl-today');
      if (line) scroller.scrollLeft = Math.max(0, line.offsetLeft - scroller.clientWidth / 2);
    }
  };
})();
