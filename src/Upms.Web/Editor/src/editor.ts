// The formatted-text editor for descriptions and comments (Phase 4 FR-001, FR-005, FR-007; research R2, R5), in Jira's
// way: people type straight away; selecting text brings up a floating toolbar (text styles, bold and more, lists, colour,
// link); "/" inserts tables, lists and other blocks; inside a table a table toolbar adds rows and columns, merges cells and
// colours them. Alt+F10 reaches the toolbars from the keyboard. The document goes back to .NET as JSON, and the server
// checks it against its allowlist, so nothing here is trusted for safety.
import { Editor, Extension, Mark } from "@tiptap/core";
import type { ChainedCommands } from "@tiptap/core";
import StarterKit from "@tiptap/starter-kit";
import Paragraph from "@tiptap/extension-paragraph";
import { BulletList, TaskItem, TaskList } from "@tiptap/extension-list";
import { TableCell, TableHeader, TableKit } from "@tiptap/extension-table";
import Mention from "@tiptap/extension-mention";
import Image from "@tiptap/extension-image";
import { Placeholder } from "@tiptap/extensions";
import Suggestion from "@tiptap/suggestion";
import { PluginKey } from "@tiptap/pm/state";
import type { EditorView } from "@tiptap/pm/view";

interface DotNetReference {
  invokeMethodAsync<T>(method: string, ...args: unknown[]): Promise<T>;
}

interface Person {
  id: string;
  label: string;
  userName: string;
}

interface Options {
  /** The document as JSON, or null to start empty. */
  content: string | null;
  label: string;
  placeholder: string | null;
  /** The id of the hint that says how to format, for the editing area's description. */
  hintId: string | null;
  /** The limit of plain text, shown as a count near it. */
  limit: number;
  /** Receives SuggestMentionsAsync(query) calls; null turns "@" suggestions off. */
  dotnet: DotNetReference | null;
  /** Pasted and dropped images are attached to the item and shown in the text (Phase 4 FR-023). */
  images: boolean;
}

interface Instance {
  editor: Editor;
  host: HTMLElement;
  close: () => void;
}

/** The colours text and table cells can take; the server keeps only these names. */
const COLORS = ["gray", "red", "orange", "yellow", "green", "teal", "blue", "purple"] as const;

const instances = new Map<number, Instance>();
let nextHandle = 1;
let nextId = 1;

const allowedLink = (href: string): boolean => {
  try {
    const url = new URL(href);
    return url.protocol === "http:" || url.protocol === "https:" || url.protocol === "mailto:";
  } catch {
    return false;
  }
};

const capital = (text: string) => text[0].toUpperCase() + text.slice(1);

// ----- Extensions -----

/** An image is one of the item's attachments, shown from its preview address; images from elsewhere are never pasted in,
 * and the server keeps only attachments of the same item (Phase 4 FR-023). */
const AttachmentImage = Image.extend({
  addAttributes() {
    return {
      attachmentId: {
        default: null,
        parseHTML: (element: HTMLElement) => Number(element.getAttribute("data-attachment-id")) || null,
        renderHTML: (attributes: Record<string, unknown>) =>
          attributes.attachmentId
            ? { "data-attachment-id": String(attributes.attachmentId), src: `attachments/${attributes.attachmentId}/preview` }
            : {},
      },
      alt: { default: null },
    };
  },
  parseHTML() {
    return [{ tag: "img[data-attachment-id]" }];
  },
});

/** Jira's "Small text": a paragraph in a smaller size. */
const SizedParagraph = Paragraph.extend({
  addAttributes() {
    return {
      size: {
        default: null,
        parseHTML: (element: HTMLElement) => (element.classList.contains("ft-small") ? "small" : null),
        renderHTML: (attributes: Record<string, unknown>) => (attributes.size === "small" ? { class: "ft-small" } : {}),
      },
    };
  },
});

/** Bullets as discs, circles or squares. */
const StyledBulletList = BulletList.extend({
  addAttributes() {
    return {
      ...this.parent?.(),
      listStyle: {
        default: null,
        parseHTML: (element: HTMLElement) => element.getAttribute("data-list-style"),
        renderHTML: (attributes: Record<string, unknown>) =>
          attributes.listStyle ? { "data-list-style": attributes.listStyle, class: `ft-list-${attributes.listStyle}` } : {},
      },
    };
  },
});

const TextColor = Mark.create({
  name: "textColor",
  addAttributes() {
    return {
      color: {
        default: "red",
        parseHTML: (element: HTMLElement) => element.getAttribute("data-color"),
        renderHTML: (attributes: Record<string, unknown>) => ({ "data-color": attributes.color, class: `ft-color-${attributes.color}` }),
      },
    };
  },
  parseHTML() {
    return [{ tag: "span[data-color]" }];
  },
  renderHTML({ HTMLAttributes }) {
    return ["span", HTMLAttributes, 0];
  },
});

const Subscript = Mark.create({
  name: "subscript",
  excludes: "superscript",
  parseHTML() {
    return [{ tag: "sub" }];
  },
  renderHTML() {
    return ["sub", 0];
  },
  addKeyboardShortcuts() {
    return { "Mod-,": () => this.editor.commands.toggleMark(this.name) };
  },
});

