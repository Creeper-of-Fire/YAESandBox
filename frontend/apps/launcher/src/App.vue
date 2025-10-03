<!-- launcher/src/App.vue -->
<template>
  <div class="app-shell">
    <div class="app-content">
      <LauncherView/>
    </div>
  </div>
</template>

<script setup lang="ts">
import LauncherView from "./views/LauncherView.vue";
</script>

<!-- 新增：在这里定义 CSS 变量，作用域限定在 .app-shell 内 -->
<style>
.app-shell {
  /* --- Light Theme (Default) --- */

  /* Base Palette */
  --color-brand: #3b82f6; /* Blue 500 */
  --color-brand-hover: #2563eb; /* Blue 600 */
  --color-secondary: #64748b; /* Slate 500 */
  --color-secondary-hover: #475569; /* Slate 600 */
  --color-success: #16a34a; /* Green 600 */
  --color-success-hover: #15803d; /* Green 700 */
  --color-info: #0d9488; /* Teal 600 */
  --color-info-hover: #0f766e; /* Teal 700 */
  --color-warning: #f59e0b; /* Amber 500 */
  --color-warning-hover: #d97706; /* Amber 600 */
  --color-warning-rgb: 245, 158, 11;
  --color-danger: #dc2626; /* Red 600 */
  --color-white: #ffffff;
  --color-black: #000000;

  /* Text */
  --text-color-primary: #1f2937; /* Slate 800 */
  --text-color-secondary: #475569; /* Slate 600 */
  --text-color-muted: #64748b; /* Slate 500 */
  --text-color-accent: #111827; /* Slate 900 */
  --text-color-inverted: var(--color-white);
  --text-color-link: var(--color-brand);
  --text-color-link-hover: var(--color-brand-hover);
  --text-color-danger: #991b1b; /* Red 800 */
  --text-color-on-warning: #333;

  /* Backgrounds */
  --bg-color-main: #f8fafc; /* Slate 50 */
  --bg-color-panel: var(--color-white);
  --bg-color-card: var(--color-white);
  --bg-color-overlay: rgba(0, 0, 0, 0.6);
  --bg-color-code: #f1f5f9; /* Slate 100 */
  --bg-color-code-block: #e2e8f0; /* Slate 200 */
  --bg-color-hover: #eff6ff; /* Blue 50 */
  --bg-color-success-soft: #dcfce7; /* Green 100 */
  --bg-color-danger-soft: #fee2e2; /* Red 100 */

  /* Buttons */
  --btn-secondary-bg: #e2e8f0; /* Slate 200 */
  --btn-secondary-bg-hover: #cbd5e1; /* Slate 300 */
  --btn-secondary-text: #475569; /* Slate 600 */
  --btn-secondary-border: #e2e8f0; /* Slate 200 */

  /* Borders */
  --border-color-soft: #f1f5f9; /* Slate 100 */
  --border-color-medium: #e2e8f0; /* Slate 200 */
  --border-color-strong: #cbd5e1; /* Slate 300 */
  --border-color-divider: #e2e8f0; /* Slate 200 */
  --border-color-danger: #fca5a5; /* Red 300 */

  /* Shadows */
  --shadow-color: rgba(0, 0, 0, 0.08);
  --shadow-card: 0 1px 3px 0 var(--shadow-color), 0 1px 2px -1px var(--shadow-color);
  --shadow-modal: 0 10px 15px -3px rgba(0, 0, 0, 0.1), 0 4px 6px -4px rgba(0, 0, 0, 0.1);
}

/* --- Dark Theme --- */
@media (prefers-color-scheme: dark) {
  .app-shell {
    /* Base Palette */
    --color-brand: #60a5fa; /* Blue 400 */
    --color-brand-hover: #93c5fd; /* Blue 300 */
    --color-brand-rgb: 96, 165, 250;
    --color-secondary: #9ca3af; /* Gray 400 */
    --color-secondary-hover: #d1d5db; /* Gray 300 */
    --color-success: #4ade80; /* Green 400 */
    --color-success-hover: #86efac; /* Green 300 */
    --color-info: #2dd4bf; /* Teal 400 */
    --color-info-hover: #5eead4; /* Teal 300 */
    --color-warning: #fbbf24; /* Amber 400 */
    --color-warning-hover: #fcd34d; /* Amber 300 */
    --color-warning-rgb: 251, 191, 36;
    --color-danger: #f87171; /* Red 400 */

    /* Text */
    --text-color-primary: #e5e7eb; /* Gray 200 */
    --text-color-secondary: #9ca3af; /* Gray 400 */
    --text-color-muted: #6b7280; /* Gray 500 */
    --text-color-accent: #f9fafb; /* Gray 50 */
    --text-color-inverted: #111827; /* Gray 900 */
    --text-color-danger: #fca5a5; /* Red 300 */
    --text-color-on-warning: var(--color-black);

    /* Backgrounds */
    --bg-color-main: #111827; /* Gray 900 */
    --bg-color-panel: #1f2937; /* Gray 800 */
    --bg-color-card: #1f2937; /* Gray 800 */
    --bg-color-overlay: rgba(0, 0, 0, 0.6);
    --bg-color-code: #374151; /* Gray 700 */
    --bg-color-code-block: #1f2937; /* Gray 800 */
    --bg-color-hover: rgba(var(--color-brand-rgb), 0.1);
    --bg-color-success-soft: rgba(74, 222, 128, 0.1);
    --bg-color-danger-soft: rgba(248, 113, 113, 0.1);

    /* Buttons */
    --btn-secondary-bg: #374151; /* Gray 700 */
    --btn-secondary-bg-hover: #4b5563; /* Gray 600 */
    --btn-secondary-text: #e5e7eb; /* Gray 200 */
    --btn-secondary-border: #374151; /* Gray 700 */

    /* Borders */
    --border-color-soft: #374151; /* Gray 700 */
    --border-color-medium: #4b5563; /* Gray 600 */
    --border-color-strong: #6b7280; /* Gray 500 */
    --border-color-divider: #374151; /* Gray 700 */
    --border-color-danger: #5b2121; /* Darker Red for borders */

    /* Shadows (shadows are less prominent in dark mode) */
    --shadow-color: rgba(0, 0, 0, 0.3);
    --shadow-card: 0 1px 3px 0 var(--shadow-color), 0 1px 2px -1px var(--shadow-color);
    --shadow-modal: 0 10px 15px -3px rgba(0, 0, 0, 0.3), 0 4px 6px -4px rgba(0, 0, 0, 0.3);
  }
}
</style>

<style>
/* 样式保持不变 */
.app-shell {
  display: flex;
  flex-direction: column;
  height: 100vh;
  overflow: hidden;
  scrollbar-color: var(--color-secondary) var(--bg-color-main);
  scrollbar-width: thin;
}

.app-content {
  flex-grow: 1;
  position: relative;
}
</style>