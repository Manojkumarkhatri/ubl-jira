// Bundles the formatted-text editor into one classic script served from the site (Phase 4 research R2), so the page's
// Content Security Policy stays script-src 'self'. The output is committed; CI rebuilds it and checks it is unchanged.
import { build } from "esbuild";

await build({
  entryPoints: ["src/editor.ts"],
  outfile: "../wwwroot/js/editor.js",
  bundle: true,
  minify: true,
  format: "iife",
  target: ["es2022"],
  charset: "utf8",
  legalComments: "none",
  banner: {
    js: "/*! U-PMS formatted-text editor. Built from src/Upms.Web/Editor with TipTap and ProseMirror (MIT licence). */",
  },
  logLevel: "info",
});