const Superscript = Mark.create({
  name: "superscript",
  excludes: "subscript",
  parseHTML() {
    return [{ tag: "sup" }];
  },
  renderHTML() {
    return ["sup", 0];
  },
  addKeyboardShortcuts() {
    return { "Mod-.": () => this.editor.commands.toggleMark(this.name) };
  },
});

const cellBackground = {
  background: {
    default: null,
    parseHTML: (element: HTMLElement) => element.getAttribute("data-background"),
    renderHTML: (attributes: Record<string, unknown>) =>
      attributes.background ? { "data-background": attributes.background, class: `ft-bg-${attributes.background}` } : {},
  },
};

const ColoredCell = TableCell.extend({
  addAttributes() {
    return { ...this.parent?.(), ...cellBackground };
  },
});

const ColoredHeader = TableHeader.extend({
  addAttributes() {
    return { ...this.parent?.(), ...cellBackground };
  },
});

/** Jira's text style shortcuts: Ctrl+Alt+0 normal text, Ctrl+Alt+7 small text (headings have Ctrl+Alt+1 to 6). */
const TextStyleKeys = Extension.create({
  name: "textStyleKeys",
  addKeyboardShortcuts() {
    return {
      "Mod-Alt-0": () => setStyle(this.editor, "normal"),
      "Mod-Alt-7": () => setStyle(this.editor, "small"),
    };
  },
});

/** Escape leaves the editor: focus moves to the area around it, so the next Tab goes on to Save (research R5). That area
 * holds the editing box, so the box leaves the tab order until focus moves on. */
const leaveOnEscape = (surface: HTMLElement) =>
  Extension.create({
    name: "leaveOnEscape",
    priority: 10,
    addKeyboardShortcuts() {
      return {
        Escape: () => {
          const editable = this.editor.view.dom as HTMLElement;
          editable.setAttribute("tabindex", "-1");
          surface.addEventListener("blur", () => editable.setAttribute("tabindex", "0"), { once: true });
          surface.focus();
          return true;
        },
      };
    },
  });

// ----- Commands -----

type Style = "normal" | "small" | 1 | 2 | 3 | 4 | 5 | 6;

function setStyle(editor: Editor, style: Style): boolean {
  const chain = editor.chain().focus();
  if (style === "normal" || style === "small") {
    return chain.setParagraph().updateAttributes("paragraph", { size: style === "small" ? "small" : null }).run();
  }

  return chain.setHeading({ level: style }).run();
}

function currentStyle(editor: Editor): Style {
  for (const level of [1, 2, 3, 4, 5, 6] as const) {
    if (editor.isActive("heading", { level })) return level;
  }

  return editor.isActive("paragraph", { size: "small" }) ? "small" : "normal";
}

/** Bullets of a style: the selection becomes a bulleted list first if it is not one. */
function setBullets(editor: Editor, listStyle: string | null) {
  const chain = editor.chain().focus();
  if (!editor.isActive("bulletList")) chain.toggleBulletList();
  chain.updateAttributes("bulletList", { listStyle }).run();
}

function setNumbers(editor: Editor, type: string) {
  const chain = editor.chain().focus();
  if (!editor.isActive("orderedList")) chain.toggleOrderedList();
  chain.updateAttributes("orderedList", { type: type === "1" ? null : type }).run();
}

/** What "/" offers; each item runs on the chain that has already removed the "/query" text. */
interface SlashItem {
  title: string;
  hint: string;
  run: (chain: ChainedCommands) => void;
}

const SLASH_ITEMS: SlashItem[] = [
  { title: "Table", hint: "3 × 3 with a header row", run: (c) => c.insertTable({ rows: 3, cols: 3, withHeaderRow: true }).run() },
  { title: "Bulleted list", hint: "Type - and a space", run: (c) => c.toggleBulletList().run() },
  { title: "Numbered list", hint: "Type 1. and a space", run: (c) => c.toggleOrderedList().run() },
  { title: "Checklist", hint: "Type [] and a space", run: (c) => c.toggleTaskList().run() },
  { title: "Heading 1", hint: "Type # and a space", run: (c) => c.setHeading({ level: 1 }).run() },
  { title: "Heading 2", hint: "Type ## and a space", run: (c) => c.setHeading({ level: 2 }).run() },
  { title: "Heading 3", hint: "Type ### and a space", run: (c) => c.setHeading({ level: 3 }).run() },
  { title: "Quote", hint: "Type > and a space", run: (c) => c.toggleBlockquote().run() },
  { title: "Code block", hint: "Type ``` and a space", run: (c) => c.toggleCodeBlock().run() },
];

// ----- The editor -----

