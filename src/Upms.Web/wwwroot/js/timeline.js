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

    // Draws finish-to-start arrows (Phase 3 FR-050): from the end of the item waited for to the start of the waiting
    // item. The arrows are hidden from assistive technology; each bar's name says what it waits for.
    drawArrows(scroller, deps) {
      const svg = scroller && scroller.querySelector('svg.tl-arrows');
      if (!svg) return;
      const table = svg.parentElement;
      const ns = 'http://www.w3.org/2000/svg';
      svg.setAttribute('width', table.scrollWidth);
      svg.setAttribute('height', table.scrollHeight);
      svg.replaceChildren();
      const defs = document.createElementNS(ns, 'defs');
      for (const [id, cls] of [['tl-head', 'tl-arrow-head'], ['tl-head-late', 'tl-arrow-head tl-arrow-late']]) {
        const marker = document.createElementNS(ns, 'marker');
        marker.setAttribute('id', id);
        marker.setAttribute('viewBox', '0 0 8 8');
        marker.setAttribute('refX', '8');
        marker.setAttribute('refY', '4');
        marker.setAttribute('markerWidth', '8');
        marker.setAttribute('markerHeight', '8');
        marker.setAttribute('orient', 'auto');
        const tip = document.createElementNS(ns, 'path');
        tip.setAttribute('d', 'M0,0 L8,4 L0,8 z');
        tip.setAttribute('class', cls);
        marker.appendChild(tip);
        defs.appendChild(marker);
      }
      svg.appendChild(defs);
      const origin = table.getBoundingClientRect();
      for (const dep of deps || []) {
        const from = document.getElementById(`tl-bar-${dep.from}`);
        const to = document.getElementById(`tl-bar-${dep.to}`);
        if (!from || !to || !table.contains(from) || !table.contains(to)) continue;
        const a = from.getBoundingClientRect();
        const b = to.getBoundingClientRect();
        const x1 = a.right - origin.left;
        const y1 = a.top + a.height / 2 - origin.top;
        const x2 = b.left - origin.left;
        const y2 = b.top + b.height / 2 - origin.top;
        const bend = Math.max(x1 + 8, Math.min(x2 - 8, x1 + 16));
        const path = document.createElementNS(ns, 'path');
        path.setAttribute('d', x2 - 8 >= x1 + 8
          ? `M${x1},${y1} H${bend} V${y2} H${x2}`
          : `M${x1},${y1} h8 V${(y1 + y2) / 2} H${x2 - 8} V${y2} H${x2}`);
        path.setAttribute('class', dep.late ? 'tl-arrow tl-arrow-late' : 'tl-arrow');
        path.setAttribute('marker-end', dep.late ? 'url(#tl-head-late)' : 'url(#tl-head)');
        svg.appendChild(path);
      }
    },

    // Brings today's line a third of the way into the chart, past the list of work on the left, as Jira's "Today"
    // does; when asked from the button it scrolls smoothly and the line flashes so the jump is seen.
    scrollToToday(scroller, smooth) {
      const line = scroller && scroller.querySelector('.tl-today');
      if (!line) return;
      const corner = scroller.querySelector('.tl-corner');
      const list = corner ? corner.offsetWidth : 0;
      const x = line.getBoundingClientRect().left - scroller.getBoundingClientRect().left + scroller.scrollLeft;
      const left = Math.max(0, x - list - (scroller.clientWidth - list) / 3);
      scroller.scrollTo({ left, behavior: smooth ? 'smooth' : 'auto' });
      // A page in the background does not animate: make sure the jump happens anyway.
      if (smooth) setTimeout(() => { if (Math.abs(scroller.scrollLeft - left) > 2) scroller.scrollLeft = left; }, 600);
      if (smooth) {
        line.classList.remove('is-flashing');
        void line.offsetWidth; // restart the animation
        line.classList.add('is-flashing');
      }
    }
  };
})();
