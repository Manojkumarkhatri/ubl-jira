// Attaching files from the drawer (Phase 4 FR-023, research R13): the "Attach files" button opens the file chooser, and
// files dropped anywhere on the drawer are attached too. Each file goes to .NET as a stream reference, one at a time, so
// the upload uses the circuit's own authentication and nothing new is opened on the server.
(() => {
  'use strict';
  const upms = (window.upms = window.upms || {});
  const bindings = new WeakMap();

  const hasFiles = (e) => !!e.dataTransfer && Array.from(e.dataTransfer.types || []).includes('Files');

  async function upload(dotnet, files) {
    for (const file of files) {
      try {
        await dotnet.invokeMethodAsync('UploadAsync', file.name, file.size, DotNet.createJSStreamReference(file));
      } catch {
        // The section reports refusals itself; a lost circuit ends the page anyway.
      }
    }
  }

  upms.attachments = {
    init(input, dotnet) {
      if (!input || bindings.has(input)) return;
      const zone = input.closest('dialog') || document.body;
      const section = input.closest('[data-testid=attachments]');
      let depth = 0;
      const onChange = async () => {
        const files = Array.from(input.files || []);
        input.value = '';
        await upload(dotnet, files);
      };
      const onEnter = (e) => {
        if (!hasFiles(e)) return;
        depth++;
        section?.classList.add('is-drop-target');
      };
      const onLeave = (e) => {
        if (!hasFiles(e)) return;
        depth = Math.max(0, depth - 1);
        if (depth === 0) section?.classList.remove('is-drop-target');
      };
      const onOver = (e) => {
        if (hasFiles(e) && !e.target.closest('.rte-editable')) e.preventDefault();
      };
      const onDrop = async (e) => {
        if (!hasFiles(e) || e.target.closest('.rte-editable')) return; // the editor puts dropped images in its text
        e.preventDefault();
        depth = 0;
        section?.classList.remove('is-drop-target');
        await upload(dotnet, Array.from(e.dataTransfer.files));
      };
      input.addEventListener('change', onChange);
      zone.addEventListener('dragenter', onEnter);
      zone.addEventListener('dragleave', onLeave);
      zone.addEventListener('dragover', onOver);
      zone.addEventListener('drop', onDrop);
      bindings.set(input, () => {
        input.removeEventListener('change', onChange);
        zone.removeEventListener('dragenter', onEnter);
        zone.removeEventListener('dragleave', onLeave);
        zone.removeEventListener('dragover', onOver);
        zone.removeEventListener('drop', onDrop);
      });
    },
    choose(input) {
      input?.click();
    },
    dispose(input) {
      bindings.get(input)?.();
      bindings.delete(input);
    }
  };
})();