function create(host: HTMLElement, options: Options): number {
  const handle = nextHandle++;
  const area = host.querySelector<HTMLElement>(".rte-content")!;
  const surface = host.querySelector<HTMLElement>(".rte-surface")!;
  const count = host.querySelector<HTMLElement>(".rte-count");
  const people = suggestionList<Person>(host, "People to mention", "No one found",
    (person) => [person.label, Object.assign(document.createElement("span"), { className: "rte-mention-user", textContent: ` ${person.userName}` })]);
  const blocks = suggestionList<SlashItem>(host, "Insert", "Nothing matches",
    (item) => [item.title, Object.assign(document.createElement("span"), { className: "rte-mention-user", textContent: ` ${item.hint}` })]);
  const link = linkForm(host);

  const editor = new Editor({
    element: area,
    content: options.content ? JSON.parse(options.content) : "",
    extensions: [
      StarterKit.configure({
        paragraph: false,
        bulletList: false,
        heading: { levels: [1, 2, 3, 4, 5, 6] },
        horizontalRule: false,
        trailingNode: false,
        link: {
          openOnClick: false,
          autolink: true,
          linkOnPaste: true,
          protocols: ["mailto"],
          isAllowedUri: (url) => allowedLink(url),
          HTMLAttributes: { rel: "noopener noreferrer nofollow", target: "_blank" },
        },
      }),
      SizedParagraph,
      StyledBulletList,
      TaskList,
      TaskItem.configure({ nested: true }),
      TextColor,
      Subscript,
      Superscript,
      TableKit.configure({ table: { resizable: true, cellMinWidth: 60 }, tableCell: false, tableHeader: false }),
      ColoredCell,
      ColoredHeader,
      TextStyleKeys,
      ...(options.dotnet
        ? [
            Mention.configure({
              HTMLAttributes: { class: "mention" },
              suggestion: {
                char: "@",
                items: async ({ query }) => {
                  try {
                    return await options.dotnet!.invokeMethodAsync<Person[]>("SuggestMentionsAsync", query);
                  } catch {
                    return [];
                  }
                },
                render: () => people.renderer(area, (person, command) => command({ id: person.id, label: person.label })),
              },
            }),
          ]
        : []),
      Extension.create({
        name: "slashCommands",
        addProseMirrorPlugins() {
          return [
            Suggestion<SlashItem, SlashItem>({
              editor: this.editor,
              pluginKey: new PluginKey("slashCommands"),
              char: "/",
              allow: ({ editor }) => !editor.isActive("codeBlock"),
              items: ({ query, editor }) =>
                SLASH_ITEMS.filter((item) => !(item.title === "Table" && editor.isActive("table"))
                  && item.title.toLowerCase().includes(query.toLowerCase())),
              command: ({ editor, range, props }) => props.run(editor.chain().focus().deleteRange(range)),
              render: () => blocks.renderer(area, (item, command) => command(item)),
            }),
          ];
        },
      }),
      ...(options.images ? [AttachmentImage] : []),
      Placeholder.configure({ placeholder: options.placeholder ?? "" }),
      leaveOnEscape(surface),
      Extension.create({
        name: "toolbarKeys",
        addKeyboardShortcuts() {
          return {
            "Mod-k": () => (link.open(this.editor), true),
            "Alt-F10": () => (toolbars.focus(), true),
          };
        },
      }),
    ],
    editorProps: {
      attributes: {
        role: "textbox",
        "aria-multiline": "true",
        "aria-label": options.label,
        ...(options.hintId ? { "aria-describedby": options.hintId } : {}),
        class: "rte-editable",
      },
      // Pasted screenshots and dropped files are attached to the item; images then show in the text (Phase 4 FR-023).
      handlePaste: (view, event) => {
        const files = Array.from(event.clipboardData?.files ?? []).filter((f) => f.type.startsWith("image/"));
        if (!options.images || !options.dotnet || files.length === 0) return false;
        event.preventDefault();
        void attachImages(view, files, options.dotnet);
        return true;
      },
      handleDrop: (view, event, _slice, moved) => {
        const files = Array.from(event.dataTransfer?.files ?? []);
        if (moved || !options.images || !options.dotnet || files.length === 0) return false;
        event.preventDefault();
        void attachImages(view, files, options.dotnet, view.posAtCoords({ left: event.clientX, top: event.clientY })?.pos);
        return true;
      },
    },
  });

  const toolbars = floatingToolbars(host, editor, link);
  const update = () => {
    toolbars.update();
    if (count) {
      const used = editor.getText({ blockSeparator: "\n" }).length;
      const left = options.limit - used;
      count.textContent = left < options.limit / 10 ? `${left.toLocaleString()} characters left` : "";
      count.classList.toggle("over", left < 0);
    }
  };
  editor.on("transaction", update);
  editor.on("focus", update);
  editor.on("blur", () => setTimeout(update, 0));
  update();

  instances.set(handle, {
    editor,
    host,
    close: () => {
      toolbars.close();
      people.close();
      blocks.close();
      link.close(false);
      editor.destroy();
    },
  });
  return handle;
}

/** Sends each file to .NET as a stream; an image that was attached goes into the text where it was pasted or dropped. */
async function attachImages(view: EditorView, files: File[], dotnet: DotNetReference, at?: number) {
  const createStream = (window as unknown as { DotNet: { createJSStreamReference(file: File): unknown } }).DotNet.createJSStreamReference;
  for (const file of files) {
    let id: number | null = null;
    try {
      id = await dotnet.invokeMethodAsync<number | null>("UploadImageAsync", file.name, file.size, createStream(file));
    } catch {
      id = null;
    }

    const image = view.state.schema.nodes.image;
    if (!id || !image) continue;
    const node = image.create({ attachmentId: id, alt: file.name });
    view.dispatch(at === undefined
      ? view.state.tr.replaceSelectionWith(node)
      : view.state.tr.insert(Math.min(at, view.state.doc.content.size), node));
  }
}

// ----- The floating toolbars -----

interface MenuItem {
  label: string;
  /** Shown at the end of the item, such as a shortcut. */
  hint?: string;
  /** Extra classes, for previews of a style or a colour. */
  className?: string;
  checked?: boolean;
  run: () => void;
}

type MenuEntry = MenuItem | { heading: string } | "separator";

const ICONS: Record<string, string> = {
  list: '<svg viewBox="0 0 16 16" width="16" height="16" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="1.4" stroke-linecap="round"><path d="M6 4h8M6 8h8M6 12h8"/><circle cx="2.8" cy="4" r=".9" fill="currentColor"/><circle cx="2.8" cy="8" r=".9" fill="currentColor"/><circle cx="2.8" cy="12" r=".9" fill="currentColor"/></svg>',
  link: '<svg viewBox="0 0 16 16" width="16" height="16" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="1.4" stroke-linecap="round"><path d="M6.8 9.2a2.5 2.5 0 003.5 0l2.2-2.2a2.5 2.5 0 00-3.5-3.5l-.8.8M9.2 6.8a2.5 2.5 0 00-3.5 0L3.5 9a2.5 2.5 0 003.5 3.5l.8-.8"/></svg>',
  caret: '<svg viewBox="0 0 16 16" width="12" height="12" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round"><path d="M4 6l4 4 4-4"/></svg>',
};

/** The selection toolbar and the table toolbar. Buttons keep the editor's focus and selection when clicked; from the
 * keyboard, Alt+F10 moves into the toolbar, arrow keys move between its buttons and Escape returns to the text. */
function floatingToolbars(host: HTMLElement, editor: Editor, link: ReturnType<typeof linkForm>) {
  const bar = toolbar("Formatting", "rte-bubble");
  bar.innerHTML =
    `<button type="button" class="rte-btn" data-menu="style" aria-haspopup="menu" aria-expanded="false" title="Text styles">` +
    `<span class="rte-style-label">Normal text</span>${ICONS.caret}</button>` +
    `<span class="rte-split"><button type="button" class="rte-btn rte-bold" data-cmd="bold" aria-pressed="false" title="Bold (Ctrl+B)">B</button>` +
    `<button type="button" class="rte-btn rte-more" data-menu="more" aria-haspopup="menu" aria-expanded="false" aria-label="More formatting" title="More formatting">${ICONS.caret}</button></span>` +
    `<button type="button" class="rte-btn" data-menu="lists" aria-haspopup="menu" aria-expanded="false" aria-label="Lists" title="Lists">${ICONS.list}${ICONS.caret}</button>` +
    `<button type="button" class="rte-btn rte-color-btn" data-menu="color" aria-haspopup="menu" aria-expanded="false" aria-label="Text colour" title="Text colour"><span class="rte-a">A</span></button>` +
    `<button type="button" class="rte-btn" data-cmd="link" aria-pressed="false" aria-label="Link" title="Link (Ctrl+K)">${ICONS.link}</button>`;

  const table = toolbar("Table", "rte-table-bar");
  table.innerHTML =
    `<button type="button" class="rte-btn" data-menu="rows" aria-haspopup="menu" aria-expanded="false">Rows${ICONS.caret}</button>` +
    `<button type="button" class="rte-btn" data-menu="columns" aria-haspopup="menu" aria-expanded="false">Columns${ICONS.caret}</button>` +
    `<button type="button" class="rte-btn" data-cmd="mergeCells" title="Select several cells, then merge them">Merge cells</button>` +
    `<button type="button" class="rte-btn" data-cmd="splitCell">Split cell</button>` +
    `<button type="button" class="rte-btn" data-cmd="toggleHeaderRow" aria-pressed="false">Header row</button>` +
    `<button type="button" class="rte-btn" data-cmd="toggleHeaderColumn" aria-pressed="false">Header column</button>` +
    `<button type="button" class="rte-btn" data-menu="cell" aria-haspopup="menu" aria-expanded="false">Cell colour${ICONS.caret}</button>` +
    `<button type="button" class="rte-btn rte-danger" data-cmd="deleteTable">Delete table</button>`;

  const menu = document.createElement("div");
  menu.className = "rte-menu";
  menu.setAttribute("role", "menu");
  menu.hidden = true;
  host.append(bar, table, menu);

  let pinned = false;
  // True while Alt+F10 moves focus into a toolbar: the editor's blur then must not hide it.
  let entering = false;
  // The selection when Alt+F10 moved into a toolbar: the browser drops it from the text while a button has focus.
  let saved: { from: number; to: number } | null = null;
  const restore = () => {
    if (saved && !editor.isFocused) {
      editor.chain().focus().setTextSelection(saved).run();
    }
  };
  let menuButton: HTMLButtonElement | null = null;

  const focusWithin = () => [bar, table, menu].some((el) => el.contains(document.activeElement));
  const inCode = () => editor.isActive("codeBlock");

  const commands: Record<string, () => void> = {
    bold: () => editor.chain().focus().toggleBold().run(),
    link: () => link.open(editor),
    mergeCells: () => editor.chain().focus().mergeCells().run(),
    splitCell: () => editor.chain().focus().splitCell().run(),
    toggleHeaderRow: () => editor.chain().focus().toggleHeaderRow().run(),
    toggleHeaderColumn: () => editor.chain().focus().toggleHeaderColumn().run(),
    deleteTable: () => editor.chain().focus().deleteTable().run(),
  };

  const swatches = (apply: (color: string | null) => void, current: string | null, none: string): MenuEntry[] => [
    { label: none, className: "rte-swatch rte-swatch-none", checked: current === null, run: () => apply(null) },
    ...COLORS.map((color) => ({ label: capital(color), className: `rte-swatch ft-swatch-${color}`, checked: current === color, run: () => apply(color) })),
  ];

  const menus: Record<string, () => MenuEntry[]> = {
    style: () => {
      const current = currentStyle(editor);
      return [
        { label: "Normal text", hint: "Ctrl+Alt+0", checked: current === "normal", run: () => setStyle(editor, "normal") },
        { label: "Small text", hint: "Ctrl+Alt+7", className: "rte-style-small", checked: current === "small", run: () => setStyle(editor, "small") },
        ...([1, 2, 3, 4, 5, 6] as const).map((level) => ({
          label: `Heading ${level}`,
          hint: `Ctrl+Alt+${level}`,
          className: `rte-style-h${level}`,
          checked: current === level,
          run: () => setStyle(editor, level),
        })),
      ];
    },
    more: () => [
      { label: "Italic", hint: "Ctrl+I", className: "rte-italic", checked: editor.isActive("italic"), run: () => editor.chain().focus().toggleItalic().run() },
      { label: "Underline", hint: "Ctrl+U", className: "rte-underline", checked: editor.isActive("underline"), run: () => editor.chain().focus().toggleUnderline().run() },
      { label: "Strikethrough", hint: "Ctrl+Shift+S", className: "rte-strike", checked: editor.isActive("strike"), run: () => editor.chain().focus().toggleStrike().run() },
      { label: "Code", hint: "Ctrl+E", className: "rte-code", checked: editor.isActive("code"), run: () => editor.chain().focus().toggleCode().run() },
      { label: "Subscript", hint: "Ctrl+,", checked: editor.isActive("subscript"), run: () => editor.chain().focus().toggleMark("subscript").run() },
      { label: "Superscript", hint: "Ctrl+.", checked: editor.isActive("superscript"), run: () => editor.chain().focus().toggleMark("superscript").run() },
      "separator",
      { label: "Clear formatting", run: () => editor.chain().focus().unsetAllMarks().run() },
    ],
    lists: () => {
      const bullets = editor.getAttributes("bulletList").listStyle ?? "disc";
      const numbers = editor.getAttributes("orderedList").type ?? "1";
      return [
        { label: "Bulleted list", hint: "Ctrl+Shift+8", checked: editor.isActive("bulletList"), run: () => editor.chain().focus().toggleBulletList().run() },
        { label: "Numbered list", hint: "Ctrl+Shift+7", checked: editor.isActive("orderedList"), run: () => editor.chain().focus().toggleOrderedList().run() },
        { label: "Checklist", hint: "Ctrl+Shift+9", checked: editor.isActive("taskList"), run: () => editor.chain().focus().toggleTaskList().run() },
        { heading: "Bullet style" },
        ...[["disc", "• Disc"], ["circle", "◦ Circle"], ["square", "▪ Square"]].map(([style, label]) => ({
          label, checked: editor.isActive("bulletList") && bullets === style, run: () => setBullets(editor, style === "disc" ? null : style),
        })),
        { heading: "Number style" },
        ...[["1", "1. 2. 3."], ["a", "a. b. c."], ["A", "A. B. C."], ["i", "i. ii. iii."], ["I", "I. II. III."]].map(([type, label]) => ({
          label, checked: editor.isActive("orderedList") && numbers === type, run: () => setNumbers(editor, type),
        })),
      ];
    },
    color: () => swatches(
      (color) => (color ? editor.chain().focus().setMark("textColor", { color }).run() : editor.chain().focus().unsetMark("textColor").run()),
      editor.isActive("textColor") ? editor.getAttributes("textColor").color ?? null : null,
      "Default colour"),
    rows: () => [
      { label: "Insert row above", run: () => editor.chain().focus().addRowBefore().run() },
      { label: "Insert row below", run: () => editor.chain().focus().addRowAfter().run() },
      { label: "Delete row", run: () => editor.chain().focus().deleteRow().run() },
    ],
    columns: () => [
      { label: "Insert column left", run: () => editor.chain().focus().addColumnBefore().run() },
      { label: "Insert column right", run: () => editor.chain().focus().addColumnAfter().run() },
      { label: "Delete column", run: () => editor.chain().focus().deleteColumn().run() },
    ],
    cell: () => swatches(
      (color) => editor.chain().focus().setCellAttribute("background", color).run(),
      editor.getAttributes(editor.isActive("tableHeader") ? "tableHeader" : "tableCell").background ?? null,
      "No colour"),
  };

  const closeMenu = (returnFocus: boolean) => {
    if (menu.hidden) return;
    menu.hidden = true;
    menuButton?.setAttribute("aria-expanded", "false");
    if (returnFocus) menuButton?.focus();
    menuButton = null;
  };

  const openMenu = (button: HTMLButtonElement, fromKeyboard: boolean) => {
    if (menuButton === button) {
      closeMenu(fromKeyboard);
      return;
    }

    closeMenu(false);
    const entries = menus[button.dataset.menu!]();
    const palette = button.dataset.menu === "color" || button.dataset.menu === "cell";
    menu.classList.toggle("rte-palette", palette);
    menu.setAttribute("aria-label", button.getAttribute("aria-label") ?? button.textContent ?? "Menu");
    menu.replaceChildren(...entries.map((entry) => {
      if (entry === "separator") return Object.assign(document.createElement("div"), { className: "rte-menu-separator", role: "separator" });
      if ("heading" in entry) return Object.assign(document.createElement("div"), { className: "rte-menu-heading", textContent: entry.heading, role: "presentation" });
      const item = document.createElement("button");
      item.type = "button";
      item.className = `rte-menu-item ${entry.className ?? ""}`;
      item.setAttribute("role", entry.checked === undefined ? "menuitem" : "menuitemradio");
      if (entry.checked !== undefined) item.setAttribute("aria-checked", String(entry.checked));
      item.tabIndex = -1;
      item.append(Object.assign(document.createElement("span"), { className: "rte-menu-label", textContent: entry.label }));
      if (palette) item.setAttribute("aria-label", entry.label);
      if (entry.hint) item.append(Object.assign(document.createElement("kbd"), { textContent: entry.hint }));
      item.addEventListener("mousedown", (e) => e.preventDefault());
      item.addEventListener("click", () => {
        const keyboard = focusWithin();
        closeMenu(false);
        restore();
        entry.run();
        pinned = false;
        if (keyboard) editor.commands.focus();
        update();
      });
      return item;
    }));
    menuButton = button;
    button.setAttribute("aria-expanded", "true");
    menu.hidden = false;
    const box = host.getBoundingClientRect();
    const rect = button.getBoundingClientRect();
    menu.style.left = `${Math.max(0, Math.min(rect.left - box.left, box.width - menu.offsetWidth))}px`;
    menu.style.top = `${rect.bottom - box.top + 4}px`;
    if (fromKeyboard) menu.querySelector<HTMLButtonElement>(".rte-menu-item")?.focus();
  };

  const onClick = (e: MouseEvent) => {
    const button = (e.target as HTMLElement).closest<HTMLButtonElement>("button");
    if (!button) return;
    const keyboard = e.detail === 0;
    if (button.dataset.menu) {
      openMenu(button, keyboard);
    } else if (button.dataset.cmd) {
      closeMenu(false);
      restore();
      commands[button.dataset.cmd]();
      if (keyboard && button.dataset.cmd !== "link") button.focus();
      update();
    }
  };
  const keepSelection = (e: MouseEvent) => {
    if ((e.target as HTMLElement).closest("button")) e.preventDefault();
  };
  const onToolbarKey = (e: KeyboardEvent) => {
    const owner = (e.currentTarget as HTMLElement);
    const buttons = Array.from(owner.querySelectorAll<HTMLButtonElement>("button")).filter((b) => !b.disabled);
    const index = buttons.indexOf(document.activeElement as HTMLButtonElement);
    if (e.key === "Escape") {
      e.preventDefault();
      e.stopPropagation();
      pinned = false;
      closeMenu(false);
      editor.commands.focus();
      update();
      return;
    }

    if (e.key === "ArrowDown" && (document.activeElement as HTMLElement)?.dataset.menu) {
      e.preventDefault();
      openMenu(document.activeElement as HTMLButtonElement, true);
      return;
    }

    const target = { ArrowRight: index + 1, ArrowLeft: index - 1, Home: 0, End: buttons.length - 1 }[e.key];
    if (target === undefined || index < 0) return;
    e.preventDefault();
    closeMenu(false);
    buttons[(target + buttons.length) % buttons.length].focus();
  };
  const onMenuKey = (e: KeyboardEvent) => {
    const items = Array.from(menu.querySelectorAll<HTMLButtonElement>(".rte-menu-item"));
    const index = items.indexOf(document.activeElement as HTMLButtonElement);
    const step = menu.classList.contains("rte-palette") ? { ArrowRight: 1, ArrowLeft: -1, ArrowDown: 3, ArrowUp: -3 } : { ArrowDown: 1, ArrowUp: -1 };
    if (e.key === "Escape" || e.key === "Tab") {
      e.preventDefault();
      e.stopPropagation();
      closeMenu(true);
      return;
    }

    const move = step[e.key as keyof typeof step];
    if (move === undefined) return;
    e.preventDefault();
    items[(Math.max(index, 0) + move + items.length) % items.length]?.focus();
  };

  for (const el of [bar, table]) {
    el.addEventListener("click", onClick);
    el.addEventListener("mousedown", keepSelection);
    el.addEventListener("keydown", onToolbarKey);
    el.addEventListener("focusout", () => setTimeout(update, 0));
  }

  menu.addEventListener("keydown", onMenuKey);
  menu.addEventListener("focusout", () => setTimeout(() => {
    if (!focusWithin() && !editor.isFocused) closeMenu(false);
    update();
  }, 0));
  const onOutside = (e: MouseEvent) => {
    if (!menu.hidden && !menu.contains(e.target as Node) && !menuButton?.contains(e.target as Node)) closeMenu(false);
  };
  document.addEventListener("mousedown", onOutside);

  /** Places a toolbar above a rectangle, or below it when there is no room. */
  const place = (el: HTMLElement, rect: { top: number; bottom: number; left: number; right: number }, below = false) => {
    const box = host.getBoundingClientRect();
    const width = el.offsetWidth;
    const height = el.offsetHeight;
    let top = below ? rect.bottom - box.top + 6 : rect.top - box.top - height - 8;
    if (!below && rect.top - height - 8 < 0) top = rect.bottom - box.top + 8;
    const left = Math.max(0, Math.min((rect.left + rect.right) / 2 - box.left - width / 2, box.width - width));
    el.style.top = `${top}px`;
    el.style.left = `${below ? Math.max(0, Math.min(rect.left - box.left, box.width - width)) : left}px`;
  };

  const tableElement = (): HTMLElement | null => {
    const at = editor.view.domAtPos(editor.state.selection.from).node;
    const element = at instanceof HTMLElement ? at : at.parentElement;
    return element?.closest<HTMLElement>(".tableWrapper") ?? element?.closest<HTMLElement>("table") ?? null;
  };

  const refresh = () => {
    const style = currentStyle(editor);
    bar.querySelector(".rte-style-label")!.textContent =
      style === "normal" ? "Normal text" : style === "small" ? "Small text" : `Heading ${style}`;
    bar.querySelector("[data-cmd=bold]")!.setAttribute("aria-pressed", String(editor.isActive("bold")));
    bar.querySelector("[data-cmd=link]")!.setAttribute("aria-pressed", String(editor.isActive("link")));
    const color = editor.isActive("textColor") ? editor.getAttributes("textColor").color : null;
    bar.querySelector<HTMLElement>(".rte-a")!.className = `rte-a ${color ? `ft-color-${color}` : ""}`;
    table.querySelector("[data-cmd=mergeCells]")!.toggleAttribute("disabled", !editor.can().mergeCells());
    table.querySelector("[data-cmd=splitCell]")!.toggleAttribute("disabled", !editor.can().splitCell());
  };

  const update = () => {
    if (editor.isDestroyed) return;
    const active = entering || editor.isFocused || focusWithin() || !menu.hidden;
    const selected = !editor.state.selection.empty && !inCode();
    const showBar = active && (selected || pinned);
    const tableNode = active && editor.isActive("table") ? tableElement() : null;
    refresh();
    bar.hidden = !showBar;
    table.hidden = !tableNode || showBar;
    if (showBar) {
      const { from, to } = editor.state.selection;
      const start = editor.view.coordsAtPos(from);
      const end = editor.view.coordsAtPos(to, -1);
      place(bar, { top: Math.min(start.top, end.top), bottom: Math.max(start.bottom, end.bottom), left: Math.min(start.left, end.left), right: Math.max(start.right, end.right) });
    }

    if (tableNode && !showBar) {
      place(table, tableNode.getBoundingClientRect());
    }

    if (!active) {
      pinned = false;
      closeMenu(false);
    }
  };

  return {
    update,
    /** Alt+F10: into the selection toolbar, shown even with nothing selected; inside a table with nothing selected, the
     * table toolbar. */
    focus() {
      entering = true;
      saved = { from: editor.state.selection.from, to: editor.state.selection.to };
      try {
        if (editor.isActive("table") && editor.state.selection.empty) {
          update();
          table.querySelector<HTMLButtonElement>("button:not([disabled])")?.focus();
          return;
        }

        pinned = true;
        update();
        bar.querySelector<HTMLButtonElement>("button")?.focus();
      } finally {
        entering = false;
      }
    },
    close() {
      document.removeEventListener("mousedown", onOutside);
    },
  };
}

function toolbar(label: string, className: string) {
  const el = document.createElement("div");
  el.className = className;
  el.setAttribute("role", "toolbar");
  el.setAttribute("aria-label", label);
  el.hidden = true;
  return el;
}

// ----- "@" and "/" suggestions -----

type Renderer<T> = {
  onStart: (props: SuggestionProps<T>) => void;
  onUpdate: (props: SuggestionProps<T>) => void;
  onKeyDown: (props: { event: KeyboardEvent }) => boolean;
  onExit: () => void;
};

interface SuggestionProps<T> {
  items: T[];
  command: (attributes: never) => void;
  clientRect?: (() => DOMRect | null) | null;
}

/** A listbox under the caret, tied to the editing area by aria-activedescendant (research R5): people for "@", blocks
 * for "/". */
function suggestionList<T>(host: HTMLElement, label: string, empty: string, describe: (item: T) => (string | Node)[]) {
  const list = document.createElement("ul");
  list.className = "rte-mentions";
  list.id = `rte-list-${nextId++}`;
  list.setAttribute("role", "listbox");
  list.setAttribute("aria-label", label);
  list.hidden = true;
  host.appendChild(list);
  let items: T[] = [];
  let selected = 0;
  let choose: ((item: T) => void) | null = null;
  let area: HTMLElement | null = null;

  const editable = () => area?.querySelector<HTMLElement>(".rte-editable") ?? null;
  const draw = () => {
    list.replaceChildren(
      ...(items.length === 0
        ? [Object.assign(document.createElement("li"), { className: "rte-mentions-empty", textContent: empty })]
        : items.map((item, i) => {
            const option = document.createElement("li");
            option.id = `${list.id}-${i}`;
            option.setAttribute("role", "option");
            option.setAttribute("aria-selected", String(i === selected));
            option.append(...describe(item));
            option.addEventListener("mousedown", (e) => {
              e.preventDefault();
              choose?.(item);
            });
            return option;
          })),
    );
    // The text box points at the active suggestion and at the list; a text box cannot take aria-expanded (ARIA 1.2).
    if (items.length > 0) {
      editable()?.setAttribute("aria-activedescendant", `${list.id}-${selected}`);
    } else {
      editable()?.removeAttribute("aria-activedescendant");
    }
  };
  const place = (rect: DOMRect | null | undefined) => {
    if (!rect) return;
    const box = host.getBoundingClientRect();
    list.style.left = `${Math.max(0, rect.left - box.left)}px`;
    list.style.top = `${rect.bottom - box.top + 4}px`;
  };
  const close = () => {
    list.hidden = true;
    editable()?.removeAttribute("aria-activedescendant");
    editable()?.removeAttribute("aria-controls");
  };

  return {
    close,
    renderer: (editingArea: HTMLElement, pick: (item: T, command: (attributes: never) => void) => void): Renderer<T> => {
      const start = (props: SuggestionProps<T>) => {
        area = editingArea;
        items = props.items;
        selected = Math.min(selected, Math.max(0, items.length - 1));
        choose = (item) => pick(item, props.command);
        list.hidden = false;
        editable()?.setAttribute("aria-controls", list.id);
        draw();
        place(props.clientRect?.());
      };
      return {
        onStart: (props) => {
          selected = 0;
          start(props);
        },
        onUpdate: start,
        onKeyDown: ({ event }) => {
          if (event.key === "Escape") {
            close();
            return true;
          }

          if (items.length === 0) return false;
          if (event.key === "ArrowDown" || event.key === "ArrowUp") {
            selected = (selected + (event.key === "ArrowDown" ? 1 : items.length - 1)) % items.length;
            draw();
            return true;
          }

          if (event.key === "Enter" || event.key === "Tab") {
            choose?.(items[selected]);
            return true;
          }

          return false;
        },
        onExit: close,
      };
    },
  };
}

/** Ctrl+K and the Link button: a small labelled form to add, change or remove a link. */
function linkForm(host: HTMLElement) {
  const id = nextId++;
  const form = document.createElement("div");
  form.className = "rte-link";
  form.setAttribute("role", "group");
  form.setAttribute("aria-label", "Link");
  form.hidden = true;
  form.innerHTML =
    `<label for="rte-link-${id}">Link address</label>` +
    `<input id="rte-link-${id}" type="url" inputmode="url" autocomplete="off" placeholder="https://" aria-describedby="rte-link-error-${id}">` +
    `<span id="rte-link-error-${id}" class="error-text" role="alert"></span>` +
    `<button type="button" class="btn btn-sm btn-primary" data-link="apply">Apply</button>` +
    `<button type="button" class="btn btn-sm" data-link="remove">Remove link</button>` +
    `<button type="button" class="btn btn-sm btn-ghost" data-link="cancel">Cancel</button>`;
  host.querySelector(".rte-surface")!.after(form);
  const input = form.querySelector("input")!;
  const error = form.querySelector<HTMLElement>(".error-text")!;
  let editor: Editor | null = null;

  const close = (refocus: boolean) => {
    form.hidden = true;
    error.textContent = "";
    if (refocus) editor?.commands.focus();
  };
  const apply = () => {
    const href = input.value.trim();
    if (!allowedLink(href)) {
      error.textContent = "Enter an address that starts with http://, https:// or mailto:.";
      input.setAttribute("aria-invalid", "true");
      input.focus();
      return;
    }

    input.removeAttribute("aria-invalid");
    const chain = editor!.chain().focus().extendMarkRange("link");
    if (editor!.state.selection.empty && !editor!.isActive("link")) {
      chain.insertContent({ type: "text", text: href, marks: [{ type: "link", attrs: { href } }] }).run();
    } else {
      chain.setLink({ href }).run();
    }

    close(false);
  };
  form.addEventListener("click", (e) => {
    const action = (e.target as HTMLElement).closest<HTMLElement>("[data-link]")?.dataset.link;
    if (action === "apply") apply();
    else if (action === "remove") {
      editor?.chain().focus().extendMarkRange("link").unsetLink().run();
      close(false);
    } else if (action === "cancel") close(true);
  });
  form.addEventListener("keydown", (e) => {
    if (e.key === "Enter") {
      e.preventDefault();
      apply();
    } else if (e.key === "Escape") {
      e.preventDefault();
      e.stopPropagation();
      close(true);
    }
  });

  return {
    close,
    open(target: Editor) {
      editor = target;
      input.value = target.getAttributes("link").href ?? "";
      form.hidden = false;
      input.focus();
      input.select();
    },
  };
}

// ----- The interface .NET calls -----

function getJson(handle: number): string | null {
  const instance = instances.get(handle);
  return instance ? JSON.stringify(instance.editor.getJSON()) : null;
}

function isEmpty(handle: number): boolean {
  return instances.get(handle)?.editor.isEmpty ?? true;
}

function setContent(handle: number, content: string | null) {
  instances.get(handle)?.editor.commands.setContent(content ? JSON.parse(content) : "");
}

function focus(handle: number) {
  instances.get(handle)?.editor.commands.focus("end");
}

function destroy(handle: number) {
  instances.get(handle)?.close();
  instances.delete(handle);
}

const upms = ((window as unknown as { upms?: Record<string, unknown> }).upms ??= {});
upms.editor = { create, getJson, isEmpty, setContent, focus, destroy };
